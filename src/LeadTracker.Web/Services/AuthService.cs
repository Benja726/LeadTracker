using Supabase.Gotrue;
using Supabase.Gotrue.Exceptions;

namespace LeadTracker.Web.Services;

/// <summary>
/// Thin wrapper over the GoTrue client. The SDK owns the session: it persists it to
/// localStorage and auto-refreshes the access token in the background, so the user stays
/// logged in until they explicitly sign out (no more "401 after ~1h" from a dropped
/// refresh token).
/// </summary>
public class AuthService(Client client)
{
    public async Task<bool> SignInAsync(string email, string password)
    {
        try
        {
            var session = await client.SignInWithPassword(email, password);
            return session is not null;
        }
        catch (GotrueException)
        {
            // Bad credentials / unconfirmed user etc. — surface as a normal login failure
            // instead of an unhandled render exception.
            return false;
        }
    }

    // The SDK refreshes CurrentSession in the background; this hands ApiService a live token.
    public Task<string?> GetTokenAsync()
        => Task.FromResult(client.CurrentSession?.AccessToken);

    public Task<bool> IsAuthenticatedAsync()
    {
        var session = client.CurrentSession;
        return Task.FromResult(session is not null && !session.Expired());
    }

    public Task SignOutAsync() => client.SignOut();
}
