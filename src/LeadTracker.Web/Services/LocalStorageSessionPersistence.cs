using Microsoft.JSInterop;
using Newtonsoft.Json;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;

namespace LeadTracker.Web.Services;

/// <summary>
/// Persists the GoTrue session to localStorage so it survives page reloads. Uses
/// synchronous in-process JS interop, which is valid in single-threaded Blazor WASM.
/// Serialized with Newtonsoft to match how the GoTrue SDK models the Session type.
/// </summary>
public sealed class LocalStorageSessionPersistence : IGotrueSessionPersistence<Session>
{
    private const string Key = "sb_session";
    private readonly IJSInProcessRuntime _js;

    public LocalStorageSessionPersistence(IJSRuntime js)
        => _js = (IJSInProcessRuntime)js;

    public void SaveSession(Session session)
        => _js.InvokeVoid("localStorage.setItem", Key, JsonConvert.SerializeObject(session));

    public void DestroySession()
        => _js.InvokeVoid("localStorage.removeItem", Key);

    public Session? LoadSession()
    {
        var json = _js.Invoke<string?>("localStorage.getItem", Key);
        return string.IsNullOrEmpty(json) ? null : JsonConvert.DeserializeObject<Session>(json);
    }
}
