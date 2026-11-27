import 'dart:async';
import 'dart:math' as math;

import 'package:audioplayers/audioplayers.dart';
import 'package:flutter/foundation.dart';

abstract class TimerAlarmAudio {
  Future<void> play(double volume);
  Future<void> stop();
  Future<void> dispose();
}

class _PlayerAudio implements TimerAlarmAudio {
  final _player = AudioPlayer();
  late final _source = BytesSource(timerAlarmWav(), mimeType: 'audio/wav');

  @override
  Future<void> play(double volume) => _player.play(_source, volume: volume);
  @override
  Future<void> stop() => _player.stop();
  @override
  Future<void> dispose() => _player.dispose();
}

/// Owns playback independently of the end-of-session dialog and API request.
class TimerAlarm extends ChangeNotifier {
  TimerAlarm({TimerAlarmAudio? audio}) : _audio = audio ?? _PlayerAudio();

  static const duration = Duration(seconds: 30);
  static const volume = 1.0;
  final TimerAlarmAudio _audio;
  Future<void> _pending = Future<void>.value();
  Timer? _stopTimer;
  bool _disposed = false;
  int _generation = 0;
  bool playing = false;
  String? error;

  // Serialize plugin calls so a slow play cannot restart audio after stop/dispose.
  Future<void> _enqueue(Future<void> Function() operation) {
    _pending = _pending.then((_) => operation()).catchError((Object _) {
      if (!_disposed) {
        playing = false;
        error = 'Som indisponível. Toque em Testar som e confira o volume do aparelho.';
        notifyListeners();
      }
    });
    return _pending;
  }

  Future<void> play({bool preview = false}) {
    if (_disposed) return Future<void>.value();
    final generation = ++_generation;
    _stopTimer?.cancel();
    error = null;
    playing = true;
    notifyListeners();
    return _enqueue(() async {
      await _audio.stop();
      if (_disposed || generation != _generation) return;
      try {
        await _audio.play(volume);
      } catch (_) {
        await _audio.stop();
        rethrow;
      }
      if (_disposed || generation != _generation) {
        await _audio.stop();
        return;
      }
      _stopTimer = Timer(preview ? const Duration(seconds: 2) : duration, () {
        unawaited(stop());
      });
    });
  }

  Future<void> stop() {
    if (_disposed) return Future<void>.value();
    _generation++;
    _stopTimer?.cancel();
    playing = false;
    notifyListeners();
    return _enqueue(_audio.stop);
  }

  @override
  void dispose() {
    _disposed = true;
    _generation++;
    _stopTimer?.cancel();
    unawaited(
      _enqueue(() async {
        try {
          await _audio.stop();
        } finally {
          await _audio.dispose();
        }
      }),
    );
    super.dispose();
  }
}

/// A single 30-second WAV avoids loop gaps and stops even if browser timers lag.
Uint8List timerAlarmWav() {
  const rate = 22050;
  const samples = rate * 30;
  final data = ByteData(44 + samples * 2);
  void ascii(int offset, String text) {
    for (var i = 0; i < text.length; i++) {
      data.setUint8(offset + i, text.codeUnitAt(i));
    }
  }

  ascii(0, 'RIFF');
  data.setUint32(4, 36 + samples * 2, Endian.little);
  ascii(8, 'WAVE');
  ascii(12, 'fmt ');
  data.setUint32(16, 16, Endian.little);
  data.setUint16(20, 1, Endian.little);
  data.setUint16(22, 1, Endian.little);
  data.setUint32(24, rate, Endian.little);
  data.setUint32(28, rate * 2, Endian.little);
  data.setUint16(32, 2, Endian.little);
  data.setUint16(34, 16, Endian.little);
  ascii(36, 'data');
  data.setUint32(40, samples * 2, Endian.little);
  for (var i = 0; i < samples; i++) {
    final t = i / rate;
    final fade = math.min(1.0, math.min(t / 0.02, (30 - t) / 0.02));
    final tone =
        math.sin(2 * math.pi * 660 * t) * 0.55 +
        math.sin(2 * math.pi * 880 * t) * 0.25;
    data.setInt16(44 + i * 2, (tone * fade * 32700).round(), Endian.little);
  }
  return data.buffer.asUint8List();
}
