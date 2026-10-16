using System;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SocialWorker.Api.Data;
using SocialWorker.Api.Features.Auth;

namespace SocialWorker.Api.Features.Mcp.Auth;

public static class McpOAuthEndpoints
{
    public static void MapMcpOAuthEndpoints(this WebApplication app)
    {
        // 1. RFC 9728 Protected Resource Metadata
        app.MapGet("/.well-known/oauth-protected-resource", (HttpContext ctx) =>
        {
            var baseUrl = GetBaseUrl(ctx);
            var metadata = new ProtectedResourceMetadataResponse(
                Resource: $"{baseUrl}/api/mcp",
                AuthorizationServers: new[] { baseUrl },
                ScopesSupported: new[] { "mcp:tools", "mcp:read", "mcp:write" },
                BearerMethodsSupported: new[] { "header" },
                ResourceDocumentation: $"{baseUrl}/docs/mcp");

            return Results.Ok(metadata);
        }).AllowAnonymous();

        app.MapGet("/api/mcp/.well-known/oauth-protected-resource", (HttpContext ctx) =>
        {
            var baseUrl = GetBaseUrl(ctx);
            var metadata = new ProtectedResourceMetadataResponse(
                Resource: $"{baseUrl}/api/mcp",
                AuthorizationServers: new[] { baseUrl },
                ScopesSupported: new[] { "mcp:tools", "mcp:read", "mcp:write" },
                BearerMethodsSupported: new[] { "header" },
                ResourceDocumentation: $"{baseUrl}/docs/mcp");

            return Results.Ok(metadata);
        }).AllowAnonymous();

        // 2. RFC 8414 Authorization Server Metadata
        app.MapGet("/.well-known/oauth-authorization-server", (HttpContext ctx) =>
        {
            var baseUrl = GetBaseUrl(ctx);
            var metadata = new AuthorizationServerMetadataResponse(
                Issuer: baseUrl,
                AuthorizationEndpoint: $"{baseUrl}/oauth/authorize",
                TokenEndpoint: $"{baseUrl}/oauth/token",
                RegistrationEndpoint: $"{baseUrl}/oauth/register",
                ResponseTypesSupported: new[] { "code" },
                GrantTypesSupported: new[] { "authorization_code", "refresh_token" },
                CodeChallengeMethodsSupported: new[] { "S256", "plain" },
                TokenEndpointAuthMethodsSupported: new[] { "none", "client_secret_post", "client_secret_basic" },
                ScopesSupported: new[] { "mcp:tools", "mcp:read", "mcp:write" });

            return Results.Ok(metadata);
        }).AllowAnonymous();

        app.MapGet("/.well-known/openid-configuration", (HttpContext ctx) =>
        {
            var baseUrl = GetBaseUrl(ctx);
            var metadata = new AuthorizationServerMetadataResponse(
                Issuer: baseUrl,
                AuthorizationEndpoint: $"{baseUrl}/oauth/authorize",
                TokenEndpoint: $"{baseUrl}/oauth/token",
                RegistrationEndpoint: $"{baseUrl}/oauth/register",
                ResponseTypesSupported: new[] { "code" },
                GrantTypesSupported: new[] { "authorization_code", "refresh_token" },
                CodeChallengeMethodsSupported: new[] { "S256", "plain" },
                TokenEndpointAuthMethodsSupported: new[] { "none", "client_secret_post", "client_secret_basic" },
                ScopesSupported: new[] { "mcp:tools", "mcp:read", "mcp:write" });

            return Results.Ok(metadata);
        }).AllowAnonymous();

        // 3. RFC 7591 Dynamic Client Registration (DCR)
        app.MapPost("/oauth/register", async (HttpContext ctx, McpOAuthStore store) =>
        {
            ClientRegistrationRequest? req = null;
            try
            {
                req = await ctx.Request.ReadFromJsonAsync<ClientRegistrationRequest>();
            }
            catch { }

            var client = store.RegisterClient(req?.ClientName, req?.RedirectUris);
            var response = new ClientRegistrationResponse(
                ClientId: client.ClientId,
                ClientName: client.ClientName,
                RedirectUris: client.RedirectUris,
                GrantTypes: new[] { "authorization_code", "refresh_token" },
                ResponseTypes: new[] { "code" },
                TokenEndpointAuthMethod: "none",
                ClientIdIssuedAt: DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            return Results.Created($"/oauth/register/{client.ClientId}", response);
        }).AllowAnonymous();

        // 4. OAuth 2.1 Authorization Code Consent UI
        app.MapGet("/oauth/authorize", (
            HttpContext ctx,
            McpOAuthStore store,
            string? response_type,
            string? client_id,
            string? redirect_uri,
            string? code_challenge,
            string? code_challenge_method,
            string? state,
            string? scope,
            string? error) =>
        {
            if (string.IsNullOrWhiteSpace(client_id) || string.IsNullOrWhiteSpace(redirect_uri))
            {
                return Results.BadRequest(new OAuthErrorResponse("invalid_request", "Missing client_id or redirect_uri."));
            }

            if (!string.Equals(response_type, "code", StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new OAuthErrorResponse("unsupported_response_type", "Only response_type=code is supported."));
            }

            var client = store.GetClient(client_id);
            var clientName = client?.ClientName ?? client_id;

            var html = GenerateConsentHtml(
                clientName: WebUtility.HtmlEncode(clientName),
                clientId: WebUtility.HtmlEncode(client_id),
                redirectUri: WebUtility.HtmlEncode(redirect_uri),
                codeChallenge: WebUtility.HtmlEncode(code_challenge ?? ""),
                codeChallengeMethod: WebUtility.HtmlEncode(code_challenge_method ?? "S256"),
                state: WebUtility.HtmlEncode(state ?? ""),
                scope: WebUtility.HtmlEncode(scope ?? "mcp:tools"),
                errorMessage: error);

            return Results.Content(html, "text/html");
        }).AllowAnonymous();

        // 5. OAuth 2.1 Authorization Decision Processor
        app.MapPost("/oauth/authorize", async (
            HttpContext ctx,
            McpOAuthStore store,
            AppDbContext db,
            AuthService authService,
            CancellationToken ct) =>
        {
            var form = await ctx.Request.ReadFormAsync(ct);
            var action = form["action"].ToString();
            var clientId = form["client_id"].ToString();
            var redirectUri = form["redirect_uri"].ToString();
            var codeChallenge = form["code_challenge"].ToString();
            var codeChallengeMethod = form["code_challenge_method"].ToString();
            var state = form["state"].ToString();
            var scope = form["scope"].ToString();
            var username = form["username"].ToString();
            var password = form["password"].ToString();

            if (string.IsNullOrWhiteSpace(redirectUri))
            {
                return Results.BadRequest(new OAuthErrorResponse("invalid_request", "Missing redirect_uri."));
            }

            var separator = redirectUri.Contains('?') ? "&" : "?";

            if (string.Equals(action, "deny", StringComparison.OrdinalIgnoreCase))
            {
                var stateParam = string.IsNullOrWhiteSpace(state) ? "" : $"&state={WebUtility.UrlEncode(state)}";
                return Results.Redirect($"{redirectUri}{separator}error=access_denied{stateParam}");
            }

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                return RenderAuthPageWithError("Please provide your username/email and password.");
            }

            var loginRes = await authService.LoginAsync(username, password, ct);
            if (loginRes == null)
            {
                return RenderAuthPageWithError("Invalid username or password. Please try again.");
            }

            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == loginRes.User.Id && u.IsActive, ct);
            if (user == null)
            {
                return RenderAuthPageWithError("User account inactive or not found.");
            }

            var code = store.CreateAuthorizationCode(
                clientId: clientId,
                redirectUri: redirectUri,
                codeChallenge: codeChallenge,
                codeChallengeMethod: codeChallengeMethod,
                userId: user.Id,
                scope: scope);

            var codeParam = $"code={WebUtility.UrlEncode(code)}";
            if (!string.IsNullOrWhiteSpace(state))
            {
                codeParam += $"&state={WebUtility.UrlEncode(state)}";
            }

            return Results.Redirect($"{redirectUri}{separator}{codeParam}");

            IResult RenderAuthPageWithError(string message)
            {
                var client = store.GetClient(clientId);
                var clientName = client?.ClientName ?? clientId;
                var html = GenerateConsentHtml(
                    clientName: WebUtility.HtmlEncode(clientName),
                    clientId: WebUtility.HtmlEncode(clientId),
                    redirectUri: WebUtility.HtmlEncode(redirectUri),
                    codeChallenge: WebUtility.HtmlEncode(codeChallenge),
                    codeChallengeMethod: WebUtility.HtmlEncode(codeChallengeMethod),
                    state: WebUtility.HtmlEncode(state),
                    scope: WebUtility.HtmlEncode(scope),
                    errorMessage: message);

                return Results.Content(html, "text/html");
            }
        }).AllowAnonymous();

