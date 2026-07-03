import 'package:flutter/material.dart';
import 'package:miclink/domain/enums/connection_status.dart';
import 'package:miclink/domain/models/device_connection_info.dart';
import 'package:miclink/features/connection/data/connection_repository.dart';

class HomeController extends ChangeNotifier {
  HomeController({ConnectionRepository? repository})
      : _repository = repository ?? ConnectionRepository() {
    _repository.addListener(_onRepositoryChanged);
  }

  final ConnectionRepository _repository;

  DeviceConnectionInfo get info => _repository.info;
  String? get errorMessage => _repository.errorMessage;
  bool get isConnected => _repository.info.status == ConnectionStatus.connected;
  bool get isBusyConnecting =>
      _repository.info.status == ConnectionStatus.connecting;

  void toggleMicrophone() => _repository.toggleMicrophone();

  void toggleCamera() => _repository.toggleCamera();

  Future<void> pair(String pin) => _repository.pair(pin);

  Future<void> hardReset() => _repository.hardReset();

  Future<void> resetConnection() => _repository.hardReset();

  Future<void> disconnect() => _repository.hardReset();

  void _onRepositoryChanged() => notifyListeners();

  @override
  void dispose() {
    _repository.removeListener(_onRepositoryChanged);
    _repository.dispose();
    super.dispose();
  }
}
