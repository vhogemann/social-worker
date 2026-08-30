using System;
using System.Collections.Concurrent;
using System.Threading.Channels;
using SocialWorker.Api.Features.Mcp.Models;

namespace SocialWorker.Api.Features.Mcp.Services;

public sealed class McpSession
{
    public string SessionId { get; }
    public Guid UserId { get; }
    public Channel<McpResponse> OutgoingChannel { get; }
    public DateTime CreatedAt { get; } = DateTime.UtcNow;

    public McpSession(string sessionId, Guid userId)
    {
        SessionId = sessionId;
        UserId = userId;
        OutgoingChannel = Channel.CreateUnbounded<McpResponse>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
    }
}

public sealed class McpSessionManager
{
    private readonly ConcurrentDictionary<string, McpSession> _sessions = new();

    public McpSession CreateSession(Guid userId)
    {
        var sessionId = Guid.NewGuid().ToString("N");
        var session = new McpSession(sessionId, userId);
        _sessions[sessionId] = session;
        return session;
    }

    public McpSession? GetSession(string sessionId)
    {
        return _sessions.TryGetValue(sessionId, out var session) ? session : null;
    }

    public void RemoveSession(string sessionId)
    {
        if (_sessions.TryRemove(sessionId, out var session))
        {
            session.OutgoingChannel.Writer.TryComplete();
        }
    }
}
