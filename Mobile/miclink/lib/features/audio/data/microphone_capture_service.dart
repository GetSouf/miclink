import 'dart:async';
import 'dart:typed_data';

import 'package:miclink/core/constants/audio_constants.dart';
import 'package:record/record.dart';

class MicrophoneCaptureService {
  final AudioRecorder _recorder = AudioRecorder();
  StreamSubscription<Uint8List>? _subscription;

  Future<bool> hasPermission() => _recorder.hasPermission();

  Future<void> start({
    required void Function(Uint8List pcm) onPcm,
  }) async {
    await stop();

    if (!await hasPermission()) {
      return;
    }

    final stream = await _recorder.startStream(
      const RecordConfig(
        encoder: AudioEncoder.pcm16bits,
        sampleRate: AudioConstants.sampleRate,
        numChannels: AudioConstants.channels,
        bitRate: 768000,
        autoGain: false,
        echoCancel: false,
        noiseSuppress: false,
      ),
    );

    _subscription = stream.listen(onPcm);
  }

  Future<void> stop() async {
    await _subscription?.cancel();
    _subscription = null;

    if (await _recorder.isRecording()) {
      await _recorder.stop();
    }
  }

  Future<void> dispose() async {
    await stop();
    await _recorder.dispose();
  }
}