        // 6. OAuth 2.1 Token Endpoint
        app.MapPost("/oauth/token", async (
            HttpContext ctx,
            McpOAuthService oauthService,
            CancellationToken ct) =>
        {
            string? grantType = null;
            string? code = null;
            string? redirectUri = null;
            string? clientId = null;
            string? codeVerifier = null;
            string? refreshToken = null;

            if (ctx.Request.HasFormContentType)
            {
                var form = await ctx.Request.ReadFormAsync(ct);
                grantType = form["grant_type"].ToString();
                code = form["code"].ToString();
                redirectUri = form["redirect_uri"].ToString();
                clientId = form["client_id"].ToString();
                codeVerifier = form["code_verifier"].ToString();
                refreshToken = form["refresh_token"].ToString();
            }
            else
            {
                try
                {
                    using var doc = await JsonDocument.ParseAsync(ctx.Request.Body, cancellationToken: ct);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("grant_type", out var gt)) grantType = gt.GetString();
                    if (root.TryGetProperty("code", out var cd)) code = cd.GetString();
                    if (root.TryGetProperty("redirect_uri", out var ru)) redirectUri = ru.GetString();
                    if (root.TryGetProperty("client_id", out var ci)) clientId = ci.GetString();
                    if (root.TryGetProperty("code_verifier", out var cv)) codeVerifier = cv.GetString();
                    if (root.TryGetProperty("refresh_token", out var rt)) refreshToken = rt.GetString();
                }
                catch { }
            }

            var baseUrl = GetBaseUrl(ctx);

            if (string.Equals(grantType, "authorization_code", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(redirectUri) ||
                    string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(codeVerifier))
                {
                    return Results.BadRequest(new OAuthErrorResponse("invalid_request", "Missing required authorization_code parameters."));
                }

                var (tokenRes, err, errDesc) = await oauthService.ExchangeAuthorizationCodeAsync(
                    code: code,
                    redirectUri: redirectUri,
                    clientId: clientId,
                    codeVerifier: codeVerifier,
                    baseUrl: baseUrl,
                    ct: ct);

                if (tokenRes == null)
                {
                    return Results.BadRequest(new OAuthErrorResponse(err ?? "invalid_grant", errDesc));
                }

                return Results.Ok(tokenRes);
            }

            if (string.Equals(grantType, "refresh_token", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(refreshToken))
                {
                    return Results.BadRequest(new OAuthErrorResponse("invalid_request", "Missing refresh_token."));
                }

                var (tokenRes, err, errDesc) = await oauthService.RefreshTokenAsync(
                    refreshTokenStr: refreshToken,
                    baseUrl: baseUrl,
                    ct: ct);

                if (tokenRes == null)
                {
                    return Results.BadRequest(new OAuthErrorResponse(err ?? "invalid_grant", errDesc));
                }

                return Results.Ok(tokenRes);
            }

            return Results.BadRequest(new OAuthErrorResponse("unsupported_grant_type", $"Grant type '{grantType}' is not supported."));
        }).AllowAnonymous();
    }

    public static string GetBaseUrl(HttpContext ctx)
    {
        var scheme = ctx.Request.Headers["X-Forwarded-Proto"].ToString();
        if (string.IsNullOrWhiteSpace(scheme))
        {
            scheme = ctx.Request.Scheme;
        }

        var host = ctx.Request.Headers["X-Forwarded-Host"].ToString();
        if (string.IsNullOrWhiteSpace(host))
        {
            host = ctx.Request.Host.Value;
        }

        return $"{scheme}://{host}{ctx.Request.PathBase}";
    }

    private static string GenerateConsentHtml(
        string clientName,
        string clientId,
        string redirectUri,
        string codeChallenge,
        string codeChallengeMethod,
        string state,
        string scope,
        string? errorMessage)
    {
        var errorHtml = string.IsNullOrWhiteSpace(errorMessage)
            ? ""
            : $@"<div class=""error-box"">{WebUtility.HtmlEncode(errorMessage)}</div>";

        var bodyContent = $@"
        <p>An AI client (<span class=""highlight"">{clientName}</span>) is requesting access to your SocialWorker MCP server.</p>
        <p>Please enter your SocialWorker credentials to authorize this connection. Stored browser sessions are not accepted for MCP authorization.</p>

        {errorHtml}

        <form method=""POST"" action=""/oauth/authorize"">
            <input type=""hidden"" name=""client_id"" value=""{clientId}"" />
            <input type=""hidden"" name=""redirect_uri"" value=""{redirectUri}"" />
            <input type=""hidden"" name=""code_challenge"" value=""{codeChallenge}"" />
            <input type=""hidden"" name=""code_challenge_method"" value=""{codeChallengeMethod}"" />
            <input type=""hidden"" name=""state"" value=""{state}"" />
            <input type=""hidden"" name=""scope"" value=""{scope}"" />

            <div class=""form-group"">
                <label for=""username"">Username or Email</label>
                <input type=""text"" id=""username"" name=""username"" required autofocus placeholder=""username or email"" autocomplete=""username"" />
            </div>

            <div class=""form-group"">
                <label for=""password"">Password</label>
                <input type=""password"" id=""password"" name=""password"" required placeholder=""••••••••"" autocomplete=""current-password"" />
            </div>

            <div class=""permissions"">
                <strong>Requested Capabilities:</strong>
                <ul>
                    <li>Full MCP tool & resource access ({scope})</li>
                    <li>Read and edit thread drafts</li>
                    <li>Inspect connected feeds and social workflows</li>
                </ul>
            </div>
            
            <div class=""actions"">
                <button type=""submit"" name=""action"" value=""approve"" class=""btn-approve"">Sign In & Authorize</button>
                <button type=""submit"" name=""action"" value=""deny"" class=""btn-deny"">Deny</button>
            </div>
        </form>";

        return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"" />
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
    <title>Authorize {clientName} - SocialWorker</title>
    <style>
        body {{
            font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, Helvetica, Arial, sans-serif;
            background-color: #0f172a;
            color: #f8fafc;
            display: flex;
            align-items: center;
            justify-content: center;
            min-height: 100vh;
            margin: 0;
            padding: 1rem;
            box-sizing: border-box;
        }}
        .card {{
            background: #1e293b;
            border: 1px solid #334155;
            border-radius: 12px;
            padding: 2rem;
            max-width: 440px;
            width: 100%;
            box-shadow: 0 10px 25px -5px rgba(0, 0, 0, 0.4);
        }}
        .logo {{
            display: inline-block;
            background: #2563eb;
            color: #fff;
            padding: 6px 12px;
            border-radius: 6px;
            font-weight: bold;
            font-size: 0.85rem;
            margin-bottom: 1.25rem;
            letter-spacing: 0.05em;
            text-transform: uppercase;
        }}
        h1 {{
            font-size: 1.25rem;
            margin: 0 0 0.5rem 0;
            color: #ffffff;
        }}
        p {{
            font-size: 0.95rem;
            line-height: 1.5;
            color: #94a3b8;
            margin: 0 0 1.25rem 0;
        }}
        .highlight {{
            color: #38bdf8;
            font-weight: 600;
        }}
        .user-badge {{
            display: flex;
            align-items: center;
            gap: 0.75rem;
            background: #0f172a;
            border: 1px solid #3b82f6;
            border-radius: 8px;
            padding: 0.75rem 1rem;
            margin-bottom: 1.25rem;
        }}
        .user-avatar {{
            font-size: 1.5rem;
        }}
        .user-details {{
            display: flex;
            flex-direction: column;
        }}
        .user-name {{
            font-weight: 600;
            color: #f8fafc;
            font-size: 0.95rem;
        }}
        .user-email {{
            font-size: 0.8rem;
            color: #94a3b8;
        }}
        .form-group {{
            margin-bottom: 1rem;
            text-align: left;
        }}
        label {{
            display: block;
            font-size: 0.85rem;
            font-weight: 500;
            margin-bottom: 0.35rem;
            color: #cbd5e1;
        }}
        input[type=""text""], input[type=""password""] {{
            width: 100%;
            padding: 0.65rem 0.75rem;
            border-radius: 6px;
            border: 1px solid #475569;
            background: #0f172a;
            color: #f8fafc;
            font-size: 0.95rem;
            box-sizing: border-box;
            outline: none;
            transition: border-color 0.2s;
        }}
        input[type=""text""]:focus, input[type=""password""]:focus {{
            border-color: #3b82f6;
        }}
        .error-box {{
            background: rgba(239, 68, 68, 0.15);
            border: 1px solid #ef4444;
            color: #fca5a5;
            padding: 0.75rem;
            border-radius: 6px;
            font-size: 0.85rem;
            margin-bottom: 1.25rem;
        }}
        .permissions {{
            background: #0f172a;
            border-radius: 8px;
            padding: 0.85rem 1rem;
            margin-bottom: 1.5rem;
            font-size: 0.85rem;
            border: 1px solid #334155;
        }}
        .permissions ul {{
            margin: 0.4rem 0 0 1.2rem;
            padding: 0;
            color: #cbd5e1;
        }}
        .actions {{
            display: flex;
            gap: 0.75rem;
        }}
        button {{
            flex: 1;
            padding: 0.75rem 1rem;
            font-size: 0.95rem;
            font-weight: 600;
            border-radius: 8px;
            border: none;
            cursor: pointer;
            transition: all 0.2s;
        }}
        .btn-approve {{
            background: #2563eb;
            color: white;
        }}
        .btn-approve:hover {{
            background: #1d4ed8;
        }}
        .btn-deny {{
            background: #334155;
            color: #cbd5e1;
        }}
        .btn-deny:hover {{
            background: #475569;
            color: white;
        }}
        .switch-wrapper {{
            text-align: center;
            margin-top: 1rem;
        }}
        .switch-link {{
            color: #94a3b8;
            font-size: 0.85rem;
            text-decoration: none;
        }}
        .switch-link:hover {{
            color: #38bdf8;
            text-decoration: underline;
        }}
    </style>
</head>
<body>
    <div class=""card"">
        <div class=""logo"">SocialWorker</div>
        <h1>Authorize <span class=""highlight"">{clientName}</span></h1>
        {bodyContent}
    </div>
</body>
</html>";
    }
}
