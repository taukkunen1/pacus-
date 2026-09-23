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
}
