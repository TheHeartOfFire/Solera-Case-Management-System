namespace AMFormsCST.Core.Types.IPC;

/// <summary>
/// Standard action identifiers for Solera CMS IPC communication.
/// </summary>
public static class IpcActions
{
    public const string Ping = "Ping";
    public const string BringToFront = "BringToFront";
    public const string OpenFormgen = "OpenFormgen";
    public const string LoadCase = "LoadCase";
    public const string SetDashboardField = "SetDashboardField";
    public const string GetLiveVariables = "GetLiveVariables";
    public const string ParseCaseNotes = "ParseCaseNotes";

    // Standard Project Citadel actions for seamless future bridge
    public const string CitadelBrainDump = "Citadel.BrainDump";
    public const string CitadelLogTimeBlock = "Citadel.LogTimeBlock";
}

