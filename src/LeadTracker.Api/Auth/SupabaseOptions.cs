namespace LeadTracker.Api.Auth;

/// <summary>
/// Bound from the "Supabase" section of configuration / user-secrets.
/// </summary>
public sealed class SupabaseOptions
{
    /// <summary>e.g. https://abcdefgh.supabase.co (no trailing slash)</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Legacy HS256 shared secret (Dashboard → Project Settings → API → JWT Secret).
    /// Leave empty if your project uses asymmetric JWT signing keys (recommended),
    /// in which case the JWKS endpoint is used instead.
    /// </summary>
    public string? JwtSecret { get; set; }

    /// <summary>
    /// Supabase service-role key (Dashboard → Project Settings → API). Secret-only —
    /// set via user-secrets / env vars, never in appsettings. The API uses this to talk
    /// to PostgREST over HTTPS; tenant scoping is enforced in C#, not by RLS.
    /// </summary>
    public string? ServiceRoleKey { get; set; }

    public string Issuer => $"{Url.TrimEnd('/')}/auth/v1";
    public string JwksUri => $"{Url.TrimEnd('/')}/auth/v1/.well-known/jwks.json";

    /// <summary>PostgREST data API base, e.g. https://abc.supabase.co/rest/v1</summary>
    public string RestUrl => $"{Url.TrimEnd('/')}/rest/v1";

    /// <summary>Supabase Auth always sets aud="authenticated" for logged-in users.</summary>
    public const string Audience = "authenticated";
}
