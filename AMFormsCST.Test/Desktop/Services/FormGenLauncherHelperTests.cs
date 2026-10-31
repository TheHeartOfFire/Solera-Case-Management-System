using System;
using System.IO;
using AMFormsCST.Desktop.Services;
using Xunit;

namespace AMFormsCST.Test.Desktop.Services;

[Collection("IpcSocketTests")]
public class FormGenLauncherHelperTests
{
    [Fact]
    public void OpenInFormGen_WhenPathIsEmpty_ReturnsFalse()
    {
        bool result = FormGenLauncherHelper.OpenInFormGen(string.Empty);
        Assert.False(result);
    }

    [Fact]
    public void OpenInFormGen_WhenFileDoesNotExist_ReturnsFalse()
    {
        bool result = FormGenLauncherHelper.OpenInFormGen(@"C:\NonExistent_File_12345.formgen");
        Assert.False(result);
    }

    [Fact]
    public void ResolveFormGenBatchPath_WhenExplicitPathProvided_ReturnsExplicitPath()
    {
        var tempBat = Path.ChangeExtension(Path.GetTempFileName(), ".bat");
        try
        {
            File.WriteAllText(tempBat, "@echo off");
            var resolved = FormGenLauncherHelper.ResolveFormGenBatchPath(tempBat);
            Assert.Equal(tempBat, resolved);
        }
        finally
        {
            if (File.Exists(tempBat)) File.Delete(tempBat);
        }
    }

    [Fact]
    public void EnsureAgentDeployedAndPatched_WhenBatchFileExists_PatchesWithAgentAndWorkingDir()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "FormGenTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var tempBat = Path.Combine(tempDir, "NewFormGen.bat");

        try
        {
            File.WriteAllText(tempBat, "java -Xms1024m -cp * -ea com.automate.formgen.NewFormGen %*");

            FormGenLauncherHelper.EnsureAgentDeployedAndPatched(tempBat);

            var patched = File.ReadAllText(tempBat);
            Assert.Contains("cd /d \"%~dp0\"", patched);
            Assert.Contains("-javaagent:FormGenAgent.jar", patched);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void RestoreVanillaBatch_WhenPatched_RemovesAgentParameters()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "FormGenTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var tempBat = Path.Combine(tempDir, "NewFormGen.bat");

        try
        {
            File.WriteAllText(tempBat, "cd /d \"%~dp0\"\r\njava -Xms1024m -cp * -javaagent:FormGenAgent.jar -Dformgen.file=\"%~f1\" com.automate.formgen.NewFormGen %*");

            FormGenLauncherHelper.RestoreVanillaBatch(tempBat);

            var restored = File.ReadAllText(tempBat);
            Assert.DoesNotContain("FormGenAgent.jar", restored);
            Assert.Contains("com.automate.formgen.NewFormGen", restored);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void IsAgentActive_WhenPortFileDoesNotExist_ReturnsFalse()
    {
        var tempPortFile = UsePrivatePortFile();
        try
        {
            if (File.Exists(tempPortFile)) File.Delete(tempPortFile);
            Assert.False(FormGenLauncherHelper.IsAgentActive());
        }
        catch { }
    }

    [Fact]
    public void SyncStateToAgent_WhenPortFileDoesNotExist_ReturnsFalse()
    {
        var tempPortFile = UsePrivatePortFile();
        try
        {
            if (File.Exists(tempPortFile)) File.Delete(tempPortFile);
            Assert.False(FormGenLauncherHelper.SyncStateToAgent());
        }
        catch { }
    }

    [Fact]
    public void GetActiveFormFromAgent_WhenPortFileDoesNotExist_ReturnsNull()
    {
        var tempPortFile = UsePrivatePortFile();
        try
        {
            if (File.Exists(tempPortFile)) File.Delete(tempPortFile);
            Assert.Null(FormGenLauncherHelper.GetActiveFormFromAgent());
        }
        catch { }
    }

    [Fact]
    public void AgentSocketMethods_WithMockTcpServer_RespondCorrectly()
    {
        var tempPortFile = UsePrivatePortFile();
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;

        File.WriteAllText(tempPortFile, port.ToString());

        var serverTask = Task.Run(() =>
        {
            for (int i = 0; i < 3; i++)
            {
                using var client = listener.AcceptTcpClient();
                using var stream = client.GetStream();
                using var reader = new StreamReader(stream, new System.Text.UTF8Encoding(false));
                using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false)) { AutoFlush = true };

                var line = reader.ReadLine();
                if (line == "STATUS")
                {
                    writer.WriteLine("CONNECTED: hasActiveFile=true");
                }
                else if (line?.StartsWith("SCMT_PATH:") == true || line?.StartsWith("VARS:") == true)
                {
                    writer.WriteLine("OK");
                }
                else if (line == "GET_ACTIVE_FORM")
                {
                    writer.WriteLine(@"D:\Forms\TestForm.formgen");
                }
            }
        });

        try
        {
            Assert.True(FormGenLauncherHelper.IsAgentActive());
            Assert.True(FormGenLauncherHelper.SyncStateToAgent(scmtExecutablePath: @"C:\Test\SCMT.exe"));
            Assert.Equal(@"D:\Forms\TestForm.formgen", FormGenLauncherHelper.GetActiveFormFromAgent());
        }
        finally
        {
            if (File.Exists(tempPortFile)) File.Delete(tempPortFile);
            listener.Stop();
        }
    }

