import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:pacus_flutter/api.dart';
import 'package:shared_preferences/shared_preferences.dart';

void main() {
  setUp(() {
    SharedPreferences.setMockInitialValues({});
  });

  test('request envia Bearer token quando autenticado', () async {
    SharedPreferences.setMockInitialValues({
      'pacus.auth.token': 'jwt-teste',
      'pacus.auth.role': 'Adult',
      'pacus.auth.userId': 'u1',
      'pacus.auth.name': 'Pedro',
    });

    late http.Request captured;
    final api = PacusApi(
      client: MockClient((request) async {
        captured = request;
        return http.Response(
          jsonEncode({'ok': true}),
          200,
          headers: {'content-type': 'application/json'},
        );
      }),
    );

    final result = await api.getMap('/health');

    expect(result['ok'], isTrue);
    expect(captured.headers['Authorization'], 'Bearer jwt-teste');
    api.dispose();
  });

  test('request publico nao envia Authorization', () async {
    late http.Request captured;
    final api = PacusApi(
      client: MockClient((request) async {
        captured = request;
        return http.Response('{}', 200);
      }),
    );

    await api.request('/publico', authenticated: false);

    expect(captured.headers.containsKey('Authorization'), isFalse);
    api.dispose();
  });

  test('erro da API usa mensagem do campo error', () async {
    final api = PacusApi(
      client: MockClient((request) async => http.Response(
            jsonEncode({'error': 'Mensagem de teste'}),
            400,
            headers: {'content-type': 'application/json'},
          )),
    );

    expect(
      () => api.request('/falha'),
      throwsA(
        isA<ApiException>()
            .having((e) => e.message, 'message', 'Mensagem de teste')
            .having((e) => e.statusCode, 'statusCode', 400),
      ),
    );
    api.dispose();
  });

  test('401 limpa sessao persistida', () async {
    SharedPreferences.setMockInitialValues({
      'pacus.auth.token': 'token-expirado',
      'pacus.auth.role': 'Child',
      'pacus.auth.userId': 'u2',
      'pacus.auth.name': 'Hector',
    });

    final api = PacusApi(
      client: MockClient((request) async => http.Response(
            jsonEncode({'error': 'Nao autorizado'}),
            401,
            headers: {'content-type': 'application/json'},
          )),
    );

    try {
      await api.request('/privado');
    } on ApiException {
      // esperado
    }

    expect(await api.restoreSession(), isNull);
    api.dispose();
  });
}
