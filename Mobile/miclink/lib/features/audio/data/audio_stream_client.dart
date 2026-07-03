import 'dart:io';
import 'dart:typed_data';

class AudioStreamClient {
  Socket? _socket;

  bool get isConnected => _socket != null;

  Future<void> connect(String host, int port) async {
    await disconnect();
    _socket = await Socket.connect(
      host,
      port,
      timeout: const Duration(seconds: 5),
    );
    _socket!.setOption(SocketOption.tcpNoDelay, true);
  }

  void sendPcm(Uint8List pcm) {
    final socket = _socket;
    if (socket == null) {
      return;
    }

    final header = ByteData(4)..setUint32(0, pcm.length, Endian.little);
    socket.add(header.buffer.asUint8List());
    socket.add(pcm);
  }

  Future<void> disconnect() async {
    await _socket?.close();
    _socket = null;
  }
}
