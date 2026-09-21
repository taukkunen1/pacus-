import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'api.dart';
import 'models.dart';
import 'screens/app_shell.dart';
import 'screens/login_screen.dart';
import 'theme.dart';

void main() {
  WidgetsFlutterBinding.ensureInitialized();
  runApp(const PacusApp());
}

class PacusApp extends StatefulWidget {
  const PacusApp({super.key});

  @override
  State<PacusApp> createState() => _PacusAppState();
}

class _PacusAppState extends State<PacusApp> {
  static const _themeKey = 'pacus.theme.mode';

  final api = PacusApi();
  AuthSession? session;
  ThemeMode themeMode = ThemeMode.system;
  bool loading = true;

  @override
  void initState() {
    super.initState();
    _restore();
  }

  Future<void> _restore() async {
    final prefs = await SharedPreferences.getInstance();
    final savedTheme = prefs.getString(_themeKey);
    themeMode = switch (savedTheme) {
      'light' => ThemeMode.light,
      'dark' => ThemeMode.dark,
      _ => ThemeMode.system,
    };
    session = await api.restoreSession();
    if (mounted) setState(() => loading = false);
  }

  Future<void> _setThemeMode(ThemeMode value) async {
    final prefs = await SharedPreferences.getInstance();
    final stored = switch (value) {
      ThemeMode.light => 'light',
      ThemeMode.dark => 'dark',
      ThemeMode.system => 'system',
    };
    await prefs.setString(_themeKey, stored);
    if (mounted) setState(() => themeMode = value);
  }

  Future<void> _logout() async {
    await api.logout();
    if (mounted) setState(() => session = null);
  }

  @override
  void dispose() {
    api.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      debugShowCheckedModeBanner: false,
      title: 'PACUS',
      theme: PacusTheme.light(),
      darkTheme: PacusTheme.dark(),
      themeMode: themeMode,
      home: loading
          ? const _BootScreen()
          : session == null
              ? LoginScreen(
                  api: api,
                  themeMode: themeMode,
                  onThemeChanged: _setThemeMode,
                  onLoggedIn: (value) => setState(() => session = value),
                )
              : PacusShell(
                  api: api,
                  session: session!,
                  themeMode: themeMode,
                  onThemeChanged: _setThemeMode,
                  onLogout: _logout,
                ),
    );
  }
}

class _BootScreen extends StatelessWidget {
  const _BootScreen();

  @override
  Widget build(BuildContext context) => const Scaffold(
        body: Center(child: CircularProgressIndicator()),
      );
}
