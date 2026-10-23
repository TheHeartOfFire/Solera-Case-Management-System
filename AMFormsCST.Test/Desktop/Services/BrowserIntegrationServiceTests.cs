using System.Security.Cryptography;
using System.Text;
using AMFormsCST.Desktop.Services;
using Xunit;

namespace AMFormsCST.Test.Desktop.Services;

public class BrowserIntegrationServiceTests
{
    [Fact]
    public void GenerateDeterministicKey_ComputesValidChromiumExtensionId()
    {
        using var rsa = RSA.Create(2048);
        var pubKeyBytes = rsa.ExportSubjectPublicKeyInfo();
        var pubKeyBase64 = Convert.ToBase64String(pubKeyBytes);

        var hash = SHA256.HashData(pubKeyBytes);
        var idChars = new char[32];
        const string alphabet = "abcdefghijklmnop";

        for (int i = 0; i < 16; i++)
        {
            byte b = hash[i];
            idChars[i * 2] = alphabet[(b >> 4) & 0x0F];
            idChars[i * 2 + 1] = alphabet[b & 0x0F];
        }

        var extensionId = new string(idChars);

        Assert.Equal(32, extensionId.Length);
        Assert.Matches("^[a-p]{32}$", extensionId);
        Assert.False(string.IsNullOrWhiteSpace(pubKeyBase64));
    }

    [Fact]
    public void ExtensionId_MatchesFixedId()
    {
        var service = new BrowserIntegrationService();
        Assert.Equal("epjedpmecdfdbfalaklkfbjcpacpfhlp", service.ExtensionId);
    }

    [Fact]
    public void RegisterAndCheck_ReturnsTrue()
    {
        var service = new BrowserIntegrationService();
        service.RegisterNativeMessagingHost();
        Assert.True(service.IsRegistered());
    }

    [Fact]
    public void GetExtensionDirectory_FindsValidDirectory()
    {
        var dir = BrowserIntegrationService.GetExtensionDirectory();
        Assert.NotNull(dir);
        Assert.True(System.IO.File.Exists(System.IO.Path.Combine(dir, "manifest.json")));
    }
}

