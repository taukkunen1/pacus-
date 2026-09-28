import 'dart:math';

import '../models.dart';

// 2026-09-28: type/points deixaram de variar (toda tarefa agora e 'expected'
// e vale 1 Pacus Point -- ver TaskValidation.ValidatePoints), entao as
// mensagens nao podem mais usar isso como sinal de variedade/especificidade.
// A partir de agora a variedade vem de (a) pools maiores e mais especificos,
// (b) mensagens que citam o titulo da tarefa, e (c) o momento (tarefa comum,
// periodo fechado, dia fechado).

const _genericEffort = [
  'Você conseguiu!',
  'Boa, você cuidou disso!',
  'Isso é responsabilidade!',
  'Você se organizou sozinho!',
  'Mandou bem!',
  'Show, você fez!',
  'Isso é você cuidando de você mesmo!',
  'Mais um passo, no seu ritmo!',
  'Você lembrou sozinho, isso conta muito!',
  'Aí sim!',
  'Isso vira hábito rapidinho desse jeito!',
  'Você está cuidando da sua rotina!',
];

// Templates que citam o titulo da tarefa -- misturados com os genericos pra
// dar a sensacao de que o Pacus percebeu especificamente o que foi feito.
const _titledEffort = [
  '"{title}" feito -- você cuidou disso sozinho!',
  'Marcou "{title}"! Isso é organização.',
  'Mais uma: "{title}" concluída. Bom trabalho!',
  '"{title}" já era. Você deu conta!',
];

const _periodComplete = [
  'Você fechou tudo desse período!',
  'Período inteiro em dia, muito bem!',
  'Nenhuma pendência nesse período -- ótimo ritmo!',
];

const _dayComplete = [
  'Você cuidou do dia inteiro sozinho!',
  'Dia completo — isso é consistência!',
  'Você deu conta de tudo hoje!',
  'Dia fechado com organização, parabéns!',
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

  final title = task.title.trim();
  final useTitled = title.isNotEmpty && rng.nextBool();
  if (useTitled) {
    final template = _titledEffort[rng.nextInt(_titledEffort.length)];
    return template.replaceAll('{title}', title);
  }

  return _genericEffort[rng.nextInt(_genericEffort.length)];
}
