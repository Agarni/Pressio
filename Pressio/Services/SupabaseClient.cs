using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Pressio.Services;

public sealed record AuthResult(bool Success, string? Error, bool NeedsEmailConfirmation = false);

/// <summary>Cliente Supabase (Auth e-mail+senha + snapshot de sync via PostgREST). Credenciais ficam em Settings, fora do Git.</summary>
public sealed class SupabaseClient
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private string _url = string.Empty;
    private string _anonKey = string.Empty;

    public string? AccessToken { get; private set; }
    public string? RefreshToken { get; private set; }
    public string? UserId { get; private set; }
    public string? Email { get; private set; }

    public void Configure(string url, string anonKey)
    {
        _url = url.TrimEnd('/');
        _anonKey = anonKey.Trim();
    }

    public bool IsConfigured => _url.Length > 0 && _anonKey.Length > 0;
    public bool IsAuthenticated => !string.IsNullOrEmpty(AccessToken) && !string.IsNullOrEmpty(UserId);

    public void ClearSession() { AccessToken = RefreshToken = UserId = Email = null; }

    public string SerializeSession() => IsAuthenticated ? $"{AccessToken}|{RefreshToken}|{UserId}|{Email}" : string.Empty;

    public void RestoreSession(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload)) return;
        var parts = payload.Split('|');
        if (parts.Length < 3) return;
        AccessToken = parts[0];
        RefreshToken = parts.Length > 1 && parts[1].Length > 0 ? parts[1] : null;
        UserId = parts[2];
        Email = parts.Length > 3 ? parts[3] : null;
    }

    public async Task<AuthResult> RefreshAsync()
    {
        if (string.IsNullOrEmpty(RefreshToken)) return new(false, "Sessão expirada. Entre novamente.");
        using var request = NewRequest(HttpMethod.Post, "/auth/v1/token?grant_type=refresh_token", authed: false);
        request.Content = new StringContent(JsonSerializer.Serialize(new { refresh_token = RefreshToken }), Encoding.UTF8, "application/json");
        using var response = await _http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return new(false, await ReadErrorAsync(response));
        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync()) as JsonObject;
        var token = json?["access_token"]?.GetValue<string>();
        if (string.IsNullOrEmpty(token)) return new(false, "Não foi possível renovar a sessão.");
        SetSession(token, json?["refresh_token"]?.GetValue<string>() ?? RefreshToken, json?["user"]?["id"]?.GetValue<string>() ?? UserId);
        Email ??= json?["user"]?["email"]?.GetValue<string>();
        return new(true, null);
    }

    private HttpRequestMessage NewRequest(HttpMethod method, string path, bool authed)
    {
        var request = new HttpRequestMessage(method, _url + path);
        request.Headers.Add("apikey", _anonKey);
        if (authed && AccessToken is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
        return request;
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync();
            var node = JsonNode.Parse(body);
            if (node is JsonObject obj && obj["message"] is JsonValue msg) return msg.GetValue<string>() ?? response.ReasonPhrase ?? "Erro";
        }
        catch { }
        return response.ReasonPhrase ?? "Erro desconhecido";
    }

    public async Task<AuthResult> SignUpAsync(string email, string password)
    {
        if (!IsConfigured) return new(false, "Configure a URL e a chave do Supabase em Configurações.");
        using var request = NewRequest(HttpMethod.Post, "/auth/v1/signup", authed: false);
        request.Content = new StringContent(JsonSerializer.Serialize(new { email, password }), Encoding.UTF8, "application/json");
        using var response = await _http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return new(false, await ReadErrorAsync(response));

        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync()) as JsonObject;
        if (json is null) return new(false, "Resposta inválida do servidor.");
        // Com confirmação de e-mail ativa, não vem access_token -> aguardar confirmação.
        var token = json["access_token"]?.GetValue<string>();
        if (string.IsNullOrEmpty(token)) return new(true, "Conta criada. Confirme seu e-mail para entrar.", NeedsEmailConfirmation: true);

        Email = json["user"]?["email"]?.GetValue<string>() ?? email;
        SetSession(token, json["refresh_token"]?.GetValue<string>(), json["user"]?["id"]?.GetValue<string>());
        return new(true, null);
    }

    public async Task<AuthResult> SignInAsync(string email, string password)
    {
        if (!IsConfigured) return new(false, "Configure a URL e a chave do Supabase em Configurações.");
        using var request = NewRequest(HttpMethod.Post, "/auth/v1/token?grant_type=password", authed: false);
        request.Content = new StringContent(JsonSerializer.Serialize(new { email, password }), Encoding.UTF8, "application/json");
        using var response = await _http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return new(false, await ReadErrorAsync(response));

        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync()) as JsonObject;
        var token = json?["access_token"]?.GetValue<string>();
        if (string.IsNullOrEmpty(token)) return new(false, "Não foi possível entrar.");
        Email = json?["user"]?["email"]?.GetValue<string>() ?? email;
        SetSession(token, json?["refresh_token"]?.GetValue<string>(), json?["user"]?["id"]?.GetValue<string>());
        return new(true, null);
    }

    private void SetSession(string access, string? refresh, string? userId)
    {
        AccessToken = access;
        RefreshToken = refresh;
        UserId = userId;
    }

    public async Task<string?> FetchSnapshotAsync()
    {
        if (!IsAuthenticated) return null;
        using var request = NewRequest(HttpMethod.Get, $"/rest/v1/pressio_sync?select=snapshot&user_id=eq.{UserId}", authed: true);
        using var response = await _http.SendAsync(request);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException(await ReadErrorAsync(response));
        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync()) as JsonArray;
        if (json is null || json.Count == 0) return null;
        return json[0]?["snapshot"]?.GetValue<string>();
    }

    public async Task SaveSnapshotAsync(string snapshot)
    {
        if (!IsAuthenticated) return;
        using var request = NewRequest(HttpMethod.Post, "/rest/v1/pressio_sync", authed: true);
        request.Content = new StringContent(JsonSerializer.Serialize(new[] { new { user_id = UserId, snapshot, updated_at = DateTime.UtcNow.ToString("O") } }), Encoding.UTF8, "application/json");
        request.Headers.Add("Prefer", "resolution=merge-duplicates");
        using var response = await _http.SendAsync(request);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException(await ReadErrorAsync(response));
    }
}
