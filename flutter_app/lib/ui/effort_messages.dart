import '../models.dart';

/// Reconhecimento ancorado na ação concluída, sem inferir esforço, independência
/// ou dificuldade a partir da quantidade de pontos da tarefa.
String pickEffortMessage(DailyTask task, List<DailyTask> allTasks) {
  final active = allTasks.where((item) => !item.isDeleted).toList();

  if (active.isNotEmpty && active.every((item) => item.isDone)) {
    return 'Você concluiu as tarefas planejadas para hoje.';
  }

  final period = active
      .where((item) => item.period.toLowerCase() == task.period.toLowerCase())
      .toList();
  if (period.isNotEmpty && period.every((item) => item.isDone)) {
    final label = switch (task.period.toLowerCase()) {
      'morning' => 'da manhã',
      'afternoon' => 'da tarde',
      'evening' => 'da noite',
      _ => 'deste período',
    };
    return 'Você concluiu as tarefas $label.';
  }

  final selectedOption = task.selectedOption?.trim();
  if (selectedOption != null && selectedOption.isNotEmpty) {
    return 'Você concluiu “${task.title}” usando a opção que escolheu.';
  }

  if (task.type.toLowerCase() == 'challenge') {
    return 'Desafio concluído: ${task.title}.';
  }

  return 'Tarefa concluída: ${task.title}.';
}
