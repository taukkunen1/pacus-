import 'package:flutter/material.dart';
import 'api.dart';
import 'models.dart';
import 'screens/home_screen.dart';
import 'screens/login_screen.dart';

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
  final api = PacusApi();
  AuthSession? session;
  bool loading = true;

  @override
  void initState() {
    super.initState();
    _restore();
  }

  Future<void> _restore() async {
    session = await api.restoreSession();
    if (mounted) setState(() => loading = false);
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
      theme: ThemeData(
        useMaterial3: true,
        fontFamily: 'Arial',
        colorScheme: ColorScheme.fromSeed(
          seedColor: const Color(0xFF1F6A55),
          brightness: Brightness.light,
          surface: const Color(0xFFF9F6EF),
        ),
        scaffoldBackgroundColor: const Color(0xFFF5F1E7),
        cardTheme: const CardThemeData(
          elevation: 0,
          margin: EdgeInsets.zero,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.all(Radius.circular(24)),
          ),
        ),
        inputDecorationTheme: InputDecorationTheme(
          filled: true,
          fillColor: Colors.white,
          border: OutlineInputBorder(
            borderRadius: BorderRadius.circular(18),
            borderSide: BorderSide.none,
          ),
        ),
      ),
      home: loading
          ? const _BootScreen()
          : session == null
              ? LoginScreen(
                  api: api,
                  onLoggedIn: (value) => setState(() => session = value),
                )
              : HomeScreen(api: api, session: session!, onLogout: _logout),
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