    [Fact]
    public void PushFormToFormGen_WhenFileDoesNotExist_ReturnsFalse()
    {
        bool result = FormGenLauncherHelper.PushFormToFormGen(@"C:\NonExistent_Form_98765.formgen", Guid.NewGuid().ToString());
        Assert.False(result);
    }

    [Fact]
    public void PushFormToFormGen_WhenPathIsEmpty_ReturnsFalse()
    {
        bool result = FormGenLauncherHelper.PushFormToFormGen(string.Empty);
        Assert.False(result);
    }

    [Fact]
    public void TryPushViaIpc_WhenAgentReceivesPushCommand_ReturnsTrue()
    {
        var tempPortFile = UsePrivatePortFile();
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        File.WriteAllText(tempPortFile, port.ToString());

        string? receivedLine = null;

        System.Threading.Tasks.Task.Run(() =>
        {
            using var client = listener.AcceptTcpClient();
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
            using var writer = new StreamWriter(stream, System.Text.Encoding.UTF8) { AutoFlush = true };

            receivedLine = reader.ReadLine();
            writer.WriteLine("OK");
        });

        try
        {
            var fakeFile = Path.GetTempFileName();
            try
            {
                var testUuid = "11112222-3333-4444-5555-666677778888";
                bool ok = FormGenLauncherHelper.TryPushViaIpc(fakeFile, testUuid);
                Assert.True(ok);
                Assert.NotNull(receivedLine);
                Assert.Equal($"PUSH_FORM:{testUuid}|{fakeFile}", receivedLine);
            }
            finally
            {
                if (File.Exists(fakeFile)) File.Delete(fakeFile);
            }
        }
        finally
        {
            if (File.Exists(tempPortFile)) File.Delete(tempPortFile);
            listener.Stop();
        }
    }

    [Fact]
    public void UserSettings_DefaultFeatureToggles_AreAllEnabledByDefault()
    {
        var orgMock = new Moq.Mock<AMFormsCST.Core.Interfaces.UserSettings.IOrgVariables>();
        var settings = new AMFormsCST.Core.Types.UserSettings.UserSettings(orgMock.Object);
        Assert.True(settings.IsFormGenPatched);
        Assert.True(settings.FormGenPreserveUdfTab);
        Assert.True(settings.FormGenCanvasPasteField);
        Assert.True(settings.FormGenAlreadyOpenToast);
        Assert.True(settings.FormGenContextMenus);
        Assert.True(settings.FormGenEditorUndoRedo);
        Assert.True(settings.FormGenDialogAutoFill);
        Assert.True(settings.FormGenAutoRestoreForms);
        Assert.True(settings.EnableShiftRightClickFlyout);
    }

