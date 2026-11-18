using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using SocialWorker.Api.Features.Publishing.Mastodon;
using Xunit;

namespace SocialWorker.Api.Tests.Features.Publishing.Mastodon;

public sealed class MastodonApiClientTests
{
    private sealed class StubHttpHandler : HttpMessageHandler
    {
        public List<(HttpMethod Method, string Path, string? IdempotencyKey, string Body)> Requests { get; } = new();
        public Func<HttpRequestMessage, string, HttpResponseMessage> Handler { get; set; } = (_, _) => new HttpResponseMessage(HttpStatusCode.OK);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content != null ? await request.Content.ReadAsStringAsync(cancellationToken) : string.Empty;
            string? idempotencyKey = request.Headers.TryGetValues("Idempotency-Key", out var vals) ? vals.FirstOrDefault() : null;
            Requests.Add((request.Method, request.RequestUri!.AbsolutePath, idempotencyKey, body));
            return Handler(request, body);
        }
    }

    [Fact]
    public async Task UploadMediaAsync_PollsOn202AcceptedUntilReady()
    {
        int pollCount = 0;
        var handler = new StubHttpHandler
        {
            Handler = (req, _) =>
            {
                if (req.RequestUri!.AbsolutePath == "/api/v2/media")
                {
                    return new HttpResponseMessage(HttpStatusCode.Accepted)
                    {
                        Content = new StringContent("""{"id":"media-1","type":"video","url":null}""", Encoding.UTF8, "application/json")
                    };
                }

                if (req.RequestUri!.AbsolutePath == "/api/v1/media/media-1")
                {
                    pollCount++;
                    if (pollCount == 1)
                    {
                        return new HttpResponseMessage(HttpStatusCode.PartialContent);
                    }

                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("""{"id":"media-1","type":"video","url":"https://mastodon.social/media/1.mp4"}""", Encoding.UTF8, "application/json")
                    };
                }

                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
        };

        var options = Options.Create(new MastodonOptions { MediaPollingIntervalMs = 5, MediaPollingTimeoutSeconds = 5 });
        var client = new MastodonApiClient(new HttpClient(handler), options);

        var res = await client.UploadMediaAsync(
            "https://mastodon.social",
            "tok",
            new byte[] { 1, 2, 3 },
            "clip.mp4",
            "video/mp4",
            "A test video");

        Assert.True(res.Success);
        Assert.Equal("media-1", res.Data!.Id);
        Assert.Equal("https://mastodon.social/media/1.mp4", res.Data.Url);
        Assert.Equal(2, pollCount);
    }

    [Fact]
    public async Task CreateStatusAsync_SendsIdempotencyKeyAndPayload()
    {
        var handler = new StubHttpHandler
        {
            Handler = (_, _) => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"id":"st-1","url":"https://mastodon.social/@alice/st-1","created_at":"2026-01-01T00:00:00Z"}""", Encoding.UTF8, "application/json")
            }
        };

        var client = new MastodonApiClient(new HttpClient(handler), Options.Create(new MastodonOptions()));
        var res = await client.CreateStatusAsync(
            "https://mastodon.social",
            "tok",
            new MastodonStatusRequest("Hello Mastodon!", InReplyToId: "st-0", Visibility: "unlisted"),
            idempotencyKey: "draft-1:1");

        Assert.True(res.Success);
        Assert.Equal("st-1", res.Data!.Id);
        Assert.Single(handler.Requests);
        Assert.Equal("draft-1:1", handler.Requests[0].IdempotencyKey);
        Assert.Contains("\"visibility\":\"unlisted\"", handler.Requests[0].Body);
        Assert.Contains("\"in_reply_to_id\":\"st-0\"", handler.Requests[0].Body);
    }
}
