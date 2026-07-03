namespace MicLinkWinUI.Infrastructure.Network.Protocol;

using System.Text.Json;
using System.Text.Json.Serialization;

public static class ProtocolJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static ProtocolMessage? Deserialize(string json) =>
        JsonSerializer.Deserialize<ProtocolMessage>(json, Options);
}

public sealed class ProtocolMessage
{
    public string Type { get; set; } = string.Empty;
    public string? Pin { get; set; }
    public string? DeviceId { get; set; }
    public string? DeviceName { get; set; }
    public string? Token { get; set; }
    public bool? Success { get; set; }
    public string? PcName { get; set; }
    public string? Error { get; set; }
    public int? Battery { get; set; }
    public int? Signal { get; set; }
    public bool? MicMuted { get; set; }
    public bool? CameraMuted { get; set; }
    public int? PingMs { get; set; }
    public int? AudioPort { get; set; }
}
