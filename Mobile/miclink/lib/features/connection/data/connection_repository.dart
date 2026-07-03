import 'dart:async';
import 'dart:typed_data';

import 'package:flutter/foundation.dart';
import 'package:miclink/core/constants/app_constants.dart';
import 'package:miclink/domain/enums/connection_status.dart';
import 'package:miclink/domain/models/device_connection_info.dart';
import 'package:miclink/features/audio/data/audio_stream_client.dart';
import 'package:miclink/features/audio/data/microphone_capture_service.dart';
import 'package:miclink/features/connection/data/mdns_discovery_service.dart';
import 'package:miclink/features/connection/data/miclink_protocol.dart';
import 'package:miclink/features/connection/data/pairing_client.dart';
import 'package:miclink/features/connection/data/pairing_storage.dart';
import 'package:miclink/features/connection/domain/models/discovered_pc.dart';
import 'package:miclink/features/connection/domain/models/paired_session.dart';

export 'package:miclink/features/connection/domain/models/discovered_pc.dart';

/// Phone-side connection orchestrator.
///
/// Reconnect uses a saved token + fresh mDNS host (never a stale IP).
/// [hardReset] clears all transport state — same effect as reinstalling the app.
class ConnectionRepository extends ChangeNotifier {
  ConnectionRepository() {
    unawaited(_bootstrap());
  }

  static const _maxReconnectAttempts = 8;
  static const _pcAudioReadyDelay = Duration(milliseconds: 220);

  final MdnsDiscoveryService _discovery = MdnsDiscoveryService();
  final PairingClient _client = PairingClient();
  final PairingStorage _storage = PairingStorage();
  final AudioStreamClient _audioClient = AudioStreamClient();
  final MicrophoneCaptureService _microphone = MicrophoneCaptureService();

  DeviceConnectionInfo _info = const DeviceConnectionInfo(
    status: ConnectionStatus.discovering,
  );

  DiscoveredPc? _discoveredPc;
  PairedSession? _session;
  Timer? _discoveryTimer;
  Timer? _reconnectTimer;
  Timer? _heartbeatTimer;
  String? _errorMessage;
  String _deviceId = '';
  final String _deviceName = 'MicLink Phone';
  int _connectionGeneration = 0;
  int _reconnectAttempts = 0;
  bool _operationInFlight = false;

  DeviceConnectionInfo get info => _info;
  DiscoveredPc? get discoveredPc => _discoveredPc;
  String? get errorMessage => _errorMessage;

  Future<void> resetConnection() => hardReset();

  Future<void> disconnect() => hardReset();

  Future<void> _bootstrap() async {
    _client.onDisconnected = _handleUnexpectedDisconnect;
    await _discovery.start();
    _deviceId = await _storage.getOrCreateDeviceId();

    final saved = await _storage.loadSession();
    if (saved != null) {
      _session = saved;
      _enterReconnecting(saved.pcName);
      return;
    }

    _enterDiscovering();
  }

  /// Full reset: timers, sockets, audio, stored session — clean slate.
  Future<void> hardReset() async {
    _connectionGeneration++;
    _cancelAllTimers();
    _reconnectAttempts = 0;
    _operationInFlight = false;
    _errorMessage = null;

    await _stopAudio();
    await _client.disconnect(notifyDisconnect: false);
    await _storage.clearSession();

    _session = null;
    _discoveredPc = null;
    _enterDiscovering();
  }

  void _cancelAllTimers() {
    _discoveryTimer?.cancel();
    _discoveryTimer = null;
    _reconnectTimer?.cancel();
    _reconnectTimer = null;
    _heartbeatTimer?.cancel();
    _heartbeatTimer = null;
  }

  void _enterDiscovering({String? error}) {
    _cancelAllTimers();
    _reconnectAttempts = 0;
    _errorMessage = error;
    _discoveredPc = null;
    _setInfo(_info.copyWith(status: ConnectionStatus.discovering, pcName: null));
    _startDiscoveryLoop();
  }

  void _enterPairing(DiscoveredPc pc) {
    if (_info.status == ConnectionStatus.connected ||
        _info.status == ConnectionStatus.connecting) {
      return;
    }
    _discoveredPc = pc;
    _errorMessage = null;
    _setInfo(_info.copyWith(
      status: ConnectionStatus.pairing,
      pcName: pc.displayName,
    ));
  }

  void _enterReconnecting(String? pcName) {
    _cancelAllTimers();
    _errorMessage = null;
    _setInfo(_info.copyWith(
      status: ConnectionStatus.reconnecting,
      pcName: pcName,
    ));
    _startDiscoveryLoop();
    _startReconnectLoop();
  }

  void _startDiscoveryLoop() {
    _discoveryTimer?.cancel();
    _discoveryTimer = Timer.periodic(
      const Duration(milliseconds: MicLinkProtocol.discoveryIntervalMs),
      (_) => unawaited(_scanForPc()),
    );
    unawaited(_scanForPc());
  }

