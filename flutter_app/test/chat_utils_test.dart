import 'package:flutter_test/flutter_test.dart';
import 'package:pacus_flutter/ui/chat_utils.dart';

void main() {
  test('formats same-day timestamp as hour and minute', () {
    final result = formatChatTimestamp(
      '2026-09-23T12:34:00Z',
      now: DateTime.parse('2026-09-23T15:00:00Z'),
    );

    expect(result, isNotEmpty);
    expect(result, isNot(contains('/')));
  });

  test('formats older timestamp with day and month', () {
    final result = formatChatTimestamp(
      '2026-09-22T12:34:00Z',
      now: DateTime.parse('2026-09-23T15:00:00Z'),
    );

    expect(result, contains('/'));
  });

  test('invalid timestamp returns empty text', () {
    expect(formatChatTimestamp('not-a-date'), '');
    expect(formatChatTimestamp(null), '');
  });

  test('labels quick requests', () {
    expect(chatRequestTitle('help'), 'Preciso de ajuda');
    expect(chatRequestTitle('change_task'), 'Quero mudar uma tarefa');
    expect(
      chatRequestTitle('extra_time', minutes: 20),
      '+20 min de tempo de tela',
    );
  });

  test('distinguishes requests for support from rewards', () {
    expect(chatRequestNeedsSupport('help'), isTrue);
    expect(chatRequestNeedsSupport('change_task'), isTrue);
    expect(chatRequestNeedsSupport('extra_time'), isFalse);

    expect(chatRequestApproveLabel('help'), 'Vou ajudar');
    expect(chatRequestApproveLabel('change_task'), 'Vamos conversar');
    expect(chatRequestApproveLabel('extra_time'), 'Aprovar');
  });

  test('support request statuses avoid approval and rejection language', () {
    expect(
      chatRequestStatusLabel('pending', type: 'help'),
      'Aguardando resposta',
    );
    expect(chatRequestStatusLabel('approved', type: 'help'), 'Recebido');
    expect(
      chatRequestStatusLabel('rejected', type: 'change_task'),
      'Nao atendido',
    );
    expect(
      chatRequestStatusLabel('approved', type: 'extra_time'),
      'Aprovado',
    );
  });

  test('labels request status', () {
    expect(chatRequestStatusLabel('pending'), 'Aguardando');
    expect(chatRequestStatusLabel('approved'), 'Aprovado');
    expect(chatRequestStatusLabel('rejected'), 'Recusado');
  });
}
