
import 'package:flutter/material.dart';
import '../api.dart';
import '../models.dart';

class LoginScreen extends StatefulWidget {
  const LoginScreen({super.key, required this.api, required this.onLoggedIn});
  final PacusApi api;
  final ValueChanged<AuthSession> onLoggedIn;
  @override State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  final email = TextEditingController();
  final password = TextEditingController();
  final familyCode = TextEditingController();
  final pin = TextEditingController();
  bool memberMode = false;
  bool busy = false;
  String? error;
  List<Map<String, dynamic>> profiles = [];
  Map<String, dynamic>? selectedProfile;

  @override
  void dispose() {
    email.dispose(); password.dispose(); familyCode.dispose(); pin.dispose();
    super.dispose();
  }

  Future<void> _loginAdult() async {
    setState(() { busy = true; error = null; });
    try {
      final session = await widget.api.loginAdult(email.text, password.text);
      if (mounted) widget.onLoggedIn(session);
    } catch (e) {
      if (mounted) setState(() => error = e.toString());
    } finally {
      if (mounted) setState(() => busy = false);
    }
  }

  Future<void> _findProfiles() async {
    final code = familyCode.text.replaceAll(RegExp(r'[^A-Za-z0-9]'), '').toUpperCase();
    if (code.length != 6) {
      setState(() => error = 'Digite o código de 6 caracteres da família.');
      return;
    }
    final formatted = code.substring(0, 3) + '-' + code.substring(3);
    setState(() { busy = true; error = null; });
    try {
      final raw = await widget.api.request('/family/by-code/' + formatted + '/children', authenticated: false);
      final list = List<dynamic>.from(raw as List).map((e) => Map<String, dynamic>.from(e as Map)).toList();
      if (!mounted) return;
      setState(() {
        profiles = list;
        selectedProfile = list.length == 1 ? list.first : null;
        error = list.isEmpty ? 'Nenhum perfil encontrado para esse código.' : null;
      });
    } catch (e) {
      if (mounted) setState(() => error = e.toString());
    } finally {
      if (mounted) setState(() => busy = false);
    }
  }

  Future<void> _loginMember() async {
    final profile = selectedProfile;
    if (profile == null) {
      setState(() => error = 'Escolha seu perfil.');
      return;
    }
    setState(() { busy = true; error = null; });
    try {
      final session = await widget.api.loginChild(profile['id'].toString(), pin.text);
      if (mounted) widget.onLoggedIn(session);
    } catch (e) {
      if (mounted) setState(() => error = e.toString());
    } finally {
      if (mounted) setState(() => busy = false);
    }
  }

  Future<void> _register() async {
    final adultName = TextEditingController();
    final adultEmail = TextEditingController();
    final adultPassword = TextEditingController();
    final memberName = TextEditingController();
    final memberPin = TextEditingController();
    bool consent = false;

    final payload = await showDialog<Map<String, dynamic>>(
      context: context,
      builder: (context) => StatefulBuilder(
        builder: (context, setDialog) => AlertDialog(
          title: const Text('Criar família'),
          content: SizedBox(
            width: 520,
            child: SingleChildScrollView(
              child: Column(mainAxisSize: MainAxisSize.min, children: [
                TextField(controller: adultName, decoration: const InputDecoration(labelText: 'Nome do adulto responsável')),
                const SizedBox(height: 8),
                TextField(controller: adultEmail, keyboardType: TextInputType.emailAddress, decoration: const InputDecoration(labelText: 'E-mail')),
                const SizedBox(height: 8),
                TextField(controller: adultPassword, obscureText: true, decoration: const InputDecoration(labelText: 'Senha')),
                const SizedBox(height: 8),
                TextField(controller: memberName, decoration: const InputDecoration(labelText: 'Nome do membro')),
                const SizedBox(height: 8),
                TextField(controller: memberPin, keyboardType: TextInputType.number, obscureText: true, decoration: const InputDecoration(labelText: 'PIN do membro (4 dígitos)')),
                const SizedBox(height: 8),
                CheckboxListTile(
                  value: consent,
                  contentPadding: EdgeInsets.zero,
                  onChanged: (v) => setDialog(() => consent = v == true),
                  title: const Text('Confirmo que sou responsável pelo perfil e autorizo o tratamento dos dados necessários para usar o PACUS.'),
                ),
              ]),
            ),
          ),
          actions: [
            TextButton(onPressed: () => Navigator.pop(context), child: const Text('Cancelar')),
            FilledButton(
              onPressed: !consent ? null : () => Navigator.pop(context, {
                'adultName': adultName.text.trim(),
                'adultEmail': adultEmail.text.trim(),
                'adultPassword': adultPassword.text,
                'childName': memberName.text.trim(),
                'childPin': memberPin.text.trim(),
                'responsibleConsent': consent,
              }),
              child: const Text('Criar'),
            ),
          ],
        ),
      ),
    );
    adultName.dispose(); adultEmail.dispose(); adultPassword.dispose(); memberName.dispose(); memberPin.dispose();
    if (payload == null) return;

    setState(() { busy = true; error = null; });
    try {
      final result = Map<String, dynamic>.from(await widget.api.request('/bootstrap', method: 'POST', body: payload, authenticated: false) as Map);
      if (!mounted) return;
      await showDialog<void>(
        context: context,
        builder: (context) => AlertDialog(
          title: const Text('Família criada'),
          content: SelectableText(
            'Código da família: ' + (result['familyCode']?.toString() ?? '') +
            '\n\nCódigo de recuperação: ' + (result['recoveryCode']?.toString() ?? '') +
            '\n\nGuarde esses códigos em local seguro.',
          ),
          actions: [TextButton(onPressed: () => Navigator.pop(context), child: const Text('Continuar'))],
        ),
      );
      email.text = payload['adultEmail'].toString();
      password.text = payload['adultPassword'].toString();
      await _loginAdult();
    } catch (e) {
      if (mounted) setState(() => error = e.toString());
    } finally {
      if (mounted) setState(() => busy = false);
    }
  }

