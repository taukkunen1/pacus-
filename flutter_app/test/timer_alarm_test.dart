import 'dart:async';
import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:pacus_flutter/api.dart';
import 'package:pacus_flutter/models.dart';
import 'package:pacus_flutter/screens/home_screen.dart';
import 'package:pacus_flutter/ui/timer_alarm.dart';
import 'package:pacus_flutter/ui/timer_alarm_controls.dart';
import 'package:shared_preferences/shared_preferences.dart';

class FakeAudio implements TimerAlarmAudio {
  int plays = 0;
  int stops = 0;
  bool disposed = false;
  bool fail = false;
  double? volume;
  Completer<void>? pendingPlay;

  @override
  Future<void> play(double value) async {
    plays++;
    volume = value;
    if (fail) throw StateError('Audio blocked');
    await pendingPlay?.future;
  }

  @override
  Future<void> stop() async => stops++;
  @override
  Future<void> dispose() async => disposed = true;
}

class TimerApi extends PacusApi {
  TimerApi({this.active = true});

  final bool active;
  final finish = Completer<DailyRoutine>();
  int finishes = 0;

  DailyRoutine value({bool active = false}) => DailyRoutine(
    id: 'routine',
    familyId: 'family',
    date: '2026-11-20',
    gameTimerEnabled: true,
    gameTimerMinutes: 60,
    gameTimerExtraMinutes: 0,
    tasks: const [],
    gameTimerSessionMinutes: active ? 1 : null,
    gameTimerSessionEndsAt: active
        ? DateTime.now().subtract(const Duration(seconds: 1))
        : null,
  );

  @override
  Future<DailyRoutine> getToday() async => value(active: active);
  @override
  Future<DailyRoutine> finishGameTimerSession() {
    finishes++;
    return finish.future;
  }
}

