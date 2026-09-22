
import 'package:flutter/material.dart';
import 'package:url_launcher/url_launcher.dart';
import '../api.dart';

class SettingsScreen extends StatefulWidget {
  const SettingsScreen({super.key, required this.api, required this.onLogout, required this.themeMode, required this.onThemeChanged});
  final PacusApi api;
  final Future<void> Function() onLogout;
  final ThemeMode themeMode;
  final ValueChanged<ThemeMode> onThemeChanged;
  @override State<SettingsScreen> createState() => _SettingsScreenState();
}

class _SettingsScreenState extends State<SettingsScreen> {
  String familyCode = '';
  String timezone = '';
  bool timerEnabled = false;
  int timerMinutes = 120;
  List<Map<String, dynamic>> members = [];
  List<Map<String, dynamic>> growth = [];
  List<Map<String, dynamic>> tasks = [];
  bool loading = true;
  String? error;

  @override void initState() { super.initState(); _load(); }

  Future<void> _load() async {
    setState(() => loading = true);
    try {
      final code = await widget.api.getMap('/family/code');
      final tz = await widget.api.getMap('/family/timezone');
      final timer = await widget.api.getMap('/settings/game-timer');
      final rawMembers = await widget.api.getList('/family/children');
      final rawGrowth = await widget.api.getList('/settings/growth-stages');
      final rawTasks = await widget.api.getList('/tasks');
      if (!mounted) return;
      setState(() {
        familyCode = code['familyCode']?.toString() ?? '';
        timezone = tz['timezone']?.toString() ?? '';
        timerEnabled = timer['enabled'] == true;
        timerMinutes = (timer['minutes'] as num?)?.toInt() ?? 120;
        members = rawMembers.map((e) => Map<String, dynamic>.from(e as Map)).toList();
        growth = rawGrowth.map((e) => Map<String, dynamic>.from(e as Map)).toList();
        tasks = rawTasks.map((e) => Map<String, dynamic>.from(e as Map)).toList();
        loading = false;
        error = null;
      });
    } catch (e) {
      if (mounted) setState(() { loading = false; error = e.toString(); });
    }
  }

