import 'package:flutter/material.dart';

import 'timer_alarm.dart';

class TimerAlarmControls extends StatelessWidget {
  const TimerAlarmControls({super.key, required this.alarm});
  final TimerAlarm alarm;

  @override
  Widget build(BuildContext context) => ListenableBuilder(
    listenable: alarm,
    builder: (context, _) => Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        const Row(
          children: [
            Icon(Icons.volume_up),
            SizedBox(width: 8),
            Expanded(
              child: Text(
                'Volume do alarme: 100%',
                style: TextStyle(fontWeight: FontWeight.w700),
              ),
            ),
          ],
        ),
        const SizedBox(height: 8),
        const Text('Ao terminar, o alarme toca continuamente por 30 segundos.'),
        const SizedBox(height: 8),
        Wrap(
          spacing: 8,
          runSpacing: 8,
          children: [
            OutlinedButton.icon(
              onPressed: () => alarm.play(preview: true),
              icon: const Icon(Icons.play_arrow_rounded),
              label: const Text('Testar som'),
            ),
            if (alarm.playing)
              FilledButton.tonalIcon(
                onPressed: alarm.stop,
                icon: const Icon(Icons.volume_off),
                label: const Text('Parar alarme'),
              ),
          ],
        ),
        if (alarm.error != null) ...[
          const SizedBox(height: 8),
          Text(alarm.error!, semanticsLabel: alarm.error),
        ],
      ],
    ),
  );
}
