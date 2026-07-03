namespace MicLinkWinUI.Infrastructure.Network.Protocol;

public static class MicLinkProtocol
{
    public const int Version = 1;
    public const int HeartbeatIntervalMs = 3000;

    public static class MessageTypes
    {
        public const string PairRequest = "pair_request";
        public const string PairResponse = "pair_response";
        public const string ReconnectRequest = "reconnect_request";
        public const string ReconnectResponse = "reconnect_response";
        public const string Heartbeat = "heartbeat";
        public const string HeartbeatAck = "heartbeat_ack";
        public const string MuteUpdate = "mute_update";
    }
}
