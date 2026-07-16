using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace LauncherRoot.Services;

public class MicrosoftAuthResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string Username { get; set; } = "";
    public string Uuid { get; set; } = "";
    public string AccessToken { get; set; } = "";
    public string AuthType { get; set; } = "microsoft";
}

public class MicrosoftAuthService : IDisposable
{
    private readonly HttpClient _http;
    private readonly ConfigService _config;

    private const string AuthorizeUrl = "https://login.microsoftonline.com/consumers/oauth2/v2.0/authorize";
    private const string TokenUrl = "https://login.microsoftonline.com/consumers/oauth2/v2.0/token";
    private const string XboxAuthUrl = "https://user.auth.xboxlive.com/user/authenticate";
    private const string XstsAuthUrl = "https://xsts.auth.xboxlive.com/xsts/authorize";
    private const string MinecraftAuthUrl = "https://api.minecraftservices.com/authentication/login_with_xbox";
    private const string MinecraftProfileUrl = "https://api.minecraftservices.com/minecraft/profile";

    private const string Scope = "XboxLive.signin offline_access";

    private static string DefaultClientId { get; set; } = "c36a9fb6-4f2a-41ff-90bd-ae7cc92031eb";

    public MicrosoftAuthService(ConfigService config)
    {
        _config = config;
        _http = new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("LauncherRoot/1.0");
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    public void SetClientId(string clientId)
    {
        if (!string.IsNullOrWhiteSpace(clientId))
            DefaultClientId = clientId.Trim();
    }

    private string ClientIdToUse => DefaultClientId;

    public async Task<MicrosoftAuthResult> LoginWithBrowserAsync(
        Func<string, Task> updateStatusCallback)
    {
        try
        {
            await updateStatusCallback("microsoft.connecting");

            using var server = new LocalAuthServer();
            server.Start();
            var (verifier, challenge) = PkceHelper.Generate();

            var redirectUri = Uri.EscapeDataString(server.RedirectUri);
            var authUrl = $"{AuthorizeUrl}?client_id={ClientIdToUse}&response_type=code&redirect_uri={redirectUri}&scope={Uri.EscapeDataString(Scope)}&code_challenge={challenge}&code_challenge_method=S256";

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
                return Fail("Tarayıcı açılamadı. Lütfen manuel olarak giriş yap.");
            }

            var code = await server.WaitForCodeAsync();

            if (string.IsNullOrEmpty(code))
                return Fail("Giriş zaman aşımına uğradı veya iptal edildi.");

            using var tokenContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", ClientIdToUse),
                new KeyValuePair<string, string>("grant_type", "authorization_code"),
                new KeyValuePair<string, string>("code", code),
                new KeyValuePair<string, string>("redirect_uri", server.RedirectUri),
                new KeyValuePair<string, string>("code_verifier", verifier),
            });
            var tokenResp = await _http.PostAsync(TokenUrl, tokenContent).ConfigureAwait(false);

            if (!tokenResp.IsSuccessStatusCode)
            {
                var body = await tokenResp.Content.ReadAsStringAsync().ConfigureAwait(false);
                _config.Log($"Microsoft token hatası ({tokenResp.StatusCode}): {body}");
                return Fail("Microsoft oturumu açılamadı. Lütfen tekrar dene.");
            }

            var tokenJson = await tokenResp.Content.ReadFromJsonAsync<JsonElement>().ConfigureAwait(false);
            var microsoftToken = tokenJson.GetProperty("access_token").GetString()!;

            await updateStatusCallback("microsoft.xbox_live");

            var xboxResp = await _http.PostAsync(XboxAuthUrl, JsonContent.Create(new
            {
                Properties = new
                {
                    AuthMethod = "RPS",
                    SiteName = "user.auth.xboxlive.com",
                    RpsTicket = $"d={microsoftToken}"
                },
                RelyingParty = "http://auth.xboxlive.com",
                TokenType = "JWT"
            })).ConfigureAwait(false);

            if (!xboxResp.IsSuccessStatusCode)
            {
                var body = await xboxResp.Content.ReadAsStringAsync().ConfigureAwait(false);
                _config.Log($"Xbox Live hatası ({xboxResp.StatusCode}): {body}");
                return Fail("Xbox Live doğrulaması başarısız. Microsoft hesabınla tekrar dene.");
            }

