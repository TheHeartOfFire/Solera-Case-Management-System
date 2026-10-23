using System;
using System.IO;
using AMFormsCST.Desktop.Services;
using Xunit;

namespace AMFormsCST.Test.Desktop.Services;

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
}