  void _startReconnectLoop() {
    _reconnectTimer?.cancel();
    _reconnectTimer = Timer.periodic(
      const Duration(seconds: 2),
      (_) => unawaited(_attemptReconnect()),
    );
  }

  Future<void> _scanForPc() async {
    if (_operationInFlight ||
        _info.status == ConnectionStatus.connected ||
        _info.status == ConnectionStatus.connecting) {
      return;
    }

    final scanGen = _connectionGeneration;
    final pc = await _discovery.findPc(timeout: const Duration(seconds: 2));
    if (scanGen != _connectionGeneration || _operationInFlight) {
      return;
    }

    if (pc == null) {
      if (_info.status == ConnectionStatus.pairing ||
          _info.status == ConnectionStatus.error) {
        _discoveredPc = null;
        _setInfo(_info.copyWith(status: ConnectionStatus.discovering, pcName: null));
      }
      return;
    }

    _discoveredPc = pc;

    if (_info.status == ConnectionStatus.reconnecting) {
      unawaited(_attemptReconnect());
      return;
    }

    _enterPairing(pc);
  }

  PairedSession? _liveSession() {
    final base = _session;
    if (base == null) {
      return null;
    }
    final pc = _discoveredPc;
    if (pc == null) {
      return base;
    }
    return base.copyWith(host: pc.host, port: pc.port);
  }

  Future<void> _attemptReconnect() async {
    if (_operationInFlight ||
        _info.status != ConnectionStatus.reconnecting) {
      return;
    }

    final session = _liveSession() ?? await _loadStoredSessionWithFreshHost();
    if (session == null) {
      _enterDiscovering();
      return;
    }

    if (_discoveredPc == null) {
      return;
    }

    if (_reconnectAttempts >= _maxReconnectAttempts) {
      await _storage.clearSession();
      _session = null;
      _reconnectAttempts = 0;
      _reconnectTimer?.cancel();
      _enterPairing(_discoveredPc!);
      _errorMessage = 'Сессия устарела — введите PIN с экрана ПК.';
      notifyListeners();
      return;
    }

    _reconnectAttempts++;
    _operationInFlight = true;
    final generation = _connectionGeneration;

    try {
      await _client.disconnect(notifyDisconnect: false);

      final response = await _client.reconnect(
        session: session,
        deviceName: _deviceName,
      );

      if (generation != _connectionGeneration) {
        return;
      }

      if (response == null) {
        return;
      }

      if (response['success'] != true) {
        await _storage.clearSession();
        _session = null;
        _reconnectAttempts = 0;
        _reconnectTimer?.cancel();
        _errorMessage = 'Нужен новый PIN с экрана ПК.';
        _enterPairing(_discoveredPc!);
        return;
      }

      final updated = session.copyWith(
        pcName: response['pcName'] as String? ?? session.pcName,
        audioPort: response['audioPort'] as int? ?? session.audioPort,
      );
      await _onConnected(updated);
    } finally {
      _operationInFlight = false;
    }
  }

  Future<PairedSession?> _loadStoredSessionWithFreshHost() async {
    final stored = await _storage.loadSession();
    if (stored == null) {
      return null;
    }
    _session = stored;
    return _liveSession();
  }

  Future<void> pair(String pin) async {
    if (_operationInFlight) {
      return;
    }

    if (pin.trim().length < 4) {
      _errorMessage = 'Введите PIN с экрана ПК (4–6 цифр)';
      notifyListeners();
      return;
    }

    _operationInFlight = true;
    _connectionGeneration++;
    _cancelAllTimers();
    _reconnectAttempts = 0;
    _errorMessage = null;

    _setInfo(_info.copyWith(
      status: ConnectionStatus.connecting,
      pcName: _discoveredPc?.displayName ?? _info.pcName,
    ));
    notifyListeners();

    final generation = _connectionGeneration;

    try {
      await _stopAudio();
      await _client.disconnect(notifyDisconnect: false);
      await _storage.clearSession();
      _session = null;

      var pc = _discoveredPc ?? await _discovery.findPc(timeout: const Duration(seconds: 4));
      if (generation != _connectionGeneration) {
        return;
      }

      if (pc == null) {
        _errorMessage = 'ПК не найден. Запустите MicLink на ПК.';
        _enterDiscovering();
        return;
      }

      _discoveredPc = pc;
      _setInfo(_info.copyWith(
        status: ConnectionStatus.connecting,
        pcName: pc.displayName,
      ));
      notifyListeners();

      final response = await _client.connectAndPair(
        pc: pc,
        pin: pin.trim(),
        deviceId: _deviceId,
        deviceName: _deviceName,
      );

      if (generation != _connectionGeneration) {
        return;
      }

      if (response == null) {
        _errorMessage = 'ПК не ответил. Нажмите «Сбросить» и попробуйте снова.';
        _enterPairing(pc);
        return;
      }

      if (response['success'] != true) {
        _errorMessage = 'Неверный PIN. Актуальный PIN — на экране ПК.';
        _enterPairing(pc);
        return;
      }

      final session = PairedSession(
        deviceId: _deviceId,
        token: response['token'] as String,
        pcName: response['pcName'] as String? ?? pc.displayName,
        host: pc.host,
        port: pc.port,
        audioPort: response['audioPort'] as int? ?? AppConstants.audioPort,
      );

      await _onConnected(session);
    } finally {
      _operationInFlight = false;
      notifyListeners();
    }
  }