            var xboxJson = await xboxResp.Content.ReadFromJsonAsync<JsonElement>().ConfigureAwait(false);
            var xboxToken = xboxJson.GetProperty("Token").GetString()!;

            await updateStatusCallback("microsoft.xsts");

            var xstsResp = await _http.PostAsync(XstsAuthUrl, JsonContent.Create(new
            {
                Properties = new
                {
                    SandboxId = "RETAIL",
                    UserTokens = new[] { xboxToken }
                },
                RelyingParty = "rp://api.minecraftservices.com/",
                TokenType = "JWT"
            })).ConfigureAwait(false);

            if (!xstsResp.IsSuccessStatusCode)
            {
                var body = await xstsResp.Content.ReadAsStringAsync().ConfigureAwait(false);
                _config.Log($"XSTS hatası ({xstsResp.StatusCode}): {body}");
                return Fail("XSTS doğrulaması başarısız. Microsoft hesabınla tekrar dene.");
            }

            var xstsJson = await xstsResp.Content.ReadFromJsonAsync<JsonElement>().ConfigureAwait(false);
            var xstsToken = xstsJson.GetProperty("Token").GetString()!;
            var userHash = xstsJson.GetProperty("DisplayClaims").GetProperty("xui")[0].GetProperty("uhs").GetString()!;

            await updateStatusCallback("microsoft.minecraft_auth");

            var mcResp = await _http.PostAsync(MinecraftAuthUrl, JsonContent.Create(new
            {
                identityToken = $"XBL3.0 x={userHash};{xstsToken}"
            })).ConfigureAwait(false);

            if (!mcResp.IsSuccessStatusCode)
            {
                var body = await mcResp.Content.ReadAsStringAsync().ConfigureAwait(false);
                _config.Log($"Minecraft auth hatası ({mcResp.StatusCode}): {body}");
                return Fail("Minecraft oturumu açılamadı. Hesabında Minecraft Java Edition olduğundan emin ol.");
            }

            var mcJson = await mcResp.Content.ReadFromJsonAsync<JsonElement>().ConfigureAwait(false);
            var mcAccessToken = mcJson.GetProperty("access_token").GetString()!;

            await updateStatusCallback("microsoft.profile");

            var profileReq = new HttpRequestMessage(HttpMethod.Get, MinecraftProfileUrl);
            profileReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", mcAccessToken);
            var profileResp = await _http.SendAsync(profileReq).ConfigureAwait(false);

            string username;
            string uuid;

            if (profileResp.IsSuccessStatusCode)
            {
                var profileJson = await profileResp.Content.ReadFromJsonAsync<JsonElement>().ConfigureAwait(false);
                username = profileJson.GetProperty("name").GetString()!;
                uuid = profileJson.GetProperty("id").GetString()!;
            }
            else
            {
                var body = await profileResp.Content.ReadAsStringAsync().ConfigureAwait(false);
                _config.Log($"Minecraft profil hatası ({profileResp.StatusCode}): {body}");
                return Fail("Minecraft profili alınamadı. Hesabında Minecraft Java Edition olduğundan emin ol.");
            }

            _config.Log($"Microsoft girişi başarılı: {username}");

            return new MicrosoftAuthResult
            {
                Success = true,
                Username = username,
                Uuid = uuid,
                AccessToken = mcAccessToken,
                AuthType = "microsoft"
            };
        }
        catch (HttpRequestException httpEx)
        {
            _config.Log($"Microsoft giriş hatası (HTTP): {httpEx.Message}");
            return Fail("Sunucuya bağlanılamadı. İnternet bağlantını kontrol et.");
        }
        catch (TaskCanceledException)
        {
            _config.Log("Microsoft giriş hatası: İstek zaman aşımına uğradı.");
            return Fail("Bağlantı zaman aşımına uğradı. İnternet bağlantını kontrol et.");
        }
        catch (Exception ex)
        {
            _config.Log($"Microsoft giriş hatası: {ex.Message}");
            return Fail($"Beklenmeyen hata: {ex.Message}");
        }
    }

    private static MicrosoftAuthResult Fail(string message)
    {
        return new MicrosoftAuthResult { Success = false, ErrorMessage = message };
    }

    public void Dispose()
    {
        _http?.Dispose();
        GC.SuppressFinalize(this);
    }
}