void main() {
  setUp(() => SharedPreferences.setMockInitialValues({}));

  testWidgets('alarm plays at full volume and stops after 30 seconds', (
    tester,
  ) async {
    final audio = FakeAudio();
    final alarm = TimerAlarm(audio: audio);
    await alarm.play();
    expect(audio.volume, 1.0);
    await tester.pump(const Duration(seconds: 29));
    expect(alarm.playing, isTrue);
    await tester.pump(const Duration(seconds: 1));
    expect(alarm.playing, isFalse);
    expect(audio.stops, 2);
    alarm.dispose();
    await tester.pump();
    expect(audio.disposed, isTrue);
  });

  testWidgets('old mute preference cannot change the alarm volume', (tester) async {
    SharedPreferences.setMockInitialValues({'pacus.timer.alarm.volume': 0.0});
    final audio = FakeAudio();
    final alarm = TimerAlarm(audio: audio);
    await alarm.play();
    expect(audio.volume, 1.0);
    expect(alarm.playing, isTrue);
    alarm.dispose();
    await tester.pump();
  });

  testWidgets('reminds to start a game every 15 minutes while idle', (tester) async {
    await tester.pumpWidget(
      MaterialApp(
        home: HomeScreen(
          api: TimerApi(active: false),
          timerAlarm: TimerAlarm(audio: FakeAudio()),
          session: const AuthSession(
            token: 'test',
            userId: 'child',
            role: 'Child',
            name: 'Teste',
          ),
          onLogout: () async {},
        ),
      ),
    );
    await tester.pumpAndSettle();
    const message = 'Vai jogar agora? Inicie seu tempo de jogo.';
    expect(find.byType(Slider), findsNothing);
    await tester.pump(const Duration(minutes: 14));
    expect(find.text(message), findsNothing);
    await tester.pump(const Duration(minutes: 1));
    await tester.pumpAndSettle();
    expect(find.text(message), findsOneWidget);
    await tester.tap(find.text('Ver cronômetro'));
    await tester.pumpAndSettle();
    expect(find.text(message), findsNothing);
    await tester.pump(const Duration(minutes: 15));
    await tester.pumpAndSettle();
    expect(find.text(message), findsOneWidget);
    await tester.pumpWidget(const SizedBox.shrink());
  });

  testWidgets('preview stops after two seconds and can be stopped manually', (
    tester,
  ) async {
    final alarm = TimerAlarm(audio: FakeAudio());
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(body: TimerAlarmControls(alarm: alarm)),
      ),
    );
    await tester.tap(find.text('Testar som'));
    await tester.pump();
    expect(find.text('Parar alarme'), findsOneWidget);
    await tester.pump(const Duration(seconds: 2));
    expect(alarm.playing, isFalse);
    await tester.tap(find.text('Testar som'));
    await tester.pump();
    await tester.tap(find.text('Parar alarme'));
    await tester.pump();
    expect(alarm.playing, isFalse);
    await tester.pumpWidget(const SizedBox.shrink());
    alarm.dispose();
    await tester.pump();
  });

  testWidgets('blocked audio reports a visible recovery action', (
    tester,
  ) async {
    final audio = FakeAudio()..fail = true;
    final alarm = TimerAlarm(audio: audio);
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(body: TimerAlarmControls(alarm: alarm)),
      ),
    );
    await tester.tap(find.text('Testar som'));
    await tester.pump();
    expect(alarm.playing, isFalse);
    expect(find.textContaining('Som indisponível'), findsOneWidget);
    await tester.pumpWidget(const SizedBox.shrink());
    alarm.dispose();
    await tester.pump();
  });

  testWidgets(
    'dispose cancels an in-flight play without leaving sound active',
    (tester) async {
      final audio = FakeAudio()..pendingPlay = Completer<void>();
      final alarm = TimerAlarm(audio: audio);
      unawaited(alarm.play());
      await tester.pump();
      expect(audio.plays, 1);
      alarm.dispose();
      audio.pendingPlay!.complete();
      await tester.pump();
      expect(audio.disposed, isTrue);
      expect(audio.stops, greaterThanOrEqualTo(2));
      await tester.pump(const Duration(seconds: 31));
      expect(audio.plays, 1);
    },
  );

  testWidgets(
    'expiry sounds before slow API and dialog acknowledgment stops alarm',
    (tester) async {
      final audio = FakeAudio();
      final alarm = TimerAlarm(audio: audio);
      final api = TimerApi();
      await tester.pumpWidget(
        MaterialApp(
          home: HomeScreen(
            api: api,
            timerAlarm: alarm,
            session: const AuthSession(
              token: 'test',
              userId: 'child',
              role: 'Child',
              name: 'Teste',
            ),
            onLogout: () async {},
          ),
        ),
      );
      await tester.pump();
      await tester.pump();
      expect(audio.plays, 1);
      expect(api.finishes, 1);
      expect(alarm.playing, isTrue);
      api.finish.complete(api.value());
      await tester.pumpAndSettle();
      expect(find.text('Tempo encerrado'), findsOneWidget);
      await tester.tap(find.text('Entendi e parar alarme'));
      await tester.pumpAndSettle();
      expect(alarm.playing, isFalse);
      expect(audio.plays, 1);
      await tester.pumpWidget(const SizedBox.shrink());
      await tester.pump();
      expect(audio.disposed, isTrue);
    },
  );

  test('WAV contains 30 seconds of continuous audible signal', () {
    final bytes = timerAlarmWav();
    final data = ByteData.sublistView(bytes);
    final rate = data.getUint32(24, Endian.little);
    final size = data.getUint32(40, Endian.little);
    expect(size / (rate * 2), 30);
    expect(bytes.length, size + 44);
    // Every 100 ms contains an audible peak; no gaps between repeated clips.
    for (var start = 0; start < size ~/ 2; start += rate ~/ 10) {
      var peak = 0;
      for (var i = start; i < start + rate ~/ 10; i++) {
        final sample = data.getInt16(44 + i * 2, Endian.little).abs();
        if (sample > peak) peak = sample;
      }
      expect(peak, greaterThan(10000));
    }
  });
}
