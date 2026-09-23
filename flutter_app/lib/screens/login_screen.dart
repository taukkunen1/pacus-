
import 'dart:async';

import 'package:flutter/material.dart';
import '../api.dart';
import '../brand.dart';
import '../models.dart';

class LoginScreen extends StatefulWidget {
  const LoginScreen({super.key, required this.api, required this.onLoggedIn, required this.themeMode, required this.onThemeChanged});
  final PacusApi api;
  final ValueChanged<AuthSession> onLoggedIn;
  final ThemeMode themeMode;
  final ValueChanged<ThemeMode> onThemeChanged;
  @override State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
  final email = TextEditingController();
  final password = TextEditingController();
  final familyCode = TextEditingController();
  final pin = TextEditingController();
  bool memberMode = false;
  bool busy = false;
  bool slowBusy = false;
  Timer? slowBusyTimer;
  String? error;
  List<Map<String, dynamic>> profiles = [];
  Map<String, dynamic>? selectedProfile;

  @override
  void dispose() {
    slowBusyTimer?.cancel();
    email.dispose(); password.dispose(); familyCode.dispose(); pin.dispose();
    super.dispose();
  }

  void _beginBusy() {
    slowBusyTimer?.cancel();
    setState(() {
      busy = true;
      slowBusy = false;
      error = null;
    });
    slowBusyTimer = Timer(const Duration(seconds: 4), () {
      if (mounted && busy) setState(() => slowBusy = true);
    });
  }

  void _endBusy() {
    slowBusyTimer?.cancel();
    if (mounted) {
      setState(() {
        busy = false;
        slowBusy = false;
      });
    }
  }

  Future<void> _loginAdult() async {
    _beginBusy();
    try {
      final session = await widget.api.loginAdult(email.text, password.text);
      if (mounted) widget.onLoggedIn(session);
    } catch (e) {
      if (mounted) setState(() => error = e.toString());
    } finally {
      _endBusy();
    }
  }

  Future<void> _findProfiles() async {
    final code = familyCode.text.replaceAll(RegExp(r'[^A-Za-z0-9]'), '').toUpperCase();
    if (code.length != 6) {
      setState(() => error = 'Digite o código de 6 caracteres da família.');
      return;
    }
    final formatted = code.substring(0, 3) + '-' + code.substring(3);
    _beginBusy();
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
      _endBusy();
    }
  }

  Future<void> _loginMember() async {
    final profile = selectedProfile;
    if (profile == null) {
      setState(() => error = 'Escolha seu perfil.');
      return;
    }
    _beginBusy();
    try {
      final session = await widget.api.loginChild(profile['id'].toString(), pin.text);
      if (mounted) widget.onLoggedIn(session);
    } catch (e) {
      if (mounted) setState(() => error = e.toString());
    } finally {
      _endBusy();
    }
  }

  Future<void> _resetPassword() async {
    final resetEmail = TextEditingController(text: email.text.trim());
    final recovery = TextEditingController();
    final newPassword = TextEditingController();

    final payload = await showDialog<Map<String, dynamic>>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Redefinir senha'),
        content: SizedBox(
          width: 460,
          child: Column(mainAxisSize: MainAxisSize.min, children: [
            TextField(controller: resetEmail, keyboardType: TextInputType.emailAddress, decoration: const InputDecoration(labelText: 'E-mail')),
            const SizedBox(height: 8),
            TextField(controller: recovery, decoration: const InputDecoration(labelText: 'Código de recuperação')),
            const SizedBox(height: 8),
            TextField(controller: newPassword, obscureText: true, decoration: const InputDecoration(labelText: 'Nova senha')),
          ]),
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context), child: const Text('Cancelar')),
          FilledButton(
            onPressed: () => Navigator.pop(context, {
              'email': resetEmail.text.trim(),
              'recoveryCode': recovery.text.trim(),
              'newPassword': newPassword.text,
            }),
            child: const Text('Redefinir'),
          ),
        ],
      ),
    );

    resetEmail.dispose(); recovery.dispose(); newPassword.dispose();
    if (payload == null) return;

    _beginBusy();
    try {
      final result = Map<String, dynamic>.from(
        await widget.api.request('/auth/adult/reset-password', method: 'POST', body: payload, authenticated: false) as Map,
      );
      if (!mounted) return;
      await showDialog<void>(
        context: context,
        builder: (context) => AlertDialog(
          title: const Text('Senha atualizada'),
          content: SelectableText(
            (result['message']?.toString() ?? 'Senha redefinida.') +
            '\n\nNovo código de recuperação: ' +
            (result['newRecoveryCode']?.toString() ?? result['recoveryCode']?.toString() ?? ''),
          ),
          actions: [TextButton(onPressed: () => Navigator.pop(context), child: const Text('Entendi'))],
        ),
      );
      email.text = payload['email'].toString();
    } catch (e) {
      if (mounted) setState(() => error = e.toString());
    } finally {
      _endBusy();
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

    _beginBusy();
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
      _endBusy();
    }
  }

  @override Widget build(BuildContext context) => Scaffold(
    body: Stack(
      children: [
        Positioned(
          top: 12,
          right: 12,
          child: SafeArea(
            child: PopupMenuButton<ThemeMode>(
              tooltip: 'Aparência',
              initialValue: widget.themeMode,
              onSelected: widget.onThemeChanged,
              icon: Icon(
                widget.themeMode == ThemeMode.dark
                    ? Icons.dark_mode_outlined
                    : widget.themeMode == ThemeMode.light
                        ? Icons.light_mode_outlined
                        : Icons.brightness_auto_outlined,
              ),
              itemBuilder: (_) => const [
                PopupMenuItem(value: ThemeMode.light, child: Text('Modo diurno')),
                PopupMenuItem(value: ThemeMode.dark, child: Text('Modo noturno')),
                PopupMenuItem(value: ThemeMode.system, child: Text('Seguir dispositivo')),
              ],
            ),
          ),
        ),
        Center(
      child: SingleChildScrollView(
        padding: const EdgeInsets.all(24),
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 480),
          child: Card(
            child: Padding(
              padding: const EdgeInsets.all(28),
              child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
                const Center(child: PacusBrand()),
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
                  TextButton(onPressed: busy ? null : _resetPassword, child: const Text('Esqueci minha senha')),
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
                if (busy) ...[
                  const Padding(
                    padding: EdgeInsets.only(top: 14),
                    child: Center(child: CircularProgressIndicator()),
                  ),
                  if (slowBusy)
                    Padding(
                      padding: const EdgeInsets.only(top: 12),
                      child: Text(
                        'Ainda carregando... o servidor pode estar iniciando depois de um tempo parado. Isso pode levar mais alguns segundos na primeira tentativa.',
                        textAlign: TextAlign.center,
                        style: TextStyle(color: Theme.of(context).colorScheme.onSurfaceVariant),
                      ),
                    ),
                ],
              ]),
            ),
          ),
        ),
      ),
    ),
      ],
    ),
  );
}
