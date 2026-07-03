class MicLinkProtocol {
  MicLinkProtocol._();

  static const int version = 1;
  static const int heartbeatIntervalMs = 3000;
  static const int discoveryIntervalMs = 4000;

  static const String pairRequest = 'pair_request';
  static const String pairResponse = 'pair_response';
  static const String reconnectRequest = 'reconnect_request';
  static const String reconnectResponse = 'reconnect_response';
  static const String heartbeat = 'heartbeat';
  static const String heartbeatAck = 'heartbeat_ack';
  static const String muteUpdate = 'mute_update';
}
