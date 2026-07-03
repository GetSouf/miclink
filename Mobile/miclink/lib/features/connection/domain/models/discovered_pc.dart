class DiscoveredPc {
  const DiscoveredPc({
    required this.host,
    required this.port,
    required this.serviceName,
    this.pcName,
  });

  final String host;
  final int port;
  final String serviceName;
  final String? pcName;

  String get displayName => pcName ?? serviceName;
}
