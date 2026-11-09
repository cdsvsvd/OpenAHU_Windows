using System.IO;
using System.Text.Json;
using OpenAHU.Contracts.Json;

namespace OpenAHU.Desktop.Shell;

internal sealed record SessionHandshake(int Port, string Token, int ProcessId, DateTimeOffset StartedAt, string BaseUrl)
{
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenAHU", "session.json");

    public static SessionHandshake Read(string path)
    {
        var json = File.ReadAllText(path);
        var handshake = JsonSerializer.Deserialize<SessionHandshake>(json, ContractJson.CreateOptions())
            ?? throw new InvalidDataException("本地服务握手为空。");
        Validate(handshake);
        return handshake;
    }

    private static void Validate(SessionHandshake handshake)
    {
        if (handshake.Port is < 1 or > 65535 || handshake.ProcessId <= 0 ||
            string.IsNullOrWhiteSpace(handshake.Token) || handshake.Token.Any(char.IsControl) ||
            !Uri.TryCreate(handshake.BaseUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttp || uri.Host != "127.0.0.1" || uri.Port != handshake.Port ||
            uri.AbsolutePath != "/" || uri.Query.Length != 0 || uri.Fragment.Length != 0 || uri.UserInfo.Length != 0)
        {
            throw new InvalidDataException("本地服务握手无效。");
        }
    }
}
