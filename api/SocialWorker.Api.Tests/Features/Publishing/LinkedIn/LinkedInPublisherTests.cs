using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SocialWorker.Api.Data;
using SocialWorker.Api.Data.Entities;
using SocialWorker.Api.Features.Media;
using SocialWorker.Api.Features.Publishing;
using SocialWorker.Api.Features.Publishing.LinkedIn;
using SocialWorker.Api.Infrastructure.Security;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Publishing.LinkedIn;

public sealed class LinkedInPublisherTests : SqliteTestBase
{
    private static AppDbContext CreateFreshDb(DbContextOptions<AppDbContext> options)
    {
        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private static readonly string ValidKey = "6Hu0Ff4LtNcJDESsBHL40zKqhfOoAVKURp+8jAwZQLw=";

    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _sender;

        public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> sender)
        {
            _sender = sender;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_sender(request));
        }
    }

    [Fact]
    public async Task PublishAsync_DecryptsCredentialsAndCallsService()
    {
        using var db = CreateFreshDb(Options);
        var configData = new Dictionary<string, string?> { { "Auth:DbEncryptionKey", ValidKey } };
        var config = new ConfigurationBuilder().AddInMemoryCollection(configData).Build();

        var encryptedToken = CryptoHelper.EncryptString("my-decrypted-token", ValidKey);
        var account = new Account
        {
            Handle = "urn:li:person:123",
            CredentialsEncrypted = encryptedToken
        };

        var thread = new PlatformThread
        {
            Id = Guid.NewGuid(),
            DraftId = Guid.NewGuid(),
            Content = "Hello from publisher test!"
        };

        var handler = new MockHttpMessageHandler(req =>
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"id\":\"urn:li:share:abc\"}", System.Text.Encoding.UTF8, "application/json")
            };
        });
        var client = new HttpClient(handler);
        var apiClient = new LinkedInApiClient(client);
        var storage = new FileStorageProvider();
        var publishService = new LinkedInPublishService(db, storage, apiClient);
        var publisher = new LinkedInPublisher(publishService, config);

        var result = await publisher.PublishAsync(thread, account, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.Posts);
        Assert.Equal("urn:li:share:abc", result.Posts[0].RemoteId);
    }

    [Fact]
    public async Task PublishAsync_WithOrganizationHandle_ExtractsOrganizationUrn()
    {
        using var db = CreateFreshDb(Options);
        var configData = new Dictionary<string, string?> { { "Auth:DbEncryptionKey", ValidKey } };
        var config = new ConfigurationBuilder().AddInMemoryCollection(configData).Build();

        var encryptedToken = CryptoHelper.EncryptString("my-decrypted-token", ValidKey);
        var account = new Account
        {
            Handle = "Acme Global (urn:li:organization:998877)",
            CredentialsEncrypted = encryptedToken
        };

        var thread = new PlatformThread
        {
            Id = Guid.NewGuid(),
            DraftId = Guid.NewGuid(),
            Content = "Company announcement!"
        };

        string? capturedAuthor = null;
        var handler = new MockHttpMessageHandler(req =>
        {
            if (req.RequestUri!.AbsoluteUri.Contains("ugcPosts"))
            {
                var body = req.Content!.ReadAsStringAsync().Result;
                var json = System.Text.Json.JsonDocument.Parse(body);
                capturedAuthor = json.RootElement.GetProperty("author").GetString();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"id\":\"urn:li:ugcPost:org123\"}", System.Text.Encoding.UTF8, "application/json")
                };
            }
            return new HttpResponseMessage(HttpStatusCode.BadRequest);
        });

        var client = new HttpClient(handler);
        var apiClient = new LinkedInApiClient(client);
        var storage = new FileStorageProvider();
        var publishService = new LinkedInPublishService(db, storage, apiClient);
        var publisher = new LinkedInPublisher(publishService, config);

        var result = await publisher.PublishAsync(thread, account, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("urn:li:organization:998877", capturedAuthor);
        Assert.Equal("urn:li:ugcPost:org123", result.Posts[0].RemoteId);
    }
}
