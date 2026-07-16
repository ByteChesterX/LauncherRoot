using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace LauncherRoot.Services;

public class ElybyAuthResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string Username { get; set; } = "";
    public string Uuid { get; set; } = "";
    public string AccessToken { get; set; } = "";
    public string AuthType { get; set; } = "elyby";
}

public class ElybyAuthService : IDisposable
{
    private readonly HttpClient _http;
    private readonly ConfigService _config;

    private const string AuthorizeUrl = "https://account.ely.by/oauth2/v1";
    private const string TokenUrl = "https://account.ely.by/api/oauth2/v1/token";
    private const string UserInfoUrl = "https://account.ely.by/api/account/v1/info";

    private static string DefaultClientId { get; set; } = "elyprism-launcher";

    public void SetClientId(string clientId)
    {
        if (!string.IsNullOrWhiteSpace(clientId))
            DefaultClientId = clientId.Trim();
    }

    private string ClientIdToUse => DefaultClientId;

    public ElybyAuthService(ConfigService config)
    {
        _config = config;
        _http = new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("LauncherRoot/1.0");
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<ElybyAuthResult> LoginWithBrowserAsync()
    {
        try
        {
            using var server = new LocalAuthServer();
            server.Start();

            var redirectUri = Uri.EscapeDataString(server.RedirectUri);
            var scope = Uri.EscapeDataString("account_info offline_access minecraft_server_session");
            var authUrl = $"{AuthorizeUrl}?client_id={ClientIdToUse}&response_type=code&redirect_uri={redirectUri}&scope={scope}";

            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = authUrl,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch
            {
                return new ElybyAuthResult { Success = false, ErrorMessage = "Tarayıcı açılamadı." };
            }

            var code = await server.WaitForCodeAsync();
            if (string.IsNullOrEmpty(code))
                return new ElybyAuthResult { Success = false, ErrorMessage = "Giriş zaman aşımına uğradı veya iptal edildi." };

            using var tokenContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", ClientIdToUse),
                new KeyValuePair<string, string>("grant_type", "authorization_code"),
                new KeyValuePair<string, string>("code", code),
                new KeyValuePair<string, string>("redirect_uri", server.RedirectUri),
            });
            var tokenResp = await _http.PostAsync(TokenUrl, tokenContent).ConfigureAwait(false);

            if (!tokenResp.IsSuccessStatusCode)
            {
                var body = await tokenResp.Content.ReadAsStringAsync().ConfigureAwait(false);
                _config.Log($"Ely.by token hatası ({tokenResp.StatusCode}): {body}");
                return new ElybyAuthResult { Success = false, ErrorMessage = "Ely.by oturumu açılamadı. Client ID'yi kontrol et." };
            }

            var tokenJson = await tokenResp.Content.ReadFromJsonAsync<JsonElement>().ConfigureAwait(false);
            var accessToken = tokenJson.GetProperty("access_token").GetString()!;

            var userReq = new HttpRequestMessage(HttpMethod.Get, UserInfoUrl);
            userReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            var userResp = await _http.SendAsync(userReq).ConfigureAwait(false);

            if (userResp.IsSuccessStatusCode)
            {
                var userJson = await userResp.Content.ReadFromJsonAsync<JsonElement>().ConfigureAwait(false);
                var userName = userJson.GetProperty("username").GetString()!;
                var uuid = userJson.TryGetProperty("uuid", out var u) ? u.GetString() : null;

                _config.Log($"Ely.by girişi başarılı: {userName}");

                return new ElybyAuthResult
                {
                    Success = true,
                    Username = userName,
                    Uuid = uuid ?? Guid.NewGuid().ToString("N"),
                    AccessToken = accessToken,
                    AuthType = "elyby"
                };
            }

            var userBody = await userResp.Content.ReadAsStringAsync().ConfigureAwait(false);
            _config.Log($"Ely.by kullanıcı bilgisi hatası ({userResp.StatusCode}): {userBody}");

            return new ElybyAuthResult { Success = false, ErrorMessage = "Ely.by kullanıcı bilgisi alınamadı." };
        }
        catch (HttpRequestException httpEx)
        {
            _config.Log($"Ely.by bağlantı hatası: {httpEx.Message}");
            return new ElybyAuthResult { Success = false, ErrorMessage = "Sunucuya bağlanılamadı." };
        }
        catch (TaskCanceledException)
        {
            _config.Log("Ely.by zaman aşımı.");
            return new ElybyAuthResult { Success = false, ErrorMessage = "Bağlantı zaman aşımına uğradı." };
        }
        catch (Exception ex)
        {
            _config.Log($"Ely.by giriş hatası: {ex.Message}");
            return new ElybyAuthResult { Success = false, ErrorMessage = $"Beklenmeyen hata: {ex.Message}" };
        }
    }

    public void Dispose()
    {
        _http?.Dispose();
        GC.SuppressFinalize(this);
    }
}
