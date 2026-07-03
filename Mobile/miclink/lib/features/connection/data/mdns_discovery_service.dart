import 'dart:async';

import 'package:miclink/core/constants/app_constants.dart';
import 'package:miclink/features/connection/domain/models/discovered_pc.dart';
import 'package:multicast_dns/multicast_dns.dart';

class MdnsDiscoveryService {
  final MDnsClient _client = MDnsClient();

  Future<void> start() => _client.start();

  Future<void> stop() async => _client.stop();

  Future<DiscoveredPc?> findPc({
    Duration timeout = const Duration(seconds: 5),
  }) async {
    try {
      await for (final PtrResourceRecord ptr in _client.lookup<PtrResourceRecord>(
        ResourceRecordQuery.serverPointer('${AppConstants.mdnsServiceType}.local'),
      ).timeout(timeout)) {
        final pc = await _resolve(ptr.domainName);
        if (pc != null) {
          return pc;
        }
      }
    } catch (_) {
      return null;
    }

    return null;
  }

  Future<DiscoveredPc?> _resolve(String serviceName) async {
    String? host;
    var port = AppConstants.defaultPort;
    String? pcName;

    await for (final SrvResourceRecord srv in _client.lookup<SrvResourceRecord>(
      ResourceRecordQuery.service(serviceName),
    )) {
      host = srv.target;
      port = srv.port;
      break;
    }

    await for (final TxtResourceRecord txt in _client.lookup<TxtResourceRecord>(
      ResourceRecordQuery.text(serviceName),
    )) {
      pcName = _parsePcName(txt.text) ?? pcName;
    }

    if (host == null) {
      return null;
    }

    await for (final IPAddressResourceRecord ip in _client.lookup<IPAddressResourceRecord>(
      ResourceRecordQuery.addressIPv4(host),
    )) {
      return DiscoveredPc(
        host: ip.address.address,
        port: port,
        serviceName: serviceName,
        pcName: pcName,
      );
    }

    return null;
  }

  String? _parsePcName(String txt) {
    final parts = txt.contains('\x00') ? txt.split('\x00') : [txt];

    for (final part in parts) {
      if (part.startsWith('pcname=')) {
        return part.substring('pcname='.length);
      }
    }

    return null;
  }
}
