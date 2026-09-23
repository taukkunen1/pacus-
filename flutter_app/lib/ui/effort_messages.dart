import 'dart:math';

import '../models.dart';

const _genericEffort = [
  'Você conseguiu!',
  'Boa, você cuidou disso!',
  'Isso é responsabilidade!',
  'Você se organizou sozinho!',
  'Mandou bem!',
  'Show, você fez!',
];

const _challengeEffort = [
  'Você topou o desafio!',
  'Coragem de tentar algo novo!',
  'Isso não era fácil, e você foi!',
  'Adorei sua criatividade nisso!',
];

const _highEffort = [
  'Essa exigiu esforço de verdade!',
  'Você persistiu até o fim!',
  'Trabalho difícil, bem feito!',
];

const _periodComplete = [
  'Você fechou tudo desse período!',
  'Período inteiro em dia, muito bem!',
];

const _dayComplete = [
  'Você cuidou do dia inteiro sozinho!',
  'Dia completo — isso é consistência!',
  'Você deu conta de tudo hoje!',
];

String pickEffortMessage(
  DailyTask task,
  List<DailyTask> allTasks, {
  Random? random,
}) {
  final rng = random ?? Random();
  final active = allTasks.where((t) => !t.isDeleted).toList();

  if (active.isNotEmpty && active.every((t) => t.isDone)) {
    return _dayComplete[rng.nextInt(_dayComplete.length)];
  }

  final period = active
      .where((t) => t.period.toLowerCase() == task.period.toLowerCase())
      .toList();
  if (period.isNotEmpty && period.every((t) => t.isDone)) {
    return _periodComplete[rng.nextInt(_periodComplete.length)];
  }

  if (task.type.toLowerCase() == 'challenge') {
    return _challengeEffort[rng.nextInt(_challengeEffort.length)];
  }

  if (task.points.abs() >= 5) {
    return _highEffort[rng.nextInt(_highEffort.length)];
  }

  return _genericEffort[rng.nextInt(_genericEffort.length)];
}
