import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:pacus_flutter/api.dart';
import 'package:pacus_flutter/models.dart';
import 'package:pacus_flutter/screens/chat_screen.dart';

class FakeChatApi extends PacusApi {
  int markReadCalls = 0;
  Map<String, dynamic>? lastReadPayload;

  @override
  Future<List<dynamic>> getList(String path) async {
    if (path == '/chat/messages') {
      return [
        {
          'id': '507f1f77bcf86cd799439011',
          'senderId': 'other-user',
          'senderName': 'Hector',
          'senderRole': 'Child',
          'text': 'Oi!',
          'createdAt': '2026-09-23T12:00:00Z',
        },
      ];
    }
    return [];
  }

  @override
  Future<Map<String, dynamic>> putMap(
    String path,
    Map<String, dynamic> body,
  ) async {
    if (path == '/chat/read') {
      markReadCalls++;
      lastReadPayload = body;
      return {'unreadCount': 0};
    }
    return {};
  }
}

void main() {
  testWidgets('adds the selected emoji to the message draft', (tester) async {
    await tester.pumpWidget(
      MaterialApp(
        home: ChatScreen(
          api: FakeChatApi(),
          session: const AuthSession(
            token: 'token',
            userId: 'adult-user',
            role: 'Adult',
            name: 'Adulto',
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();

    await tester.enterText(find.byType(TextField), 'Oi ');
    await tester.tap(find.byTooltip('Escolher emoji'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('😊'));
    await tester.pumpAndSettle();

    final field = tester.widget<TextField>(find.byType(TextField));
    expect(field.controller!.text, 'Oi 😊');

    await tester.pumpWidget(const SizedBox.shrink());
  });

  testWidgets('loads chat and marks last visible message as read', (tester) async {
    final api = FakeChatApi();
    int? unread;

    await tester.pumpWidget(
      MaterialApp(
        home: ChatScreen(
          api: api,
          session: const AuthSession(
            token: 'token',
            userId: 'adult-user',
            role: 'Adult',
            name: 'Adulto',
          ),
          onUnreadChanged: (value) => unread = value,
        ),
      ),
    );

    await tester.pump();
    await tester.pump(const Duration(milliseconds: 200));

    expect(find.text('Oi!'), findsOneWidget);
    expect(api.markReadCalls, greaterThanOrEqualTo(1));
    expect(
      api.lastReadPayload?['lastMessageId'],
      '507f1f77bcf86cd799439011',
    );
    expect(unread, 0);

    await tester.pumpWidget(const SizedBox.shrink());
    await tester.pump();
  });
}