    [Fact]
    public void WriteAgentConfig_WritesPropertiesFileWithFeatureFlags()
    {
        var tempPortFile = UsePrivatePortFile();
        var dir = Path.GetDirectoryName(tempPortFile)!;
        var configPath = Path.Combine(dir, "formgen_agent.properties");

        try
        {
            var orgMock = new Moq.Mock<AMFormsCST.Core.Interfaces.UserSettings.IOrgVariables>();
            var settings = new AMFormsCST.Core.Types.UserSettings.UserSettings(orgMock.Object)
            {
                FormGenPreserveUdfTab = true,
                FormGenCanvasPasteField = false,
                FormGenAlreadyOpenToast = true
            };
            FormGenLauncherHelper.WriteAgentConfig(settings);

            Assert.True(File.Exists(configPath));
            var content = File.ReadAllText(configPath);
            Assert.Contains("FormGenPreserveUdfTab=true", content);
            Assert.Contains("FormGenCanvasPasteField=false", content);
            Assert.Contains("FormGenAlreadyOpenToast=true", content);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void RestoreVanillaBatch_AlsoRemovesDeployedJarFile()
    {
        var tempPortFile = UsePrivatePortFile();
        var tempDir = Path.GetDirectoryName(tempPortFile)!;
        var tempBat = Path.Combine(tempDir, "NewFormGen.bat");
        var tempJar = Path.Combine(tempDir, "FormGenAgent.jar");

        try
        {
            File.WriteAllText(tempBat, "cd /d \"%~dp0\"\r\njava -Xms1024m -cp * -javaagent:FormGenAgent.jar com.automate.formgen.NewFormGen %*");
            File.WriteAllText(tempJar, "dummy jar");

            Assert.True(FormGenLauncherHelper.IsBatchPatched(tempBat));

            FormGenLauncherHelper.RestoreVanillaBatch(tempBat);

            Assert.False(FormGenLauncherHelper.IsBatchPatched(tempBat));
            Assert.False(File.Exists(tempJar));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void RestoreVanillaBatch_WhenVanillaBackupExists_RestoresPristineBackupAndDeletesBackup()
    {
        var tempPortFile = UsePrivatePortFile();
        var tempDir = Path.GetDirectoryName(tempPortFile)!;
        var tempBat = Path.Combine(tempDir, "NewFormGen.bat");
        var tempVanilla = Path.Combine(tempDir, "NewFormGen.bat.vanilla");

        try
        {
            const string pristineContent = "@echo off\r\njava -Xms1024m -cp * -ea com.automate.formgen.NewFormGen %*";
            File.WriteAllText(tempVanilla, pristineContent);
            File.WriteAllText(tempBat, "cd /d \"%~dp0\"\r\njava -javaagent:FormGenAgent.jar -Xms1024m -cp * -ea com.automate.formgen.NewFormGen %*");

            FormGenLauncherHelper.RestoreVanillaBatch(tempBat);

            Assert.False(File.Exists(tempVanilla));
            var restored = File.ReadAllText(tempBat);
            Assert.Equal(pristineContent, restored);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void RestoreVanillaBatch_WhenJavaAgentBeforeMemoryFlags_ScrubsCleanly()
    {
        var tempPortFile = UsePrivatePortFile();
        var tempDir = Path.GetDirectoryName(tempPortFile)!;
        var tempBat = Path.Combine(tempDir, "NewFormGen.bat");
        var extraJar = Path.Combine(tempDir, "WorkMachine_FormGenAgent.jar");

        try
        {
            File.WriteAllText(tempBat,
                "cd /d \"%~dp0\"\r\n" +
                "if \"%~1\"==\"\" (\r\n" +
                "    java -javaagent:FormGenAgent.jar -Xms1024m -cp * -ea com.automate.formgen.NewFormGen %*\r\n" +
                ") else (\r\n" +
                "    java -javaagent:FormGenAgent.jar -Dformgen.file=\"%~f1\" -Xms1024m -cp * -ea com.automate.formgen.NewFormGen %*\r\n" +
                ")");
            File.WriteAllText(extraJar, "dummy content");

            FormGenLauncherHelper.RestoreVanillaBatch(tempBat);

            var restored = File.ReadAllText(tempBat);
            Assert.DoesNotContain("FormGenAgent", restored);
            Assert.DoesNotContain("-Dformgen.file", restored);
            Assert.DoesNotContain("cd /d \"%~dp0\"", restored);
            Assert.Contains("java -Xms1024m", restored);
            Assert.False(File.Exists(extraJar));
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void UnpatchAndReloadFormGen_PreservesNewFormGenBat_AndRemovesAgentArtifacts()
    {
        var tempPortFile = UsePrivatePortFile();
        var tempDir = Path.GetDirectoryName(tempPortFile)!;
        var tempBat = Path.Combine(tempDir, "NewFormGen.bat");
        var tempVanilla = Path.Combine(tempDir, "NewFormGen.bat.vanilla");
        var tempBak = Path.Combine(tempDir, "NewFormGen.bat.bak");
        var tempJar = Path.Combine(tempDir, "FormGenAgent.jar");

        try
        {
            File.WriteAllText(tempVanilla, "java -Xms1024m -cp * -ea com.automate.formgen.NewFormGen %*");
            File.WriteAllText(tempBak, "backup");
            File.WriteAllText(tempJar, "dummy jar bytes");
            File.WriteAllText(tempBat, "cd /d \"%~dp0\"\r\njava -javaagent:FormGenAgent.jar -Xms1024m -cp * -ea com.automate.formgen.NewFormGen %*");

            bool ok = FormGenLauncherHelper.UnpatchAndReloadFormGen(
                tempBat,
                userSettings: null,
                relaunchFormGen: false);

            Assert.True(ok);
            // NewFormGen.bat MUST still exist and NOT be deleted by wildcard matching
            Assert.True(File.Exists(tempBat), "NewFormGen.bat was accidentally deleted by unpatch!");
            var content = File.ReadAllText(tempBat);
            Assert.DoesNotContain("FormGenAgent", content);
            Assert.Contains("java -Xms1024m", content);

            // Agent JAR and backups must be completely gone
            Assert.False(File.Exists(tempJar), "FormGenAgent.jar should have been deleted!");
            Assert.False(File.Exists(tempVanilla), "NewFormGen.bat.vanilla should have been deleted!");
            Assert.False(File.Exists(tempBak), "NewFormGen.bat.bak should have been deleted!");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void RestoreVanillaBatch_WhenBatDoesNotExist_RecreatesDefaultVanillaBatch()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "FormGenRecreate_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var tempBat = Path.Combine(tempDir, "NewFormGen.bat");

        try
        {
            Assert.False(File.Exists(tempBat));
            FormGenLauncherHelper.RestoreVanillaBatch(tempBat);

            Assert.True(File.Exists(tempBat), "RestoreVanillaBatch did not recreate missing NewFormGen.bat!");
            var content = File.ReadAllText(tempBat);
            Assert.Contains("com.automate.formgen.NewFormGen", content);
            Assert.DoesNotContain("FormGenAgent", content);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void UnpatchAndReloadFormGen_WhenBatWasMissing_RecreatesVanillaBatchAndReturnsTrue()
    {
        var tempPortFile = UsePrivatePortFile();
        var tempDir = Path.GetDirectoryName(tempPortFile)!;
        var tempBat = Path.Combine(tempDir, "NewFormGen.bat");

        try
        {
            Assert.False(File.Exists(tempBat));
            bool ok = FormGenLauncherHelper.UnpatchAndReloadFormGen(
                tempBat,
                userSettings: null,
                relaunchFormGen: false);

            Assert.True(ok);
            Assert.True(File.Exists(tempBat), "UnpatchAndReloadFormGen did not restore missing NewFormGen.bat!");
            var content = File.ReadAllText(tempBat);
            Assert.Contains("com.automate.formgen.NewFormGen", content);
            Assert.DoesNotContain("FormGenAgent", content);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void RestoreVanillaBatch_WhenVanillaBackupIs0Bytes_DiscardsBackupAndRecreatesDefaultVanillaBatch()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "FormGenCorrupt_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var tempBat = Path.Combine(tempDir, "NewFormGen.bat");
        var tempVanilla = Path.Combine(tempDir, "NewFormGen.bat.vanilla");

        try
        {
            // Simulate the exact bug from the work machine: NewFormGen.bat has only "cd /d ...", vanilla is 0 bytes
            File.WriteAllText(tempBat, "cd /d \"%~dp0\"\r\n");
            File.WriteAllBytes(tempVanilla, Array.Empty<byte>());

            FormGenLauncherHelper.RestoreVanillaBatch(tempBat);

            Assert.True(File.Exists(tempBat));
            var content = File.ReadAllText(tempBat);
            Assert.Contains("com.automate.formgen.NewFormGen", content);
            Assert.DoesNotContain("FormGenAgent", content);
            Assert.False(File.Exists(tempVanilla), "0-byte NewFormGen.bat.vanilla was not deleted!");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void EnsureAgentDeployedAndPatched_WhenBatIsEmptyOrCorrupted_InitializesVanillaAndPatchesCleanly()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "FormGenCorruptPatch_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var tempBat = Path.Combine(tempDir, "NewFormGen.bat");
        var tempVanilla = Path.Combine(tempDir, "NewFormGen.bat.vanilla");

        try
        {
            // Empty 0-byte file
            File.WriteAllBytes(tempBat, Array.Empty<byte>());

            FormGenLauncherHelper.EnsureAgentDeployedAndPatched(tempBat);

            Assert.True(File.Exists(tempBat));
            var content = File.ReadAllText(tempBat);
            Assert.Contains("com.automate.formgen.NewFormGen", content);
            Assert.Contains("-javaagent:FormGenAgent.jar", content);
            Assert.Contains("cd /d \"%~dp0\"", content);

            Assert.True(File.Exists(tempVanilla));
            Assert.True(new FileInfo(tempVanilla).Length > 0, "vanilla backup was 0 bytes!");
            var vanillaContent = File.ReadAllText(tempVanilla);
            Assert.Contains("com.automate.formgen.NewFormGen", vanillaContent);
            Assert.DoesNotContain("FormGenAgent", vanillaContent);
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void BrowserIntegrationService_EnsureManifestUpdated_NeverWritesTestHostExe()
    {
        var resolvedExe = BrowserIntegrationService.ResolveDesktopExecutablePath();
        if (resolvedExe != null)
        {
            Assert.EndsWith("AMFormsCST.Desktop.exe", resolvedExe, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("testhost", resolvedExe, StringComparison.OrdinalIgnoreCase);
        }

        var svc = new BrowserIntegrationService();
        var manifestPath = svc.EnsureManifestUpdated();
        if (!string.IsNullOrWhiteSpace(manifestPath) && File.Exists(manifestPath))
        {
            var json = File.ReadAllText(manifestPath);
            Assert.DoesNotContain("testhost.exe", json, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void ReleaseFileLocksFromAgent_WhenNoPortFile_ReturnsFalse()
    {
        _ = UsePrivatePortFile();
        var result = FormGenLauncherHelper.ReleaseFileLocksFromAgent();
        Assert.False(result);
    }

    [Fact]
    public async System.Threading.Tasks.Task ReleaseFileLocksFromAgent_WhenServerRespondsOk_ReturnsTrue()
    {
        var tempPortFile = UsePrivatePortFile();
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;

        File.WriteAllText(tempPortFile, port.ToString());

        var serverTask = System.Threading.Tasks.Task.Run(() =>
        {
            using var client = listener.AcceptTcpClient();
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, new System.Text.UTF8Encoding(false));
            using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false)) { AutoFlush = true };

            var line = reader.ReadLine();
            if (line == "RELEASE_FILE_LOCKS")
            {
                writer.WriteLine("OK:1");
            }
        });

        var result = FormGenLauncherHelper.ReleaseFileLocksFromAgent();
        Assert.True(result);
        await serverTask;
    }

    /// <summary>
    /// Points the launcher helper at a private directory for the current test's async flow, so parallel tests
    /// and background sync calls never share (or delete) the real %TEMP%\formgen_agent.port.
    /// </summary>
    private static string UsePrivatePortFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), "FormGenPort_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        FormGenLauncherHelper.PortFileDirectoryOverride.Value = dir;
        return Path.Combine(dir, "formgen_agent.port");
    }
}
