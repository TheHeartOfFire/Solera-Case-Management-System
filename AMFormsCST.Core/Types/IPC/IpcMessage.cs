using System.Text.Json.Serialization;

namespace AMFormsCST.Core.Types.IPC;

/// <summary>
/// Envelope for Inter-Process Communication messages between Solera CMS, browser extensions, and Project Citadel.
/// </summary>
public class IpcMessage
{
    /// <summary>
    /// Target destination or module: "SoleraCMS" or "Citadel".
    /// </summary>
    [JsonPropertyName("target")]
    public string Target { get; set; } = "SoleraCMS";

    /// <summary>
    /// Action verb, e.g. "LoadCase", "SetDashboardField", "OpenFormgen", "GetLiveVariables".
    /// </summary>
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Key-value dictionary carrying structured parameters.
    /// </summary>
    [JsonPropertyName("payload")]
    public Dictionary<string, string> Payload { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Optional raw text body, e.g. full case text, plain text template, or raw FormGen code.
    /// </summary>
    [JsonPropertyName("rawData")]
    public string? RawData { get; set; }

    /// <summary>
    /// UTC timestamp of message dispatch.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

