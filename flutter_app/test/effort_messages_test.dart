import 'dart:math';

import 'package:flutter_test/flutter_test.dart';
import 'package:pacus_flutter/models.dart';
import 'package:pacus_flutter/ui/effort_messages.dart';

DailyTask task({
  String id = '1',
  String period = 'morning',
  String type = 'expected',
  String status = 'pending',
  int points = 1,
}) =>
    DailyTask(
      id: id,
      title: 'Teste',
      period: period,
      type: type,
      status: status,
      points: points,
    );

void main() {
  test('prioriza dia completo', () {
    final justDone = task(status: 'pending');
    final all = [
      task(id: '1', status: 'done'),
      task(id: '2', period: 'evening', status: 'done'),
    ];

    expect(
      pickEffortMessage(justDone, all, random: Random(1)),
      anyOf(
        'Você cuidou do dia inteiro sozinho!',
        'Dia completo — isso é consistência!',
        'Você deu conta de tudo hoje!',
      ),
    );
  });

  test('usa mensagem de desafio quando o dia ainda nao terminou', () {
    final challenge = task(type: 'challenge');
    final all = [
      task(id: '1', type: 'challenge', status: 'done'),
      task(id: '2', status: 'pending'),
    ];

    expect(
      pickEffortMessage(challenge, all, random: Random(1)),
      isNotEmpty,
    );
  });
}
