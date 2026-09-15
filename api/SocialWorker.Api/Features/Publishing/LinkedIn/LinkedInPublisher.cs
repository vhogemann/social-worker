using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Infrastructure.Security;

namespace SocialWorker.Api.Features.Publishing.LinkedIn;

public class LinkedInPublisher : IPublisher
{
    private readonly LinkedInPublishService _publishService;
    private readonly string _encryptionKey;

    public string Platform => "LinkedIn";

    public LinkedInPublisher(LinkedInPublishService publishService, IConfiguration config)
    {
        _publishService = publishService;
        _encryptionKey = config["Auth:DbEncryptionKey"] ?? "";
    }

    public async Task<PublishResult> PublishAsync(PlatformThread thread, Account account, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_encryptionKey))
        {
            return new PublishResult { Success = false, ErrorMessage = "Server encryption key not configured." };
        }

        string token;
        try
        {
            token = CryptoHelper.DecryptString(account.CredentialsEncrypted, _encryptionKey);
        }
        catch (Exception ex)
        {
            return new PublishResult { Success = false, ErrorMessage = $"Failed to decrypt credentials: {ex.Message}" };
        }

        var authorUrn = ExtractAuthorUrn(account.Handle);

        return await _publishService.PublishAsync(
            content: thread.Content ?? "",
            authorUrn: authorUrn,
            visibility: "PUBLIC",
            draftId: thread.DraftId,
            threadId: thread.Id,
            token: token,
            ct: ct
        );
    }

    public static string ExtractAuthorUrn(string handle)
    {
        if (string.IsNullOrWhiteSpace(handle)) return "";
        var match = System.Text.RegularExpressions.Regex.Match(
            handle,
            @"urn:li:(?:person|organization|organizationBrand):[a-zA-Z0-9_-]+",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (match.Success)
        {
            return match.Value;
        }

        return handle.Trim();
    }
}
