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
  String pick(List<String> values) => values[rng.nextInt(values.length)];

  final active = allTasks.where((t) => !t.isDeleted).toList();
  if (active.isNotEmpty && active.every((t) => t.isDone)) {
    return pick(_dayComplete);
  }

  final periodTasks = active
      .where((t) => t.period.toLowerCase() == task.period.toLowerCase())
      .toList();
  if (periodTasks.isNotEmpty && periodTasks.every((t) => t.isDone)) {
    return pick(_periodComplete);
  }

  if (task.type.toLowerCase() == 'challenge') {
    return pick(_challengeEffort);
  }

  if (task.points.abs() >= 5) {
    return pick(_highEffort);
  }

  return pick(_genericEffort);
}
