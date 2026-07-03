import '../enums/connection_status.dart';

class DeviceConnectionInfo {
  const DeviceConnectionInfo({
    this.status = ConnectionStatus.disconnected,
    this.pcName,
    this.transport = 'Wi‑Fi',
    this.isMicrophoneMuted = false,
    this.isCameraMuted = false,
  });

  final ConnectionStatus status;
  final String? pcName;
  final String transport;
  final bool isMicrophoneMuted;
  final bool isCameraMuted;

  DeviceConnectionInfo copyWith({
    ConnectionStatus? status,
    String? pcName,
    String? transport,
    bool? isMicrophoneMuted,
    bool? isCameraMuted,
  }) {
    return DeviceConnectionInfo(
      status: status ?? this.status,
      pcName: pcName ?? this.pcName,
      transport: transport ?? this.transport,
      isMicrophoneMuted: isMicrophoneMuted ?? this.isMicrophoneMuted,
      isCameraMuted: isCameraMuted ?? this.isCameraMuted,
    );
  }
}
