import 'package:miclink/core/constants/app_constants.dart';

class PairedSession {
  const PairedSession({
    required this.deviceId,
    required this.token,
    required this.pcName,
    required this.host,
    required this.port,
    this.audioPort = AppConstants.audioPort,
  });

  final String deviceId;
  final String token;
  final String pcName;
  final String host;
  final int port;
  final int audioPort;

  Map<String, dynamic> toJson() => {
        'deviceId': deviceId,
        'token': token,
        'pcName': pcName,
        'host': host,
        'port': port,
        'audioPort': audioPort,
      };

  factory PairedSession.fromJson(Map<String, dynamic> json) => PairedSession(
        deviceId: json['deviceId'] as String,
        token: json['token'] as String,
        pcName: json['pcName'] as String,
        host: json['host'] as String,
        port: json['port'] as int,
        audioPort: json['audioPort'] as int? ?? AppConstants.audioPort,
      );
}
