import 'dart:convert';

import 'package:shared_preferences/shared_preferences.dart';

import '../domain/models/paired_session.dart';

class PairingStorage {
  static const _sessionKey = 'paired_session';
  static const _deviceIdKey = 'device_id';

  Future<String> getOrCreateDeviceId() async {
    final prefs = await SharedPreferences.getInstance();
    final existing = prefs.getString(_deviceIdKey);
    if (existing != null) {
      return existing;
    }

    final deviceId = DateTime.now().millisecondsSinceEpoch.toRadixString(16);
    await prefs.setString(_deviceIdKey, deviceId);
    return deviceId;
  }

  Future<PairedSession?> loadSession() async {
    final prefs = await SharedPreferences.getInstance();
    final json = prefs.getString(_sessionKey);
    if (json == null) {
      return null;
    }

    return PairedSession.fromJson(jsonDecode(json) as Map<String, dynamic>);
  }

  Future<void> saveSession(PairedSession session) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_sessionKey, jsonEncode(session.toJson()));
  }

  Future<void> clearSession() async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.remove(_sessionKey);
  }
}
