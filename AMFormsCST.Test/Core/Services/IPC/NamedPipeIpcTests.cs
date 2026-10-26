using AMFormsCST.Core.Services.IPC;
using AMFormsCST.Core.Types.IPC;
using System.IO;
using Assert = Xunit.Assert;
using Xunit.Abstractions;

namespace AMFormsCST.Test.Core.Services.IPC;

public class NamedPipeIpcTests
{

    [Fact]
    public async Task SendMessageAsync_PingAction_ReturnsSuccessResponse()
    {
        var pipeName = $"SoleraCMS_TestPipe_{Guid.NewGuid():N}";
        Console.Error.WriteLine($"[TEST] Starting Ping test with pipe: {pipeName}");

        await using var server = new NamedPipeIpcServer(async message =>
        {
            Console.Error.WriteLine($"[TEST] Server handler invoked with: {message.Action}");
            if (message.Action == IpcActions.Ping)
            {
                return IpcResponse.Ok("Pong", new Dictionary<string, string> { ["ServerStatus"] = "Running" });
            }
            return IpcResponse.Fail("Unknown action");
        }, pipeName);

        Console.Error.WriteLine("[TEST] Calling server.Start()...");
        server.Start();
        Console.Error.WriteLine("[TEST] server.Start() finished.");

        var request = new IpcMessage
        {
            Target = "SoleraCMS",
            Action = IpcActions.Ping,
            Payload = { ["ClientVersion"] = "1.0" }
        };

        Console.Error.WriteLine("[TEST] Calling SendMessageAsync...");
        var response = await NamedPipeIpcClient.SendMessageAsync(request, pipeName, timeoutMs: 3000);
        Console.Error.WriteLine($"[TEST] SendMessageAsync finished, success={response.Success}, msg={response.Message}");

        Assert.NotNull(response);
        Assert.True(response.Success, $"Expected success, but failed with: {response.Message}");
        Assert.Equal("Pong", response.Message);
        Assert.Equal("Running", response.Data["ServerStatus"]);
        Console.Error.WriteLine("[TEST] Ping test assertions passed, disposing server...");
    }

    [Fact]
    public async Task SendMessageAsync_GetLiveVariables_TransfersVariablesProperly()
    {
        var pipeName = $"SoleraCMS_TestPipe_{Guid.NewGuid():N}";

        var liveVars = new IpcLiveVariables
        {
            ServerId = "AL01",
            DealershipName = "Capitol Chevrolet",
            CaseNumber = "00987654",
            PrimaryCompanyNumber = "1",
            CompanyNumbers = ["1", "2"],
            ContactName = "Jane Doe",
            ActiveTemplateName = "Billing Follow-up",
            ActiveTemplateText = "Thank you for contacting billing."
        };

        await using var server = new NamedPipeIpcServer(async message =>
        {
            if (message.Action == IpcActions.GetLiveVariables)
            {
                return IpcResponse.Ok("Variables retrieved", liveVars.ToDictionary());
            }
            return IpcResponse.Fail("Invalid action");
        }, pipeName);

        server.Start();

        var request = new IpcMessage
        {
            Target = "SoleraCMS",
            Action = IpcActions.GetLiveVariables
        };

        var response = await NamedPipeIpcClient.SendMessageAsync(request, pipeName, timeoutMs: 3000);

        Assert.NotNull(response);
        Assert.True(response.Success, $"Expected success, but got: {response.Message}");
        Assert.Equal("AL01", response.Data[nameof(IpcLiveVariables.ServerId)]);
        Assert.Equal("Capitol Chevrolet", response.Data[nameof(IpcLiveVariables.DealershipName)]);
        Assert.Equal("00987654", response.Data[nameof(IpcLiveVariables.CaseNumber)]);
        Assert.Equal("Jane Doe", response.Data[nameof(IpcLiveVariables.ContactName)]);
        Assert.Equal("Thank you for contacting billing.", response.Data[nameof(IpcLiveVariables.ActiveTemplateText)]);
    }

    [Fact]
    public async Task SendMessageAsync_WhenServerNotRunning_ReturnsFailTimeout()
    {
        var nonExistentPipe = $"NonExistent_Pipe_{Guid.NewGuid():N}";
        var request = new IpcMessage { Action = "Test" };

        var response = await NamedPipeIpcClient.SendMessageAsync(request, nonExistentPipe, timeoutMs: 200);

        Assert.NotNull(response);
        Assert.False(response.Success);
    }

    [Fact]
    public async Task SendMessageAsync_ParseCodeBlock_PayloadTransfersCorrectly()
    {
        var pipeName = $"SoleraCMS_TestPipe_{Guid.NewGuid():N}";
        const string testFormula = "IF(DEAL_TYPE = 'LEASE', 500, 0)";

        await using var server = new NamedPipeIpcServer(async message =>
        {
            if (message.Action == IpcActions.ParseCodeBlock)
            {
                message.Payload.TryGetValue("code", out var code);
                return IpcResponse.Ok("Code parsed", new Dictionary<string, string> { ["parsedCode"] = code ?? "" });
            }
            return IpcResponse.Fail("Invalid action");
        }, pipeName);

        server.Start();

        var request = new IpcMessage
        {
            Target = "SoleraCMS",
            Action = IpcActions.ParseCodeBlock,
            Payload = { ["code"] = testFormula }
        };

        var response = await NamedPipeIpcClient.SendMessageAsync(request, pipeName, timeoutMs: 3000);

        Assert.NotNull(response);
        Assert.True(response.Success);
        Assert.Equal(testFormula, response.Data["parsedCode"]);
    }
}