  @override Widget build(BuildContext context) => Scaffold(
    body: Center(
      child: SingleChildScrollView(
        padding: const EdgeInsets.all(24),
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 480),
          child: Card(
            child: Padding(
              padding: const EdgeInsets.all(28),
              child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
                const Text('PACUS', textAlign: TextAlign.center, style: TextStyle(fontSize: 38, fontWeight: FontWeight.w900, letterSpacing: 2)),
                const SizedBox(height: 6),
                Text('Rotina com autonomia', textAlign: TextAlign.center, style: TextStyle(color: Theme.of(context).colorScheme.onSurfaceVariant)),
                const SizedBox(height: 28),
                SegmentedButton<bool>(
                  segments: const [
                    ButtonSegment(value: false, icon: Icon(Icons.person_outline), label: Text('Adulto')),
                    ButtonSegment(value: true, icon: Icon(Icons.person_pin_circle_outlined), label: Text('Membro')),
                  ],
                  selected: {memberMode},
                  onSelectionChanged: busy ? null : (s) => setState(() { memberMode = s.first; error = null; }),
                ),
                const SizedBox(height: 24),
                if (!memberMode) ...[
                  TextField(controller: email, keyboardType: TextInputType.emailAddress, autofillHints: const [AutofillHints.email], decoration: const InputDecoration(labelText: 'E-mail')),
                  const SizedBox(height: 14),
                  TextField(controller: password, obscureText: true, autofillHints: const [AutofillHints.password], decoration: const InputDecoration(labelText: 'Senha')),
                  const SizedBox(height: 16),
                  FilledButton(onPressed: busy ? null : _loginAdult, child: const Padding(padding: EdgeInsets.all(14), child: Text('Entrar'))),
                  TextButton(onPressed: busy ? null : _register, child: const Text('Criar uma família')),
                ] else ...[
                  TextField(controller: familyCode, textCapitalization: TextCapitalization.characters, decoration: const InputDecoration(labelText: 'Código da família', hintText: 'ABC-123')),
                  const SizedBox(height: 10),
                  OutlinedButton(onPressed: busy ? null : _findProfiles, child: const Text('Encontrar perfis')),
                  if (profiles.isNotEmpty) ...[
                    const SizedBox(height: 14),
                    const Text('Quem está entrando?', style: TextStyle(fontWeight: FontWeight.w800)),
                    const SizedBox(height: 8),
                    Wrap(
                      spacing: 8, runSpacing: 8,
                      children: profiles.map((profile) => ChoiceChip(
                        label: Text(profile['name']?.toString() ?? 'Perfil'),
                        selected: selectedProfile?['id']?.toString() == profile['id']?.toString(),
                        onSelected: (_) => setState(() => selectedProfile = profile),
                      )).toList(),
                    ),
                    const SizedBox(height: 14),
                    TextField(controller: pin, obscureText: true, keyboardType: TextInputType.number, decoration: const InputDecoration(labelText: 'PIN')),
                    const SizedBox(height: 16),
                    FilledButton(onPressed: busy ? null : _loginMember, child: const Padding(padding: EdgeInsets.all(14), child: Text('Entrar'))),
                  ],
                ],
                if (error != null) ...[
                  const SizedBox(height: 14),
                  Text(error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
                ],
                if (busy) const Padding(padding: EdgeInsets.only(top: 14), child: Center(child: CircularProgressIndicator())),
              ]),
            ),
          ),
        ),
      ),
    ),
  );
}
