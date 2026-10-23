using System.Text.Json.Serialization;

namespace AMFormsCST.Core.Types.IPC;

/// <summary>
/// Envelope for responses returned from the IPC Named Pipe server.
/// </summary>
public class IpcResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("data")]
    public Dictionary<string, string> Data { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public static IpcResponse Ok(string? message = null, Dictionary<string, string>? data = null)
    {
        var resp = new IpcResponse { Success = true, Message = message };
        if (data != null)
        {
            foreach (var kv in data) resp.Data[kv.Key] = kv.Value;
        }
        return resp;
    }

    public static IpcResponse Fail(string errorMessage)
    {
        return new IpcResponse { Success = false, Message = errorMessage };
    }
}

