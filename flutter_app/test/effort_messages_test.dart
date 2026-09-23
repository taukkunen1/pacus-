import 'dart:math';

import 'package:flutter_test/flutter_test.dart';
import 'package:pacus_flutter/models.dart';
import 'package:pacus_flutter/utils/effort_messages.dart';

DailyTask makeTask({
  required String id,
  required String period,
  required String type,
  required bool done,
  int points = 1,
}) =>
    DailyTask(
      id: id,
      title: id,
      period: period,
      type: type,
      status: done ? 'done' : 'pending',
      points: points,
    );

void main() {
  test('prioriza mensagem de dia completo', () {
    final completed = makeTask(id: 'a', period: 'morning', type: 'challenge', done: true, points: 8);
    final message = pickEffortMessage(
      completed,
      [
        completed,
        makeTask(id: 'b', period: 'evening', type: 'expected', done: true),
      ],
      random: Random(1),
    );
    expect({
      'Você cuidou do dia inteiro sozinho!',
      'Dia completo — isso é consistência!',
      'Você deu conta de tudo hoje!',
    }, contains(message));
  });

  test('prioriza período completo antes de desafio', () {
    final completed = makeTask(id: 'a', period: 'morning', type: 'challenge', done: true);
    final message = pickEffortMessage(
      completed,
      [
        completed,
        makeTask(id: 'b', period: 'morning', type: 'expected', done: true),
        makeTask(id: 'c', period: 'evening', type: 'expected', done: false),
      ],
      random: Random(1),
    );
    expect({
      'Você fechou tudo desse período!',
      'Período inteiro em dia, muito bem!',
    }, contains(message));
  });

  test('usa mensagem de desafio quando período continua pendente', () {
    final completed = makeTask(id: 'a', period: 'morning', type: 'challenge', done: true);
    final message = pickEffortMessage(
      completed,
      [
        completed,
        makeTask(id: 'b', period: 'morning', type: 'expected', done: false),
      ],
      random: Random(2),
    );
    expect({
      'Você topou o desafio!',
      'Coragem de tentar algo novo!',
      'Isso não era fácil, e você foi!',
      'Adorei sua criatividade nisso!',
    }, contains(message));
  });
}
