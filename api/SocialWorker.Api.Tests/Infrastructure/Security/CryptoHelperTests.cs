using System;
using SocialWorker.Api.Infrastructure.Security;
using Xunit;

namespace SocialWorker.Api.Tests;

public sealed class CryptoHelperTests
{
    private const string ValidKey = "6Hu0Ff4LtNcJDESsBHL40zKqhfOoAVKURp+8jAwZQLw=";

    [Fact]
    public void EncryptDecrypt_RoundTrip_ReturnsOriginal()
    {
        var original = "my-super-secret-app-password";
        var encrypted = CryptoHelper.EncryptString(original, ValidKey);
        var decrypted = CryptoHelper.DecryptString(encrypted, ValidKey);
        Assert.Equal(original, decrypted);
    }

    [Fact]
    public void EncryptString_EmptyInput_ReturnsEmpty()
    {
        var result = CryptoHelper.EncryptString("", ValidKey);
        Assert.Equal("", result);
    }

    [Fact]
    public void DecryptString_EmptyInput_ReturnsEmpty()
    {
        var result = CryptoHelper.DecryptString("", ValidKey);
        Assert.Equal("", result);
    }

    [Fact]
    public void Encrypt_Produces_Different_Output_Each_Time()
    {
        var a = CryptoHelper.EncryptString("same text", ValidKey);
        var b = CryptoHelper.EncryptString("same text", ValidKey);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Decrypt_WrongKey_Throws()
    {
        var encrypted = CryptoHelper.EncryptString("secret", ValidKey);
        Assert.ThrowsAny<Exception>(() => CryptoHelper.DecryptString(encrypted, "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA="));
    }
}