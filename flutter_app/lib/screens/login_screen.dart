import 'package:flutter/material.dart';
import '../api.dart';
import '../models.dart';

class LoginScreen extends StatefulWidget {
  const LoginScreen({super.key, required this.api, required this.onLoggedIn});
  final PacusApi api;
  final ValueChanged<AuthSession> onLoggedIn;

  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  final email = TextEditingController();
  final password = TextEditingController();
  final childId = TextEditingController();
  final pin = TextEditingController();
  bool childMode = false;
  bool busy = false;
  String? error;

  @override
  void dispose() {
    email.dispose(); password.dispose(); childId.dispose(); pin.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    setState(() { busy = true; error = null; });
    try {
      final session = childMode
          ? await widget.api.loginChild(childId.text, pin.text)
          : await widget.api.loginAdult(email.text, password.text);
      if (mounted) widget.onLoggedIn(session);
    } catch (e) {
      if (mounted) setState(() => error = e.toString());
    } finally {
      if (mounted) setState(() => busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Center(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(24),
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 460),
            child: Card(
              child: Padding(
                padding: const EdgeInsets.all(28),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    const Text('PACUS', textAlign: TextAlign.center,
                      style: TextStyle(fontSize: 38, fontWeight: FontWeight.w900, letterSpacing: 2)),
                    const SizedBox(height: 6),
                    Text('Rotina com autonomia', textAlign: TextAlign.center,
                      style: TextStyle(color: Theme.of(context).colorScheme.onSurfaceVariant)),
                    const SizedBox(height: 28),
                    SegmentedButton<bool>(
                      segments: const [
                        ButtonSegment(value: false, icon: Icon(Icons.person_outline), label: Text('Adulto')),
                        ButtonSegment(value: true, icon: Icon(Icons.child_care), label: Text('Criança')),
                      ],
                      selected: {childMode},
                      onSelectionChanged: busy ? null : (s) => setState(() { childMode = s.first; error = null; }),
                    ),
                    const SizedBox(height: 24),
                    if (!childMode) ...[
                      TextField(controller: email, keyboardType: TextInputType.emailAddress,
                        autofillHints: const [AutofillHints.email], decoration: const InputDecoration(labelText: 'E-mail')),
                      const SizedBox(height: 14),
                      TextField(controller: password, obscureText: true,
                        autofillHints: const [AutofillHints.password], decoration: const InputDecoration(labelText: 'Senha')),
                    ] else ...[
                      TextField(controller: childId, decoration: const InputDecoration(labelText: 'ID da criança')),
                      const SizedBox(height: 14),
                      TextField(controller: pin, obscureText: true, keyboardType: TextInputType.number,
                        decoration: const InputDecoration(labelText: 'PIN')),
                    ],
                    if (error != null) ...[
                      const SizedBox(height: 14),
                      Text(error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
                    ],
                    const SizedBox(height: 20),
                    FilledButton(
                      onPressed: busy ? null : _submit,
                      style: FilledButton.styleFrom(minimumSize: const Size.fromHeight(54)),
                      child: busy
                          ? const SizedBox(width: 22, height: 22, child: CircularProgressIndicator(strokeWidth: 2))
                          : Text(childMode ? 'Entrar como criança' : 'Entrar'),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
