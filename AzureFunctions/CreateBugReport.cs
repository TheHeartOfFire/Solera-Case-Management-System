using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Collections.Generic;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

public class CreateBugReport
{
    private static readonly HttpClient _httpClient = new();
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Function("CreateBugReport")]
    public static async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req,
        FunctionContext executionContext)
    {
        var logger = executionContext.GetLogger("CreateBugReport");
        logger.LogInformation("C# HTTP trigger function processed a request.");

        string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        var data = JsonSerializer.Deserialize<BugReportPayload>(requestBody, _jsonOptions);

        // Validate required fields
        if (data == null ||
            string.IsNullOrWhiteSpace(data.Title) ||
            string.IsNullOrWhiteSpace(data.Description) ||
            string.IsNullOrWhiteSpace(data.LogContent) ||
            string.IsNullOrWhiteSpace(data.AppVersion) ||
            string.IsNullOrWhiteSpace(data.OsVersion))
        {
            logger.LogWarning("Missing or invalid required fields in bug report payload.");
            var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequestResponse.WriteStringAsync("Missing or invalid required fields: Title, Description, LogContent, AppVersion, OsVersion are all required.");
            return badRequestResponse;
        }
        // Get secrets from environment variables (Application Settings in Azure)
        var pat = Environment.GetEnvironmentVariable("GITHUB_PAT")?.Trim();
        var repoOwner = Environment.GetEnvironmentVariable("GITHUB_REPO_OWNER")?.Trim();
        var repoName = Environment.GetEnvironmentVariable("GITHUB_REPO_NAME")?.Trim();

        if (string.IsNullOrEmpty(pat) || string.IsNullOrEmpty(repoOwner) || string.IsNullOrEmpty(repoName))
        {
            logger.LogError("Missing required configuration for GitHub integration.");
            return req.CreateResponse(HttpStatusCode.InternalServerError);
        }

        try
        {
            // 1. Create Gist
            var gistUrl = await CreateGistAsync(data.LogContent, pat, logger);
            if (gistUrl == null)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync("Failed to create Gist for the bug report.");
                return errorResponse;
            }

            // 2. Trigger Repository Dispatch
            var dispatchUrl = $"https://api.github.com/repos/{repoOwner}/{repoName}/dispatches";
            var encodedDescription = Convert.ToBase64String(Encoding.UTF8.GetBytes(data.Description));

            var dispatchPayload = new
            {
                event_type = "create-bug-report",
                client_payload = new
                {
                    title = data.Title,
                    description_base64 = encodedDescription,
                    gist_url = gistUrl,
                    app_version = data.AppVersion,
                    os_version = data.OsVersion
                }
            };

            var jsonPayload = JsonSerializer.Serialize(dispatchPayload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post, dispatchUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("token", pat);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("AMFormsCST-BugReportFunction", "1.0"));
            request.Content = content;

            var response = await _httpClient.SendAsync(request);

            var clientResponse = req.CreateResponse(response.StatusCode);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                logger.LogError("Failed to trigger repository dispatch. RepoOwner: {RepoOwner}, RepoName: {RepoName}, Status: {StatusCode}, Response: {ErrorContent}", 
                                    repoOwner, repoName, response.StatusCode, errorContent);
                await clientResponse.WriteStringAsync($"Failed to trigger repository dispatch for {repoOwner}/{repoName}: {errorContent}");
            }
            else
            {
                await clientResponse.WriteStringAsync("Bug report submitted successfully.");
            }
            return clientResponse;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An unexpected error occurred while processing the bug report. Exception: {Exception}", ex.ToString());
            return req.CreateResponse(HttpStatusCode.InternalServerError);
        }
    }

    private static async Task<string?> CreateGistAsync(string logContent, string pat, ILogger log)
    {
        var gistPayload = new
        {
            description = $"AMFormsCST Log File - {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC",
            @public = false,
            files = new Dictionary<string, object>
            {
                ["log.txt"] = new { content = logContent ?? "No log content provided." }
            }
        };

        var jsonPayload = JsonSerializer.Serialize(gistPayload);
        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.github.com/gists");
        request.Headers.Authorization = new AuthenticationHeaderValue("token", pat);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("AMFormsCST-BugReportFunction", "1.0"));
        request.Content = content;

        var response = await _httpClient.SendAsync(request);

        if (response.IsSuccessStatusCode)
        {
            var responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);
            return doc.RootElement.GetProperty("html_url").GetString();
        }

        var errorBody = await response.Content.ReadAsStringAsync();
        log.LogError("Failed to create Gist. Status: {StatusCode}, Body: {ErrorBody}", response.StatusCode, errorBody);
        return null;
    }
}

public class BugReportPayload
{
    /// <summary>
    /// The title of the bug report. Should be a brief summary of the issue.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// A detailed description of the bug, including steps to reproduce and expected behavior.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// The content of the log file related to the bug. Should be plain text.
    /// </summary>
    public string LogContent { get; set; }

    /// <summary>
    /// The version of the application where the bug was encountered. Example: "1.2.3".
    /// </summary>
    public string AppVersion { get; set; }

    /// <summary>
    /// The operating system version of the user's environment. Example: "Windows 10.0.19045".
    /// </summary>
    public string OsVersion { get; set; }
}
