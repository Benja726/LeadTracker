using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace LeadTracker.Api.Auth;

/// <summary>
/// Fetches the public signing keys from Supabase's JWKS endpoint
/// (https://&lt;project-ref&gt;.supabase.co/auth/v1/.well-known/jwks.json).
/// Used for asymmetric (ES256/RS256) tokens, which is Supabase's recommended/default mode.
/// Works without an OpenID discovery document, and ConfigurationManager handles key rotation.
/// </summary>
public sealed class SupabaseJwksRetriever : IConfigurationRetriever<OpenIdConnectConfiguration>
{
    public async Task<OpenIdConnectConfiguration> GetConfigurationAsync(
        string address, IDocumentRetriever retriever, CancellationToken cancel)
    {
        var json = await retriever.GetDocumentAsync(address, cancel);
        var jwks = new JsonWebKeySet(json);

        var config = new OpenIdConnectConfiguration();
        foreach (var key in jwks.GetSigningKeys())
        {
            config.SigningKeys.Add(key);
        }
        return config;
    }
}
