using System;
using System.Text.Json.Serialization;

namespace SocialWorker.Api.Features.Mcp.Auth;

public sealed record ProtectedResourceMetadataResponse(
    [property: JsonPropertyName("resource")] string Resource,
    [property: JsonPropertyName("authorization_servers")] string[] AuthorizationServers,
    [property: JsonPropertyName("scopes_supported")] string[] ScopesSupported,
    [property: JsonPropertyName("bearer_methods_supported")] string[] BearerMethodsSupported,
    [property: JsonPropertyName("resource_documentation")] string? ResourceDocumentation = null);

public sealed record AuthorizationServerMetadataResponse(
    [property: JsonPropertyName("issuer")] string Issuer,
    [property: JsonPropertyName("authorization_endpoint")] string AuthorizationEndpoint,
    [property: JsonPropertyName("token_endpoint")] string TokenEndpoint,
    [property: JsonPropertyName("registration_endpoint")] string RegistrationEndpoint,
    [property: JsonPropertyName("jwks_uri")] string JwksUri,
    [property: JsonPropertyName("response_types_supported")] string[] ResponseTypesSupported,
    [property: JsonPropertyName("grant_types_supported")] string[] GrantTypesSupported,
    [property: JsonPropertyName("code_challenge_methods_supported")] string[] CodeChallengeMethodsSupported,
    [property: JsonPropertyName("token_endpoint_auth_methods_supported")] string[] TokenEndpointAuthMethodsSupported,
    [property: JsonPropertyName("scopes_supported")] string[] ScopesSupported,
    [property: JsonPropertyName("response_modes_supported")] string[]? ResponseModesSupported = null,
    [property: JsonPropertyName("subject_types_supported")] string[]? SubjectTypesSupported = null,
    [property: JsonPropertyName("id_token_signing_alg_values_supported")] string[]? IdTokenSigningAlgValuesSupported = null);

public sealed record ClientRegistrationRequest(
    [property: JsonPropertyName("client_name")] string? ClientName = null,
    [property: JsonPropertyName("redirect_uris")] string[]? RedirectUris = null,
    [property: JsonPropertyName("grant_types")] string[]? GrantTypes = null,
    [property: JsonPropertyName("response_types")] string[]? ResponseTypes = null,
    [property: JsonPropertyName("token_endpoint_auth_method")] string? TokenEndpointAuthMethod = null,
    [property: JsonPropertyName("scope")] string? Scope = null);

public sealed record ClientRegistrationResponse(
    [property: JsonPropertyName("client_id")] string ClientId,
    [property: JsonPropertyName("client_name")] string ClientName,
    [property: JsonPropertyName("redirect_uris")] string[] RedirectUris,
    [property: JsonPropertyName("grant_types")] string[] GrantTypes,
    [property: JsonPropertyName("response_types")] string[] ResponseTypes,
    [property: JsonPropertyName("token_endpoint_auth_method")] string TokenEndpointAuthMethod,
    [property: JsonPropertyName("client_secret")] string? ClientSecret = null,
    [property: JsonPropertyName("client_secret_expires_at")] long ClientSecretExpiresAt = 0,
    [property: JsonPropertyName("registration_client_uri"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? RegistrationClientUri = null,
    [property: JsonPropertyName("registration_access_token"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? RegistrationAccessToken = null,
    [property: JsonPropertyName("client_id_issued_at"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? ClientIdIssuedAt = null,
    [property: JsonPropertyName("scope"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Scope = null);

public sealed record OAuthTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("token_type")] string TokenType,
    [property: JsonPropertyName("expires_in")] int ExpiresIn,
    [property: JsonPropertyName("refresh_token")] string? RefreshToken = null,
    [property: JsonPropertyName("scope")] string? Scope = null);

public sealed record OAuthErrorResponse(
    [property: JsonPropertyName("error")] string Error,
    [property: JsonPropertyName("error_description")] string? ErrorDescription = null);

public sealed record OAuthClientInfo(
    string ClientId,
    string ClientName,
    string[] RedirectUris,
    DateTime CreatedAt,
    string? ClientSecret = null);

public sealed record AuthorizationCodeEntry(
    string Code,
    string ClientId,
    string RedirectUri,
    string CodeChallenge,
    string CodeChallengeMethod,
    Guid UserId,
    string Scope,
    DateTime ExpiresAt);
