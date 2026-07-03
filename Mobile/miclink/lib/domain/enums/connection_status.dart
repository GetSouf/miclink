enum ConnectionStatus {
  disconnected,
  discovering,
  pairing,
  connecting,
  connected,
  reconnecting,
  error,
}

extension ConnectionStatusLabel on ConnectionStatus {
  String get label => switch (this) {
        ConnectionStatus.disconnected => 'Ожидание',
        ConnectionStatus.discovering => 'Поиск ПК…',
        ConnectionStatus.pairing => 'Готово к подключению',
        ConnectionStatus.connecting => 'Подключение…',
        ConnectionStatus.connected => 'Подключено',
        ConnectionStatus.reconnecting => 'Переподключение…',
        ConnectionStatus.error => 'Ошибка',
      };
}
