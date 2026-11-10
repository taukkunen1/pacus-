import 'package:flutter_test/flutter_test.dart';
import 'package:pacus_flutter/ui/task_guidance.dart';

void main() {
  test('usa o título atual mesmo quando o motivo antigo fala de outra tarefa', () {
    final guidance = taskGuidance(
      'Depois do banho, escolher um livro e ler por 5 minutos.',
      reason: 'Separe a toalha para o banho.',
    );

    expect(guidance.text, contains('livro'));
    expect(guidance.text, isNot(contains('toalha')));
  });

  test('tarefas personalizadas preservam motivos e têm uma dica quando falta texto', () {
    expect(
      taskGuidance('Alimentar o gato', reason: 'O gato está com fome.').text,
      'O gato está com fome.',
    );
    expect(taskGuidance('Alimentar o gato').text, contains('Alimentar o gato'));
  });
}