  void _snack(String text) {
    if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(text)));
  }

  Future<String?> _ask(String title, String label, {String initial = '', TextInputType? type}) async {
    final controller = TextEditingController(text: initial);
    final value = await showDialog<String>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text(title),
        content: TextField(controller: controller, keyboardType: type, decoration: InputDecoration(labelText: label)),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context), child: const Text('Cancelar')),
          FilledButton(onPressed: () => Navigator.pop(context, controller.text.trim()), child: const Text('Salvar')),
        ],
      ),
    );
    controller.dispose();
    return value;
  }

  Future<void> _changeTimezone() async {
    final value = await _ask('Fuso horário', 'ID IANA', initial: timezone.isEmpty ? 'America/Sao_Paulo' : timezone);
    if (value == null || value.isEmpty) return;
    try {
      await widget.api.request('/family/timezone', method: 'PUT', body: {'timezone': value});
      await _load();
    } catch (e) { _snack(e.toString()); }
  }

  Future<void> _newRecovery() async {
    try {
      final result = Map<String, dynamic>.from(await widget.api.request('/family/recovery-code', method: 'POST') as Map);
      if (!mounted) return;
      await showDialog<void>(
        context: context,
        builder: (context) => AlertDialog(
          title: const Text('Novo código de recuperação'),
          content: SelectableText(result['recoveryCode']?.toString() ?? ''),
          actions: [TextButton(onPressed: () => Navigator.pop(context), child: const Text('Fechar'))],
        ),
      );
    } catch (e) { _snack(e.toString()); }
  }

  Future<void> _addMember() async {
    final name = await _ask('Adicionar membro', 'Nome');
    if (name == null || name.isEmpty) return;
    final pin = await _ask('Adicionar membro', 'PIN de 4 dígitos', type: TextInputType.number);
    if (pin == null || !RegExp(r'^[0-9]{4}$').hasMatch(pin)) {
      _snack('O PIN deve ter 4 dígitos.');
      return;
    }
    try {
      await widget.api.request('/family/children', method: 'POST', body: {'name': name, 'pin': pin});
      await _load();
    } catch (e) { _snack(e.toString()); }
  }

  Future<void> _changePin(Map<String, dynamic> member) async {
    final pin = await _ask('Trocar PIN', 'Novo PIN de 4 dígitos', type: TextInputType.number);
    if (pin == null || !RegExp(r'^[0-9]{4}$').hasMatch(pin)) {
      if (pin != null) _snack('O PIN deve ter 4 dígitos.');
      return;
    }
    try {
      await widget.api.request('/family/children/' + member['id'].toString() + '/pin', method: 'PUT', body: {'newPin': pin});
      _snack('PIN atualizado.');
    } catch (e) { _snack(e.toString()); }
  }

  Future<void> _saveTimer(bool enabled, int minutes) async {
    try {
      await widget.api.request('/settings/game-timer', method: 'PUT', body: {'enabled': enabled, 'minutes': minutes});
      await _load();
    } catch (e) { _snack(e.toString()); }
  }

  Future<void> _addGrowth() async {
    final stage = await _ask('Novo estágio', 'egg, cracking, hatching, baby, young ou adult', initial: 'young');
    if (stage == null || stage.isEmpty) return;
    final date = await _ask('Novo estágio', 'Data AAAA-MM-DD');
    if (date == null || date.isEmpty) return;
    final updated = [...growth.where((g) => g['date']?.toString() != date), {'stage': stage, 'date': date}];
    try {
      await widget.api.request('/settings/growth-stages', method: 'PUT', body: {'stages': updated});
      await _load();
    } catch (e) { _snack(e.toString()); }
  }

  Future<void> _clearGrowth() async {
    try {
      await widget.api.request('/settings/growth-stages', method: 'PUT', body: {'stages': <dynamic>[]});
      await _load();
    } catch (e) { _snack(e.toString()); }
  }

  Future<void> _editTask([Map<String, dynamic>? task]) async {
    final title = TextEditingController(text: task?['title']?.toString() ?? '');
    final description = TextEditingController(text: task?['description']?.toString() ?? '');
    final points = TextEditingController(text: (task?['points'] ?? 1).toString());
    final minimum = TextEditingController(text: task?['minimumGoalLabel']?.toString() ?? '');
    final options = TextEditingController(text: ((task?['options'] as List?) ?? []).join(', '));
    final reasons = TextEditingController(text: ((task?['reasons'] as List?) ?? []).join(' | '));
    String type = task?['type']?.toString().toLowerCase() ?? 'expected';
    String period = task?['period']?.toString().toLowerCase() ?? 'morning';
    String recurrence = task?['recurrence']?.toString().toLowerCase() ?? 'daily';
    final anchorDate = TextEditingController(text: task?['anchorDate']?.toString() ?? '');
    final intervalDays = TextEditingController(text: (task?['intervalDays'] ?? 2).toString());
    final selectedDays = <String>{
      ...((task?['customDays'] as List?) ?? const []).map((e) => e.toString()),
    };
    String variantTitle(String day) {
      for (final raw in (task?['variants'] as List?) ?? const []) {
        if (raw is Map && raw['dayOfWeek']?.toString().toLowerCase() == day.toLowerCase()) {
          return raw['title']?.toString() ?? '';
        }
      }
      return '';
    }
    final weekdayTitles = <String, TextEditingController>{
      for (final day in const ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday'])
        day: TextEditingController(text: variantTitle(day)),
    };

    final payload = await showDialog<Map<String, dynamic>>(
      context: context,
      builder: (context) => StatefulBuilder(
        builder: (context, setDialog) => AlertDialog(
          title: Text(task == null ? 'Nova tarefa permanente' : 'Editar tarefa'),
          content: SizedBox(
            width: 520,
            child: SingleChildScrollView(
              child: Column(mainAxisSize: MainAxisSize.min, children: [
                TextField(controller: title, decoration: const InputDecoration(labelText: 'Título')),
                const SizedBox(height: 8),
                TextField(controller: description, decoration: const InputDecoration(labelText: 'Descrição')),
                const SizedBox(height: 8),
                TextField(controller: points, keyboardType: TextInputType.number, decoration: const InputDecoration(labelText: 'Pontos')),
                const SizedBox(height: 8),
                DropdownButtonFormField<String>(
                  initialValue: type,
                  decoration: const InputDecoration(labelText: 'Tipo'),
                  items: const [
                    DropdownMenuItem(value: 'mandatory', child: Text('Obrigatória')),
                    DropdownMenuItem(value: 'expected', child: Text('Esperada')),
                    DropdownMenuItem(value: 'challenge', child: Text('Desafio')),
                  ],
                  onChanged: (v) => setDialog(() => type = v ?? type),
                ),
                const SizedBox(height: 8),
                DropdownButtonFormField<String>(
                  initialValue: period,
                  decoration: const InputDecoration(labelText: 'Período'),
                  items: const [
                    DropdownMenuItem(value: 'morning', child: Text('Manhã')),
                    DropdownMenuItem(value: 'afternoon', child: Text('Tarde')),
                    DropdownMenuItem(value: 'evening', child: Text('Noite')),
                  ],
                  onChanged: (v) => setDialog(() => period = v ?? period),
                ),
                const SizedBox(height: 8),
                DropdownButtonFormField<String>(
                  initialValue: recurrence,
                  decoration: const InputDecoration(labelText: 'Recorrência'),
                  items: const [
                    DropdownMenuItem(value: 'daily', child: Text('Todos os dias')),
                    DropdownMenuItem(value: 'interval', child: Text('Intervalo de dias')),
                    DropdownMenuItem(value: 'weekday', child: Text('Dias úteis')),
                    DropdownMenuItem(value: 'weekend', child: Text('Fim de semana')),
                    DropdownMenuItem(value: 'custom', child: Text('Dias específicos')),
                    DropdownMenuItem(value: 'weekday_rotation', child: Text('Atividade diferente por dia útil')),
                  ],
                  onChanged: (v) => setDialog(() => recurrence = v ?? recurrence),
                ),
                if (recurrence == 'interval') ...[
                  const SizedBox(height: 8),
                  TextField(controller: anchorDate, decoration: const InputDecoration(labelText: 'Data âncora AAAA-MM-DD')),
                  const SizedBox(height: 8),
                  TextField(controller: intervalDays, keyboardType: TextInputType.number, decoration: const InputDecoration(labelText: 'Intervalo em dias')),
                ],
                if (recurrence == 'custom') ...[
                  const SizedBox(height: 10),
                  Align(alignment: Alignment.centerLeft, child: Text('Dias da semana', style: const TextStyle(fontWeight: FontWeight.w800))),
                  const SizedBox(height: 6),
                  Wrap(
                    spacing: 6,
                    runSpacing: 6,
                    children: const {
                      'Monday': 'Seg',
                      'Tuesday': 'Ter',
                      'Wednesday': 'Qua',
                      'Thursday': 'Qui',
                      'Friday': 'Sex',
                      'Saturday': 'Sáb',
                      'Sunday': 'Dom',
                    }.entries.map((entry) => FilterChip(
                      label: Text(entry.value),
                      selected: selectedDays.contains(entry.key),
                      onSelected: (selected) => setDialog(() {
                        if (selected) {
                          selectedDays.add(entry.key);
                        } else {
                          selectedDays.remove(entry.key);
                        }
                      }),
                    )).toList(),
                  ),
                ],
                if (recurrence == 'weekday_rotation') ...[
                  const SizedBox(height: 10),
                  const Align(alignment: Alignment.centerLeft, child: Text('Atividade por dia útil', style: TextStyle(fontWeight: FontWeight.w800))),
                  const SizedBox(height: 6),
                  for (final entry in weekdayTitles.entries) ...[
                    TextField(
                      controller: entry.value,
                      decoration: InputDecoration(labelText: _weekdayLabel(entry.key)),
                    ),
                    const SizedBox(height: 6),
                  ],
                ],
                const SizedBox(height: 8),
                TextField(controller: minimum, decoration: const InputDecoration(labelText: 'Meta mínima opcional')),
                const SizedBox(height: 8),
                TextField(controller: options, decoration: const InputDecoration(labelText: 'Opções, separadas por vírgula')),
                const SizedBox(height: 8),
                TextField(controller: reasons, decoration: const InputDecoration(labelText: 'Motivos, separados por |')),
              ]),
            ),
          ),
          actions: [
            TextButton(onPressed: () => Navigator.pop(context), child: const Text('Cancelar')),
            FilledButton(
              onPressed: () => Navigator.pop(context, {
                'title': title.text.trim(),
                'description': description.text.trim().isEmpty ? null : description.text.trim(),
                'type': type,
                'period': period,
                'points': int.tryParse(points.text) ?? 1,
                'recurrence': recurrence,
                'customDays': recurrence == 'custom' ? selectedDays.toList() : null,
                'anchorDate': recurrence == 'interval' && anchorDate.text.trim().isNotEmpty ? anchorDate.text.trim() : null,
                'intervalDays': recurrence == 'interval' ? (int.tryParse(intervalDays.text) ?? 2) : null,
                'variants': recurrence == 'weekday_rotation'
                    ? weekdayTitles.entries
                        .where((entry) => entry.value.text.trim().isNotEmpty)
                        .map((entry) => {
                              'dayOfWeek': entry.key,
                              'title': entry.value.text.trim(),
                              'description': null,
                              'points': null,
                            })
                        .toList()
                    : null,
                'options': options.text.trim().isEmpty ? null : options.text.split(',').map((e) => e.trim()).where((e) => e.isNotEmpty).toList(),
                'reasons': reasons.text.trim().isEmpty ? null : reasons.text.split('|').map((e) => e.trim()).where((e) => e.isNotEmpty).toList(),
                'minimumGoalLabel': minimum.text.trim().isEmpty ? null : minimum.text.trim(),
              }),
              child: const Text('Salvar'),
            ),
          ],
        ),
      ),
    );
    title.dispose(); description.dispose(); points.dispose(); minimum.dispose(); options.dispose(); reasons.dispose(); anchorDate.dispose(); intervalDays.dispose(); for (final controller in weekdayTitles.values) { controller.dispose(); }
    if (payload == null || (payload['title']?.toString() ?? '').isEmpty) return;
    try {
      if (task == null) {
        await widget.api.request('/tasks', method: 'POST', body: payload);
      } else {
        await widget.api.request('/tasks/' + task['id'].toString(), method: 'PUT', body: payload);
      }
      await _load();
    } catch (e) { _snack(e.toString()); }
  }

  Future<void> _deleteTask(Map<String, dynamic> task) async {
    try {
      await widget.api.delete('/tasks/' + task['id'].toString());
      await _load();
    } catch (e) { _snack(e.toString()); }
  }

  Future<void> _openLegal(String path) async {
    final uri = Uri.parse('https://www.pacus.com.br/' + path);
    if (!await launchUrl(uri, mode: LaunchMode.platformDefault)) {
      _snack('Não foi possível abrir a página.');
    }
  }

  String _weekdayLabel(String day) {
    const labels = {
      'Monday': 'Segunda-feira',
      'Tuesday': 'Terça-feira',
      'Wednesday': 'Quarta-feira',
      'Thursday': 'Quinta-feira',
      'Friday': 'Sexta-feira',
    };
    return labels[day] ?? day;
  }

  @override Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Configurações')),
    body: RefreshIndicator(
      onRefresh: _load,
      child: ListView(
        padding: const EdgeInsets.all(18),
        children: [
          if (error != null) Text(error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
          if (loading && familyCode.isEmpty) const Center(child: Padding(padding: EdgeInsets.all(30), child: CircularProgressIndicator())),
          const Text('Aparência', style: TextStyle(fontSize: 22, fontWeight: FontWeight.w900)),
          const SizedBox(height: 8),
          Card(
            child: Padding(
              padding: const EdgeInsets.all(14),
              child: SegmentedButton<ThemeMode>(
                segments: const [
                  ButtonSegment(value: ThemeMode.light, icon: Icon(Icons.light_mode_outlined), label: Text('Diurno')),
                  ButtonSegment(value: ThemeMode.dark, icon: Icon(Icons.dark_mode_outlined), label: Text('Noturno')),
                  ButtonSegment(value: ThemeMode.system, icon: Icon(Icons.brightness_auto_outlined), label: Text('Sistema')),
                ],
                selected: {widget.themeMode},
                onSelectionChanged: (values) => widget.onThemeChanged(values.first),
              ),
            ),
          ),
          const SizedBox(height: 18),
          _tile('Código da família', familyCode.isEmpty ? 'Não disponível' : familyCode, Icons.key_outlined),
          const SizedBox(height: 10),
          _tile('Fuso horário', timezone, Icons.public, action: TextButton(onPressed: _changeTimezone, child: const Text('Alterar'))),
          const SizedBox(height: 10),
          Card(
            child: SwitchListTile(
              value: timerEnabled,
              onChanged: (v) => _saveTimer(v, timerMinutes),
              title: const Text('Tempo de tela', style: TextStyle(fontWeight: FontWeight.w800)),
              subtitle: Text('Limite diário: ' + timerMinutes.toString() + ' minutos'),
              secondary: const Icon(Icons.timer_outlined),
            ),
          ),
          const SizedBox(height: 8),
          Wrap(spacing: 8, children: [60, 90, 120, 180].map((m) => ChoiceChip(
            label: Text(m.toString() + ' min'),
            selected: timerMinutes == m,
            onSelected: (_) => _saveTimer(timerEnabled, m),
          )).toList()),
          const SizedBox(height: 18),
          Row(children: [
            const Expanded(child: Text('Membros da família', style: TextStyle(fontSize: 22, fontWeight: FontWeight.w900))),
            IconButton(onPressed: _addMember, icon: const Icon(Icons.person_add_alt_1), tooltip: 'Adicionar membro'),
          ]),
          for (final member in members)
            Card(
              child: ListTile(
                leading: const CircleAvatar(child: Icon(Icons.person_outline)),
                title: Text(member['name']?.toString() ?? 'Membro'),
                subtitle: const Text('Perfil com acesso por PIN'),
                trailing: TextButton(onPressed: () => _changePin(member), child: const Text('Trocar PIN')),
              ),
            ),
          const SizedBox(height: 18),
          Row(children: [
            const Expanded(child: Text('Calendário do PACUS', style: TextStyle(fontSize: 22, fontWeight: FontWeight.w900))),
            IconButton(onPressed: _addGrowth, icon: const Icon(Icons.add)),
            if (growth.isNotEmpty) IconButton(onPressed: _clearGrowth, icon: const Icon(Icons.delete_outline)),
          ]),
          if (growth.isEmpty) const Text('Nenhum estágio personalizado.'),
          for (final g in growth) ListTile(title: Text(g['stage']?.toString() ?? ''), trailing: Text(g['date']?.toString() ?? '')),
          const SizedBox(height: 18),
          Row(children: [
            const Expanded(child: Text('Tarefas permanentes', style: TextStyle(fontSize: 22, fontWeight: FontWeight.w900))),
            IconButton(onPressed: () => _editTask(), icon: const Icon(Icons.add_task), tooltip: 'Nova tarefa'),
          ]),
          if (tasks.isEmpty) const Text('Nenhuma tarefa permanente.'),
          for (final task in tasks)
            Card(
              child: ListTile(
                title: Text(task['title']?.toString() ?? 'Tarefa', style: const TextStyle(fontWeight: FontWeight.w800)),
                subtitle: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text((task['period'] ?? '').toString() + ' · ' + (task['points'] ?? 0).toString() + ' PP'),
                    if (task['lastModifiedByMember'] == true)
                      const Padding(
                        padding: EdgeInsets.only(top: 4),
                        child: Text(
                          'Alterado pelo membro',
                          style: TextStyle(fontWeight: FontWeight.w800),
                        ),
                      ),
                  ],
                ),
                trailing: Wrap(spacing: 4, children: [
                  IconButton(onPressed: () => _editTask(task), icon: const Icon(Icons.edit_outlined)),
                  IconButton(onPressed: () => _deleteTask(task), icon: const Icon(Icons.delete_outline)),
                ]),
              ),
            ),
          const SizedBox(height: 18),
          const Text('Privacidade e termos', style: TextStyle(fontSize: 22, fontWeight: FontWeight.w900)),
          const SizedBox(height: 8),
          Wrap(spacing: 8, runSpacing: 8, children: [
            OutlinedButton.icon(onPressed: () => _openLegal('privacidade.html'), icon: const Icon(Icons.privacy_tip_outlined), label: const Text('Privacidade')),
            OutlinedButton.icon(onPressed: () => _openLegal('termos.html'), icon: const Icon(Icons.description_outlined), label: const Text('Termos de Uso')),
          ]),
          const SizedBox(height: 18),
          FilledButton.tonalIcon(onPressed: _newRecovery, icon: const Icon(Icons.password), label: const Text('Gerar novo código de recuperação')),
          const SizedBox(height: 10),
          OutlinedButton.icon(onPressed: widget.onLogout, icon: const Icon(Icons.logout), label: const Text('Sair da conta')),
        ],
      ),
    ),
  );

  Widget _tile(String title, String subtitle, IconData icon, {Widget? action}) => Card(
    child: ListTile(
      leading: Icon(icon),
      title: Text(title, style: const TextStyle(fontWeight: FontWeight.w800)),
      subtitle: Text(subtitle),
      trailing: action,
    ),
  );
}
