using System;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace LauncherRoot.Services;

public class LocalAuthServer : IDisposable
{
    private TcpListener? _listener;
    private int _port;

    public string RedirectUri => $"http://localhost:{_port}/";

    public void Start()
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        _port = ((IPEndPoint)_listener.LocalEndpoint).Port;
    }

    public async Task<string?> WaitForCodeAsync(int timeoutSeconds = 180)
    {
        if (_listener == null) return null;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        try
        {
            using var client = await _listener.AcceptTcpClientAsync().WaitAsync(cts.Token);
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream);

            var requestLine = await reader.ReadLineAsync().WaitAsync(cts.Token);
            if (requestLine == null) return null;

            var parts = requestLine.Split(' ');
            if (parts.Length < 2) return null;

            var path = parts[1];
            var queryStart = path.IndexOf('?');
            if (queryStart < 0)
            {
                SendResponse(stream, "<html><body><p>Giriş başarısız.</p></body></html>");
                return null;
            }

            var query = path[(queryStart + 1)..];
            var nvc = ParseQueryString(query);

            var code = nvc.Get("code");
            if (!string.IsNullOrEmpty(code))
            {
                SendResponse(stream, "<html><body><p>Giriş başarılı! Bu sekmeyi kapatabilirsin.</p><script>window.close()</script></body></html>");
                return code;
            }

            var error = nvc.Get("error");
            if (!string.IsNullOrEmpty(error))
            {
                SendResponse(stream, $"<html><body><p>Hata: {error}</p></body></html>");
                return null;
            }

            SendResponse(stream, "<html><body><p>Giriş başarısız.</p></body></html>");
            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    private static void SendResponse(NetworkStream stream, string body)
    {
        var bytes = Encoding.UTF8.GetBytes(
            $"HTTP/1.1 200 OK\r\nContent-Length: {body.Length}\r\nContent-Type: text/html; charset=utf-8\r\n\r\n{body}");
        stream.Write(bytes, 0, bytes.Length);
    }

    private static NameValueCollection ParseQueryString(string query)
    {
        var nvc = new NameValueCollection();
        if (string.IsNullOrEmpty(query)) return nvc;
        foreach (var pair in query.Split('&'))
        {
            var eq = pair.IndexOf('=');
            if (eq > 0)
            {
                var key = Uri.UnescapeDataString(pair[..eq]);
                var val = Uri.UnescapeDataString(pair[(eq + 1)..]);
                nvc.Add(key, val);
            }
        }
        return nvc;
    }

    public void Dispose()
    {
        _listener?.Stop();
        GC.SuppressFinalize(this);
    }
}

public static class PkceHelper
{
    public static (string verifier, string challenge) Generate()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        var verifier = Base64UrlEncode(bytes);

        var sha256 = SHA256.HashData(Encoding.UTF8.GetBytes(verifier));
        var challenge = Base64UrlEncode(sha256);

        return (verifier, challenge);
    }

    private static string Base64UrlEncode(byte[] data)
    {
        return Convert.ToBase64String(data)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
