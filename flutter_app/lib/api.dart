import 'dart:convert';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';
import 'models.dart';

class ApiException implements Exception {
  const ApiException(this.message, [this.statusCode]);
  final String message;
  final int? statusCode;
  @override String toString() => message;
}

class PacusApi {
  PacusApi({http.Client? client}) : _client = client ?? http.Client();
  static const baseUrl = String.fromEnvironment('PACUS_API_BASE_URL', defaultValue: 'https://pacus-pacus-api.fly.dev/api/v1');
  static const _tokenKey = 'pacus.auth.token', _roleKey = 'pacus.auth.role', _userIdKey = 'pacus.auth.userId', _nameKey = 'pacus.auth.name';
  final http.Client _client;

  Future<AuthSession?> restoreSession() async {
    final prefs = await SharedPreferences.getInstance();
    final token = prefs.getString(_tokenKey);
    if (token == null || token.isEmpty) return null;
    return AuthSession(token: token, userId: prefs.getString(_userIdKey) ?? '', role: prefs.getString(_roleKey) ?? '', name: prefs.getString(_nameKey) ?? '');
  }

  Future<AuthSession> loginAdult(String email, String password) async {
    final session = AuthSession.fromJson(await _request('/auth/adult/login', method: 'POST', body: {'email': email.trim(), 'password': password}, authenticated: false));
    await _persistSession(session); return session;
  }

  Future<AuthSession> loginChild(String userId, String pin) async {
    final session = AuthSession.fromJson(await _request('/auth/child/login', method: 'POST', body: {'userId': userId.trim(), 'pin': pin.trim()}, authenticated: false));
    await _persistSession(session); return session;
  }

  Future<void> logout() async {
    final prefs = await SharedPreferences.getInstance();
    for (final key in [_tokenKey, _roleKey, _userIdKey, _nameKey]) { await prefs.remove(key); }
  }

  Future<DailyRoutine> getToday() async {
    try { return DailyRoutine.fromJson(await _request('/daily-routines/today')); }
    on ApiException catch (e) {
      if (e.statusCode != 409) rethrow;
      return DailyRoutine.fromJson(await _request('/daily-routines/today'));
    }
  }

  Future<DailyRoutine> consumeGameTimer(int minutes) async => DailyRoutine.fromJson(
    await _request('/daily-routines/today/game-timer/consume', method: 'PUT', body: {'minutes': minutes}));

  Future<DailyRoutine> adjustGameTimer(int deltaMinutes) async => DailyRoutine.fromJson(
    await _request('/daily-routines/today/game-timer/adjust', method: 'PUT', body: {'deltaMinutes': deltaMinutes}));

  Future<void> completeTask(String taskId) => _request('/daily-tasks/$taskId/complete', method: 'POST').then((_) {});
  Future<void> reopenTask(String taskId) => _request('/daily-tasks/$taskId/reopen', method: 'POST').then((_) {});

  Future<Map<String, dynamic>> _request(String path, {String method = 'GET', Map<String, dynamic>? body, bool authenticated = true}) async {
    final prefs = await SharedPreferences.getInstance();
    final token = authenticated ? prefs.getString(_tokenKey) : null;
    final headers = <String, String>{'Content-Type': 'application/json', if (token != null && token.isNotEmpty) 'Authorization': 'Bearer $token'};
    final uri = Uri.parse('$baseUrl$path');
    final encoded = body == null ? null : jsonEncode(body);
    late http.Response response;
    switch (method) {
      case 'POST': response = await _client.post(uri, headers: headers, body: encoded); break;
      case 'PUT': response = await _client.put(uri, headers: headers, body: encoded); break;
      case 'DELETE': response = await _client.delete(uri, headers: headers, body: encoded); break;
      default: response = await _client.get(uri, headers: headers);
    }
    if (response.statusCode == 401 && authenticated) await logout();
    if (response.statusCode < 200 || response.statusCode >= 300) {
      var message = 'Erro na API (${response.statusCode})';
      try {
        final parsed = jsonDecode(utf8.decode(response.bodyBytes));
        if (parsed is Map && parsed['error'] != null) message = parsed['error'].toString();
      } catch (_) {}
      throw ApiException(message, response.statusCode);
    }
    if (response.statusCode == 204 || response.bodyBytes.isEmpty) return <String, dynamic>{};
    final decoded = jsonDecode(utf8.decode(response.bodyBytes));
    if (decoded is! Map<String, dynamic>) throw const ApiException('Resposta inesperada da API.');
    return decoded;
  }

  Future<void> _persistSession(AuthSession session) async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_tokenKey, session.token); await prefs.setString(_roleKey, session.role);
    await prefs.setString(_userIdKey, session.userId); await prefs.setString(_nameKey, session.name);
  }

  void dispose() => _client.close();
}