  Future<void> _onConnected(PairedSession session) async {
    _connectionGeneration++;
    _session = session;
    _reconnectAttempts = 0;
    _cancelAllTimers();
    await _storage.saveSession(session);

    _setInfo(_info.copyWith(
      status: ConnectionStatus.connected,
      pcName: session.pcName,
      transport: 'Wi‑Fi',
    ));
    notifyListeners();

    _heartbeatTimer = Timer.periodic(
      const Duration(milliseconds: MicLinkProtocol.heartbeatIntervalMs),
      (_) => _sendHeartbeat(),
    );
    _sendHeartbeat();

    await _beginLiveAudio(session);
  }

  /// Same path as unmute — ensures PC audio TCP + driver are ready before mic PCM.
  Future<void> _beginLiveAudio(PairedSession session) async {
    await _stopAudio();

    await _audioClient.connect(session.host, session.audioPort);
    await Future<void>.delayed(_pcAudioReadyDelay);

    if (_info.status != ConnectionStatus.connected || _session == null) {
      return;
    }

    if (!_info.isMicrophoneMuted) {
      await _microphone.start(onPcm: _onPcmChunk);
    }
  }

  void _onPcmChunk(Uint8List pcm) {
    if (_info.isMicrophoneMuted ||
        !_audioClient.isConnected ||
        _info.status != ConnectionStatus.connected) {
      return;
    }
    _audioClient.sendPcm(pcm);
  }

  Future<void> _stopAudio() async {
    await _microphone.stop();
    await _audioClient.disconnect();
  }

  void _sendHeartbeat() {
    final session = _session;
    if (session == null || !_client.isConnected) {
      return;
    }

    _client.sendHeartbeat(
      token: session.token,
      micMuted: _info.isMicrophoneMuted,
      cameraMuted: _info.isCameraMuted,
      battery: 100,
      signal: 100,
    );
  }

  void toggleMicrophone() {
    final muted = !_info.isMicrophoneMuted;
    _setInfo(_info.copyWith(isMicrophoneMuted: muted));
    _sendMuteUpdate();
    unawaited(_syncMicrophoneCapture());
  }

  Future<void> _syncMicrophoneCapture() async {
    final session = _session;
    if (session == null || _info.status != ConnectionStatus.connected) {
      return;
    }

    if (_info.isMicrophoneMuted) {
      await _microphone.stop();
      return;
    }

    await _beginLiveAudio(session);
  }

  void toggleCamera() {
    _setInfo(_info.copyWith(isCameraMuted: !_info.isCameraMuted));
    _sendMuteUpdate();
  }

  void _sendMuteUpdate() {
    final session = _session;
    if (session == null || !_client.isConnected) {
      return;
    }

    _client.sendMuteUpdate(
      token: session.token,
      micMuted: _info.isMicrophoneMuted,
      cameraMuted: _info.isCameraMuted,
    );
  }

  void _handleUnexpectedDisconnect() {
    if (_operationInFlight) {
      return;
    }

    final pcName = _info.pcName;
    _connectionGeneration++;
    _heartbeatTimer?.cancel();
    unawaited(_stopAudio());

    if (_session != null) {
      _enterReconnecting(pcName);
      return;
    }

    unawaited(_resumeAfterDisconnect(pcName));
  }

  Future<void> _resumeAfterDisconnect(String? pcName) async {
    final saved = await _storage.loadSession();
    if (saved == null) {
      _enterDiscovering();
      return;
    }

    _session = saved;
    _enterReconnecting(pcName ?? saved.pcName);
  }

  void _setInfo(DeviceConnectionInfo value) {
    _info = value;
    notifyListeners();
  }

  @override
  void dispose() {
    _cancelAllTimers();
    _discovery.stop();
    _client.disconnect(notifyDisconnect: false);
    unawaited(_stopAudio());
    unawaited(_microphone.dispose());
    super.dispose();
  }
}

extension on PairedSession {
  PairedSession copyWith({
    String? pcName,
    String? host,
    int? port,
    int? audioPort,
  }) =>
      PairedSession(
        deviceId: deviceId,
        token: token,
        pcName: pcName ?? this.pcName,
        host: host ?? this.host,
        port: port ?? this.port,
        audioPort: audioPort ?? this.audioPort,
      );
}
