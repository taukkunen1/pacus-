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
        Row(
          children: [
            Icon(alarm.volume == 0 ? Icons.volume_off : Icons.volume_up),
            const SizedBox(width: 8),
            Expanded(
              child: Text(
                'Volume do alarme: ${(alarm.volume * 100).round()}%',
                style: const TextStyle(fontWeight: FontWeight.w700),
              ),
            ),
          ],
        ),
        Slider(
          value: alarm.volume,
          divisions: 20,
          label: '${(alarm.volume * 100).round()}%',
          semanticFormatterCallback: (value) =>
              '${(value * 100).round()} por cento',
          onChanged: alarm.setVolume,
        ),
        Text(
          alarm.volume == 0
              ? 'Alarme silenciado. O aviso visual continua ativo.'
              : 'Ao terminar, o alarme toca continuamente por 30 segundos.',
        ),
        const SizedBox(height: 8),
        Wrap(
          spacing: 8,
          runSpacing: 8,
          children: [
            OutlinedButton.icon(
              onPressed: alarm.volume == 0
                  ? null
                  : () => alarm.play(preview: true),
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
