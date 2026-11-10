import 'package:flutter_test/flutter_test.dart';
import 'package:pacus_flutter/models.dart';
import 'package:pacus_flutter/ui/effort_messages.dart';

DailyTask task({
  String id = '1',
  String period = 'morning',
  String type = 'expected',
  String status = 'pending',
  int points = 1,
  String? selectedOption,
}) =>
    DailyTask(
      id: id,
      title: 'Teste',
      period: period,
      type: type,
      status: status,
      points: points,
      selectedOption: selectedOption,
    );

void main() {
  test('reconhece a conclusão do dia sem presumir que a criança fez tudo sozinha', () {
    final completed = [
      task(id: '1', status: 'done'),
      task(id: '2', period: 'evening', status: 'done'),
    ];

    expect(
      pickEffortMessage(task(), completed),
      'Você concluiu as tarefas planejadas para hoje.',
    );
  });

  test('reconhece o nome real da tarefa concluída', () {
    final challenge = task(type: 'challenge', status: 'done');
    expect(
      pickEffortMessage(challenge, [challenge, task(id: '2')]),
      'Desafio concluído: Teste.',
    );
  });

  test('reconhece uma escolha real quando ela foi registrada', () {
    final selected = task(status: 'done', selectedOption: 'Ler em voz alta');
    expect(
      pickEffortMessage(selected, [selected, task(id: '2')]),
      'Você concluiu “Teste” usando a opção que escolheu.',
    );
  });
}
