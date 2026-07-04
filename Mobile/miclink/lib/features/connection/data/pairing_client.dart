import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:miclink/features/connection/data/miclink_protocol.dart';
import 'package:miclink/features/connection/domain/models/discovered_pc.dart';
import 'package:miclink/features/connection/domain/models/paired_session.dart';

class PairingClient {
  Socket? _socket;
  StreamSubscription<List<int>>? _subscription;
  final _lineBuffer = StringBuffer();
  final _responseWaiters = <String, Completer<Map<String, dynamic>?>>{};
  bool _suppressDisconnect = false;

  void Function()? onDisconnected;
  void Function(Map<String, dynamic> message)? onControlMessage;

  bool get isConnected => _socket != null;

  Future<Map<String, dynamic>?> connectAndPair({
    required DiscoveredPc pc,
    required String pin,
    required String deviceId,
    required String deviceName,
  }) async {
    await _openSocket(pc.host, pc.port);
    _send({
      'type': MicLinkProtocol.pairRequest,
      'pin': pin,
      'deviceId': deviceId,
      'deviceName': deviceName,
    });
    return _waitForResponse(MicLinkProtocol.pairResponse);
  }

  Future<Map<String, dynamic>?> reconnect({
    required PairedSession session,
    required String deviceName,
  }) async {
    await _openSocket(session.host, session.port);
    _send({
      'type': MicLinkProtocol.reconnectRequest,
      'token': session.token,
      'deviceId': session.deviceId,
      'deviceName': deviceName,
    });
    return _waitForResponse(MicLinkProtocol.reconnectResponse);
  }

  void sendHeartbeat({
    required String token,
    required bool micMuted,
    required bool cameraMuted,
    int battery = 100,
    int signal = 100,
    int pingMs = 0,
  }) {
    _send({
      'type': MicLinkProtocol.heartbeat,
      'token': token,
      'battery': battery,
      'signal': signal,
      'micMuted': micMuted,
      'cameraMuted': cameraMuted,
      'pingMs': pingMs,
    });
  }

  void sendMuteUpdate({
    required String token,
    required bool micMuted,
    required bool cameraMuted,
  }) {
    _send({
      'type': MicLinkProtocol.muteUpdate,
      'token': token,
      'micMuted': micMuted,
      'cameraMuted': cameraMuted,
    });
  }

  Future<void> disconnect({bool notifyDisconnect = true}) async {
    _suppressDisconnect = !notifyDisconnect;
    await _subscription?.cancel();
    _subscription = null;

    try {
      await _socket?.close();
    } catch (_) {}

    _socket = null;
    _lineBuffer.clear();

    for (final waiter in _responseWaiters.values) {
      if (!waiter.isCompleted) {
        waiter.complete(null);
      }
    }
    _responseWaiters.clear();
    _suppressDisconnect = false;
  }

  Future<void> _openSocket(String host, int port) async {
    await disconnect(notifyDisconnect: false);
    _socket = await Socket.connect(
      host,
      port,
      timeout: const Duration(seconds: 5),
    );
    _socket!.setOption(SocketOption.tcpNoDelay, true);
    _subscription = _socket!.listen(
      _onData,
      onDone: _onSocketDone,
      onError: (_) => _onSocketDone(),
    );
  }

  void _onSocketDone() {
    if (_suppressDisconnect) {
      return;
    }
    onDisconnected?.call();
  }

  void _send(Map<String, dynamic> payload) {
    final socket = _socket;
    if (socket == null) {
      return;
    }
    socket.write('${jsonEncode(payload)}\n');
  }

  void _onData(List<int> data) {
    _lineBuffer.write(utf8.decode(data));
    final content = _lineBuffer.toString();
    final lines = content.split('\n');
    _lineBuffer.clear();

    if (!content.endsWith('\n') && lines.isNotEmpty) {
      _lineBuffer.write(lines.removeLast());
    }

    for (final line in lines) {
      if (line.trim().isEmpty) {
        continue;
      }

      final message = jsonDecode(line) as Map<String, dynamic>;
      final type = message['type'] as String?;
      if (type != null && _responseWaiters.containsKey(type)) {
        _responseWaiters.remove(type)?.complete(message);
        continue;
      }

      if (type != null) {
        onControlMessage?.call(message);
      }
    }
  }

  Future<Map<String, dynamic>?> _waitForResponse(String type) {
    final completer = Completer<Map<String, dynamic>?>();
    _responseWaiters[type] = completer;
    return completer.future.timeout(
      const Duration(seconds: 8),
      onTimeout: () => null,
    );
  }
}
