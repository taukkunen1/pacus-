import 'package:flutter/material.dart';

import '../api.dart';
import '../models.dart';

class TomorrowScreen extends StatefulWidget {
  const TomorrowScreen({
    super.key,
    required this.api,
    required this.session,
  });

  final PacusApi api;
  final AuthSession session;

  @override
  State<TomorrowScreen> createState() => _TomorrowScreenState();
}

class _TomorrowScreenState extends State<TomorrowScreen> {
  DailyRoutine? routine;
  List<Map<String, dynamic>> permanentTasks = [];
  List<Map<String, dynamic>> suggestions = [];
  bool loading = true;
  String? error;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() => loading = true);
    try {
      final data = await widget.api.getMap('/daily-routines/tomorrow');
      var routineItems = <Map<String, dynamic>>[];
      var smart = <Map<String, dynamic>>[];

      if (!widget.session.isAdult) {
        final rawRoutine = await widget.api.getList('/autonomy/routine');
        routineItems = rawRoutine.map((e) => Map<String, dynamic>.from(e as Map)).toList();
        final rawSuggestions = await widget.api.getList('/autonomy/suggestions');
        smart = rawSuggestions.map((e) => Map<String, dynamic>.from(e as Map)).toList();
      }

      if (!mounted) return;
      setState(() {
        routine = DailyRoutine.fromJson(data);
        permanentTasks = routineItems;
        suggestions = smart;
        error = null;
        loading = false;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        error = e.toString();
        loading = false;
      });
    }
  }

  int get _memberCreatedCount =>
      routine?.tasks.where((t) => !t.isDeleted && t.createdByMember).length ?? 0;

  bool get _canAddOwn => widget.session.isAdult || _memberCreatedCount < 3;

  Future<void> _quickIdea(String title) async {
    await _editTask(initialTitle: title);
  }

  Future<void> _editTask({
    DailyTask? task,
    String? initialTitle,
  }) async {
    final title = TextEditingController(text: task?.title ?? initialTitle ?? '');
    final description = TextEditingController(text: task?.description ?? '');
    final planCue = TextEditingController(text: task?.planCue ?? '');
    String period = task?.period.toLowerCase() ?? 'afternoon';

    final payload = await showDialog<Map<String, dynamic>>(
      context: context,
      builder: (context) => StatefulBuilder(
        builder: (context, setDialog) => AlertDialog(
          title: Text(task == null ? 'Criar para amanhã' : 'Editar ideia'),
          content: SizedBox(
            width: 500,
            child: SingleChildScrollView(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  TextField(
                    controller: title,
                    autofocus: task == null,
                    decoration: const InputDecoration(
                      labelText: 'O que você quer fazer?',
                      hintText: 'Ex.: desenhar um personagem',
                    ),
                  ),
                  const SizedBox(height: 10),
                  TextField(
                    controller: description,
                    decoration: const InputDecoration(
                      labelText: 'Descrição opcional',
                    ),
                  ),
                  const SizedBox(height: 10),
                  DropdownButtonFormField<String>(
                    initialValue: period,
                    decoration: const InputDecoration(labelText: 'Quando?'),
                    items: const [
                      DropdownMenuItem(value: 'morning', child: Text('Manhã')),
                      DropdownMenuItem(value: 'afternoon', child: Text('Tarde')),
                      DropdownMenuItem(value: 'evening', child: Text('Noite')),
                    ],
                    onChanged: (value) => setDialog(() => period = value ?? period),
                  ),
                  const SizedBox(height: 10),
                  TextField(
                    controller: planCue,
                    decoration: const InputDecoration(
                      labelText: 'Depois de quê você vai fazer isso?',
                      hintText: 'Ex.: depois do almoço',
                    ),
                  ),
                ],
              ),
            ),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(context),
              child: const Text('Cancelar'),
            ),
            FilledButton(
              onPressed: () {
                final normalized = title.text.trim();
                if (normalized.isEmpty) return;
                Navigator.pop(context, {
                  'title': normalized,
                  'description': description.text.trim().isEmpty ? null : description.text.trim(),
                  'period': period,
                  'planCue': planCue.text.trim().isEmpty ? null : planCue.text.trim(),
                });
              },
              child: Text(task == null ? 'Adicionar' : 'Salvar'),
            ),
          ],
        ),
      ),
    );

    title.dispose();
    description.dispose();
    planCue.dispose();
    if (payload == null) return;

    try {
      final path = task == null
          ? '/daily-routines/tomorrow/tasks'
          : '/daily-routines/tomorrow/tasks/${task.id}';
      final method = task == null ? 'POST' : 'PUT';
      final data = await widget.api.request(path, method: method, body: payload);
      if (!mounted) return;
      setState(() => routine = DailyRoutine.fromJson(Map<String, dynamic>.from(data as Map)));
    } catch (e) {
      _snack(e.toString());
    }
  }

  Future<void> _deleteTask(DailyTask task) async {
    try {
      final data = await widget.api.request(
        '/daily-routines/tomorrow/tasks/${task.id}',
        method: 'DELETE',
      );
      if (!mounted) return;
      setState(() => routine = DailyRoutine.fromJson(Map<String, dynamic>.from(data as Map)));
    } catch (e) {
      _snack(e.toString());
    }
  }

  Future<void> _move(DailyTask task, int delta) async {
    final r = routine;
    if (r == null) return;

    final active = r.tasks.where((t) => !t.isDeleted).toList()
      ..sort((a, b) => a.order.compareTo(b.order));
    final samePeriod = active
        .where((t) => t.period.toLowerCase() == task.period.toLowerCase())
        .toList();

    final index = samePeriod.indexWhere((t) => t.id == task.id);
    final target = index + delta;
    if (index < 0 || target < 0 || target >= samePeriod.length) return;

    final moving = samePeriod.removeAt(index);
    samePeriod.insert(target, moving);

    final ordered = <DailyTask>[];
    for (final period in const ['morning', 'afternoon', 'evening']) {
      if (period == task.period.toLowerCase()) {
        ordered.addAll(samePeriod);
      } else {
        ordered.addAll(active.where((t) => t.period.toLowerCase() == period));
      }
    }

    try {
      final data = await widget.api.request(
        '/daily-routines/tomorrow/order',
        method: 'PUT',
        body: ordered.map((t) => t.id).toList(),
      );
      if (!mounted) return;
      setState(() => routine = DailyRoutine.fromJson(Map<String, dynamic>.from(data as Map)));
    } catch (e) {
      _snack(e.toString());
    }
  }

  Future<void> _editPermanentTask(
    Map<String, dynamic> task, {
    String? suggestedPeriod,
  }) async {
    final title = TextEditingController(text: task['title']?.toString() ?? '');
    final description = TextEditingController(text: task['description']?.toString() ?? '');
    final reason = TextEditingController();
    String period = suggestedPeriod ?? task['period']?.toString().toLowerCase() ?? 'afternoon';

    final payload = await showDialog<Map<String, dynamic>>(
      context: context,
      builder: (context) => StatefulBuilder(
        builder: (context, setDialog) => AlertDialog(
          title: const Text('Mudar minha rotina'),
          content: SizedBox(
            width: 500,
            child: SingleChildScrollView(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  const Align(
                    alignment: Alignment.centerLeft,
                    child: Text(
                      'Sua mudança entra na rotina assim que você salvar.',
                      style: TextStyle(fontWeight: FontWeight.w700),
                    ),
                  ),
                  const SizedBox(height: 12),
                  TextField(
                    controller: title,
                    decoration: const InputDecoration(labelText: 'Nome da tarefa'),
                  ),
                  const SizedBox(height: 10),
                  TextField(
                    controller: description,
                    decoration: const InputDecoration(labelText: 'Como você quer fazer?'),
                  ),
                  const SizedBox(height: 10),
                  DropdownButtonFormField<String>(
                    initialValue: period,
                    decoration: const InputDecoration(labelText: 'Quando?'),
                    items: const [
                      DropdownMenuItem(value: 'morning', child: Text('Manhã')),
                      DropdownMenuItem(value: 'afternoon', child: Text('Tarde')),
                      DropdownMenuItem(value: 'evening', child: Text('Noite')),
                    ],
                    onChanged: (value) => setDialog(() => period = value ?? period),
                  ),
                  const SizedBox(height: 10),
                  TextField(
                    controller: reason,
                    decoration: const InputDecoration(
                      labelText: 'Por que você quer mudar? (opcional)',
                    ),
                  ),
                ],
              ),
            ),
          ),
          actions: [
            TextButton(onPressed: () => Navigator.pop(context), child: const Text('Cancelar')),
            FilledButton(
              onPressed: () {
                if (title.text.trim().isEmpty) return;
                Navigator.pop(context, {
                  'title': title.text.trim(),
                  'description': description.text.trim().isEmpty ? null : description.text.trim(),
                  'period': period,
                  'reason': reason.text.trim().isEmpty ? null : reason.text.trim(),
                });
              },
              child: const Text('Salvar mudança'),
            ),
          ],
        ),
      ),
    );

    title.dispose();
    description.dispose();
    reason.dispose();
    if (payload == null) return;

    try {
      await widget.api.request(
        '/autonomy/routine/' + task['id'].toString(),
        method: 'PUT',
        body: payload,
      );
      await _load();
      _snack('Mudança salva na sua rotina.');
    } catch (e) {
      _snack(e.toString());
    }
  }

  Future<void> _useSuggestion(Map<String, dynamic> suggestion) async {
    final kind = suggestion['kind']?.toString() ?? '';
    if (kind == 'own-idea') {
      final title = suggestion['suggestedTitle']?.toString();
      if (title != null && title.isNotEmpty) {
        await _quickIdea(title);
      }
      return;
    }

    if (kind == 'routine-change') {
      final templateId = suggestion['taskTemplateId']?.toString();
      if (templateId == null || templateId.isEmpty) return;
      final task = permanentTasks.cast<Map<String, dynamic>?>().firstWhere(
        (t) => t?['id']?.toString() == templateId,
        orElse: () => null,
      );
      if (task == null) return;
      await _editPermanentTask(
        task,
        suggestedPeriod: suggestion['suggestedPeriod']?.toString().toLowerCase(),
      );
    }
  }

  Future<void> _confirm() async {
    try {
      final data = await widget.api.request(
        '/daily-routines/tomorrow/confirm',
        method: 'POST',
      );
      if (!mounted) return;
      setState(() => routine = DailyRoutine.fromJson(Map<String, dynamic>.from(data as Map)));
      _snack('Seu amanhã foi salvo.');
    } catch (e) {
      _snack(e.toString());
    }
  }

  void _snack(String text) {
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(text)));
  }

  @override
  Widget build(BuildContext context) {
    if (loading) {
      return const Scaffold(body: Center(child: CircularProgressIndicator()));
    }

    final r = routine;
    if (r == null) {
      return Scaffold(
        appBar: AppBar(title: const Text('Meu amanhã')),
        body: Center(
          child: FilledButton(
            onPressed: _load,
            child: Text(error ?? 'Tentar novamente'),
          ),
        ),
      );
    }

    final tasks = r.tasks.where((t) => !t.isDeleted).toList()
      ..sort((a, b) => a.order.compareTo(b.order));

    return Scaffold(
      appBar: AppBar(title: const Text('Meu amanhã')),
      body: RefreshIndicator(
        onRefresh: _load,
        child: ListView(
          padding: const EdgeInsets.fromLTRB(18, 8, 18, 40),
          children: [
            _introCard(r),
            const SizedBox(height: 16),
            _ideaPicker(),
            if (!widget.session.isAdult && suggestions.isNotEmpty) ...[
              const SizedBox(height: 18),
              _suggestionsCard(),
            ],
            if (!widget.session.isAdult && permanentTasks.isNotEmpty) ...[
              const SizedBox(height: 18),
              _permanentRoutineCard(),
            ],
            const SizedBox(height: 18),
            _periodSection('Manhã', Icons.wb_sunny_outlined,
                tasks.where((t) => t.period.toLowerCase() == 'morning').toList()),
            const SizedBox(height: 12),
            _periodSection('Tarde', Icons.light_mode_outlined,
                tasks.where((t) => t.period.toLowerCase() == 'afternoon').toList()),
            const SizedBox(height: 12),
            _periodSection('Noite', Icons.nightlight_round,
                tasks.where((t) => t.period.toLowerCase() == 'evening').toList()),
            const SizedBox(height: 18),
            FilledButton.icon(
              onPressed: _confirm,
              icon: Icon(r.tomorrowPlanConfirmedAt == null
                  ? Icons.check_circle_outline
                  : Icons.verified_rounded),
              label: Text(r.tomorrowPlanConfirmedAt == null
                  ? 'Salvar meu amanhã'
                  : 'Amanhã planejado'),
            ),
          ],
        ),
      ),
    );
  }

  Widget _introCard(DailyRoutine r) {
    final scheme = Theme.of(context).colorScheme;
    return Card(
      color: scheme.primaryContainer,
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text(
              'Monte seu próprio amanhã',
              style: TextStyle(fontSize: 24, fontWeight: FontWeight.w900),
            ),
            const SizedBox(height: 6),
            const Text(
              'Escolha uma coisa sua, decida quando fazer e organize a ordem. Você pode mudar de ideia.',
            ),
            const SizedBox(height: 10),
            Text(
              widget.session.isAdult
                  ? 'Plano para ${r.date}'
                  : 'Você pode criar até 3 coisas suas. $_memberCreatedCount/3 escolhidas.',
              style: const TextStyle(fontWeight: FontWeight.w700),
            ),
          ],
        ),
      ),
    );
  }

  Widget _ideaPicker() {
    final enabled = _canAddOwn;
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(18),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            const Text(
              'Escolha uma ou crie a sua',
              style: TextStyle(fontSize: 19, fontWeight: FontWeight.w900),
            ),
            const SizedBox(height: 10),
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: [
                _ideaButton('🎨 Desenhar', enabled),
                _ideaButton('🧱 Construir', enabled),
                _ideaButton('📖 Ler', enabled),
                _ideaButton('💡 Criar', enabled),
                OutlinedButton.icon(
                  onPressed: enabled ? () => _editTask() : null,
                  icon: const Icon(Icons.add),
                  label: const Text('Minha ideia'),
                ),
              ],
            ),
            if (!enabled && !widget.session.isAdult) ...[
              const SizedBox(height: 10),
              const Text(
                'Você já escolheu 3 coisas suas. Se quiser outra, edite ou remova uma delas.',
              ),
            ],
          ],
        ),
      ),
    );
  }

  Widget _ideaButton(String label, bool enabled) {
    final title = label.replaceFirst(RegExp(r'^\S+\s*'), '');
    return FilledButton.tonal(
      onPressed: enabled ? () => _quickIdea(title) : null,
      child: Text(label),
    );
  }

  Widget _suggestionsCard() {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(18),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            const Row(
              children: [
                Icon(Icons.auto_awesome),
                SizedBox(width: 8),
                Expanded(
                  child: Text(
                    'Ideias do PACUS',
                    style: TextStyle(fontSize: 19, fontWeight: FontWeight.w900),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 6),
            const Text('São só sugestões. Nada muda até você escolher usar uma delas.'),
            const SizedBox(height: 10),
            for (final suggestion in suggestions)
              Card(
                child: ListTile(
                  title: Text(
                    suggestion['title']?.toString() ?? 'Sugestão',
                    style: const TextStyle(fontWeight: FontWeight.w800),
                  ),
                  subtitle: Text(suggestion['message']?.toString() ?? ''),
                  trailing: suggestion['kind']?.toString() == 'insight'
                      ? null
                      : TextButton(
                          onPressed: () => _useSuggestion(suggestion),
                          child: const Text('Usar'),
                        ),
                ),
              ),
          ],
        ),
      ),
    );
  }

  Widget _permanentRoutineCard() {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(18),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            const Text(
              'Minha rotina permanente',
              style: TextStyle(fontSize: 19, fontWeight: FontWeight.w900),
            ),
            const SizedBox(height: 6),
            const Text(
              'Você pode mudar nome, descrição e horário. A mudança fica salva na rotina.',
            ),
            const SizedBox(height: 10),
            for (final task in permanentTasks)
              ListTile(
                contentPadding: EdgeInsets.zero,
                leading: const CircleAvatar(child: Icon(Icons.repeat_rounded)),
                title: Text(
                  task['title']?.toString() ?? 'Tarefa',
                  style: const TextStyle(fontWeight: FontWeight.w800),
                ),
                subtitle: Text(_periodName(task['period']?.toString())),
                trailing: FilledButton.tonal(
                  onPressed: () => _editPermanentTask(task),
                  child: const Text('Mudar'),
                ),
              ),
          ],
        ),
      ),
    );
  }

  String _periodName(String? raw) {
    switch (raw?.toLowerCase()) {
      case 'morning':
        return 'Manhã';
      case 'evening':
        return 'Noite';
      default:
        return 'Tarde';
    }
  }

  Widget _periodSection(String title, IconData icon, List<DailyTask> tasks) {
    final scheme = Theme.of(context).colorScheme;
    return Card(
      color: title == 'Manhã'
          ? scheme.secondaryContainer
          : title == 'Noite'
              ? scheme.tertiaryContainer
              : scheme.primaryContainer,
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Row(
              children: [
                Icon(icon),
                const SizedBox(width: 8),
                Expanded(
                  child: Text(
                    title,
                    style: const TextStyle(fontSize: 20, fontWeight: FontWeight.w900),
                  ),
                ),
                Text('${tasks.length}'),
              ],
            ),
            const SizedBox(height: 10),
            if (tasks.isEmpty)
              const Text('Nada planejado aqui ainda.')
            else
              for (var i = 0; i < tasks.length; i++) ...[
                _taskTile(
                  tasks[i],
                  canMoveUp: i > 0,
                  canMoveDown: i < tasks.length - 1,
                ),
                if (i < tasks.length - 1) const Divider(),
              ],
          ],
        ),
      ),
    );
  }

  Widget _taskTile(
    DailyTask task, {
    required bool canMoveUp,
    required bool canMoveDown,
  }) {
    final canEdit = widget.session.isAdult || task.createdByMember;

    return ListTile(
      contentPadding: EdgeInsets.zero,
      leading: CircleAvatar(
        child: Icon(task.createdByMember ? Icons.auto_awesome : Icons.handshake_outlined),
      ),
      title: Row(
        children: [
          Expanded(
            child: Text(task.title, style: const TextStyle(fontWeight: FontWeight.w800)),
          ),
          if (task.createdByMember)
            const Chip(label: Text('Criado por mim')),
        ],
      ),
      subtitle: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          if ((task.description ?? '').isNotEmpty) Text(task.description!),
          if ((task.planCue ?? '').isNotEmpty)
            Padding(
              padding: const EdgeInsets.only(top: 4),
              child: Text('Depois de: ${task.planCue}'),
            ),
          if (!task.createdByMember)
            const Padding(
              padding: EdgeInsets.only(top: 4),
              child: Text('Combinado da rotina'),
            ),
        ],
      ),
      trailing: PopupMenuButton<String>(
        onSelected: (value) {
          if (value == 'up') _move(task, -1);
          if (value == 'down') _move(task, 1);
          if (value == 'edit') _editTask(task: task);
          if (value == 'delete') _deleteTask(task);
        },
        itemBuilder: (_) => [
          if (canMoveUp)
            const PopupMenuItem(value: 'up', child: Text('Mover para cima')),
          if (canMoveDown)
            const PopupMenuItem(value: 'down', child: Text('Mover para baixo')),
          if (canEdit)
            const PopupMenuItem(value: 'edit', child: Text('Editar')),
          if (canEdit)
            const PopupMenuItem(value: 'delete', child: Text('Remover')),
        ],
      ),
    );
  }
}
