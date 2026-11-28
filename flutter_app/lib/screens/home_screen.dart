import 'dart:async';
import 'dart:math' as math;

import 'package:flutter/material.dart';

import '../api.dart';
import '../brand.dart';
import '../models.dart';
import '../ui/effort_messages.dart';
import '../ui/pacus_components.dart';

class HomeScreen extends StatefulWidget {
  const HomeScreen({
    super.key,
    required this.api,
    required this.session,
    required this.onLogout,
    this.onPendingChanged,
  });
  final PacusApi api;
  final AuthSession session;
  final Future<void> Function() onLogout;
  final ValueChanged<int>? onPendingChanged;

  @override
  State<HomeScreen> createState() => _HomeScreenState();
}

class _HomeScreenState extends State<HomeScreen> with WidgetsBindingObserver {
  DailyRoutine? routine;
  bool loading = true;
  String? error;
  bool slowLoading = false;
  Timer? slowLoadTimer;
  Timer? dayBoundaryTimer;
  late String lastDayKey;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
    lastDayKey = _dayKey();
    _scheduleNextDayBoundary();
    _load();
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    slowLoadTimer?.cancel();
    dayBoundaryTimer?.cancel();
    super.dispose();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed) {
      final previousDayKey = lastDayKey;
      _checkDayBoundary();
      if (previousDayKey == lastDayKey) {
        _load();
      }
    }
  }

  String _dayKey() {
    final now = DateTime.now();
    return '${now.year}-${now.month}-${now.day}';
  }

  void _checkDayBoundary() {
    final key = _dayKey();
    if (key == lastDayKey) return;
    lastDayKey = key;
    _load();
  }

  void _scheduleNextDayBoundary() {
    dayBoundaryTimer?.cancel();
    final now = DateTime.now();
    final next = DateTime(now.year, now.month, now.day + 1)
        .add(const Duration(seconds: 5));
    dayBoundaryTimer = Timer(next.difference(now), () {
      _checkDayBoundary();
      _scheduleNextDayBoundary();
    });
  }

  Future<void> _load() async {
    if (mounted && routine == null) {
      setState(() {
        loading = true;
        slowLoading = false;
      });
    }

    slowLoadTimer?.cancel();
    slowLoadTimer = Timer(const Duration(seconds: 4), () {
      if (mounted && loading) setState(() => slowLoading = true);
    });

    try {
      final value = await widget.api.getToday();
      routine = value;
      widget.onPendingChanged?.call(
        widget.session.isAdult
            ? 0
            : math.max(
                0,
                value.totalTasks - value.doneTasks,
              ),
      );
      if (mounted) {
        setState(() {
          loading = false;
          slowLoading = false;
          error = null;
        });
      }
    } catch (e) {
      if (mounted) {
        setState(() {
          loading = false;
          slowLoading = false;
          error = e.toString();
        });
      }
    } finally {
      slowLoadTimer?.cancel();
    }
  }

  Future<void> _toggleTask(DailyTask task) async {
    final completingTask = !task.isDone;
    try {
      if (task.isDone) {
        await widget.api.reopenTask(task.id);
      } else {
        await widget.api.completeTask(task.id);
      }
      final updated = await widget.api.getToday();
      widget.onPendingChanged?.call(
        widget.session.isAdult ? 0 : math.max(0, updated.totalTasks - updated.doneTasks),
      );
      if (!mounted) return;
      setState(() => routine = updated);

      if (completingTask && !widget.session.isAdult) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text(pickEffortMessage(task, updated.tasks))),
        );
      }
    } catch (e) {
      if (mounted) setState(() => error = e.toString());
    }
  }

  Future<void> _moveTaskWithinPeriod(DailyTask task, int delta) async {
    final r = routine;
    if (r == null) return;
    final active = r.tasks.where((t) => !t.isDeleted).toList()
      ..sort((a, b) => a.order.compareTo(b.order));
    final samePeriod = active.where((t) => t.period.toLowerCase() == task.period.toLowerCase()).toList();
    final index = samePeriod.indexWhere((t) => t.id == task.id);
    final target = index + delta;
    if (index < 0 || target < 0 || target >= samePeriod.length) return;

    final moving = samePeriod[index];
    samePeriod[index] = samePeriod[target];
    samePeriod[target] = moving;

    final rebuilt = <DailyTask>[];
    for (final period in const ['morning', 'afternoon', 'evening']) {
      final items = period == task.period.toLowerCase()
          ? samePeriod
          : active.where((t) => t.period.toLowerCase() == period).toList();
      rebuilt.addAll(items);
    }

    try {
      await widget.api.request(
        '/daily-routines/today/order',
        method: 'PUT',
        body: rebuilt.map((t) => t.id).toList(),
      );
      final updated = await widget.api.getToday();
      if (mounted) setState(() => routine = updated);
    } catch (e) {
      if (mounted) setState(() => error = e.toString());
    }
  }

  Future<void> _createDailyTask() async {
    final title = TextEditingController();
    final description = TextEditingController();
    final points = TextEditingController(text: '1');
    String repetition = 'today';
    String period = 'morning';
    final payload = await showDialog<Map<String, dynamic>>(
      context: context,
      builder: (context) => StatefulBuilder(
        builder: (context, setDialog) => AlertDialog(
          title: const Text('Nova tarefa'),
          scrollable: true,
          content: SizedBox(
            width: 460,
            child: Column(mainAxisSize: MainAxisSize.min, children: [
              TextField(controller: title, decoration: const InputDecoration(labelText: 'Título')),
              const SizedBox(height: 10),
              TextField(controller: description, minLines: 2, maxLines: 4, decoration: const InputDecoration(labelText: 'Descrição (opcional)')),
              const SizedBox(height: 10),
              DropdownButtonFormField<String>(
                initialValue: repetition,
                decoration: const InputDecoration(labelText: 'Repetição'),
                items: const [
                  DropdownMenuItem(value: 'today', child: Text('Só hoje')),
                  DropdownMenuItem(value: 'daily', child: Text('A cada 1 dia (todos os dias)')),
                  DropdownMenuItem(value: 'interval', child: Text('A cada 2 dias (dia sim, dia não)')),
                ],
                onChanged: (v) => setDialog(() => repetition = v ?? 'today'),
              ),
              const SizedBox(height: 10),
              TextField(controller: points, keyboardType: TextInputType.number, decoration: const InputDecoration(labelText: 'Pontos (1–10; sem desconto)')),
              const SizedBox(height: 10),
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
              const SizedBox(height: 10),
            ]),
          ),
          actions: [
            TextButton(onPressed: () => Navigator.pop(context), child: const Text('Cancelar')),
            FilledButton(onPressed: () {
              final value = int.tryParse(points.text);
              if (value == null || value < 1 || value > 10) {
                ScaffoldMessenger.of(context).showSnackBar(
                  const SnackBar(content: Text('Use de 1 a 10 pontos. Tarefas não descontam pontos.')),
                );
                return;
              }
              Navigator.pop(context, {
                'title': title.text.trim(),
                'description': description.text.trim().isEmpty ? null : description.text.trim(),
                'permanent': repetition != 'today',
                'recurrence': repetition == 'interval' ? 'interval' : 'daily',
                'intervalDays': repetition == 'interval' ? 2 : null,
                'type': 'mandatory',
                'period': period,
                'points': value,
              });
            }, child: const Text('Adicionar')),
          ],
        ),
      ),
    );
    title.dispose();
    description.dispose();
    points.dispose();
    if (payload == null || (payload['title']?.toString() ?? '').isEmpty) return;
    try {
      await widget.api.request('/daily-tasks', method: 'POST', body: payload);
      final updated = await widget.api.getToday();
      if (mounted) setState(() => routine = updated);
    } catch (e) {
      if (mounted) setState(() => error = e.toString());
    }
  }

  Future<void> _deleteDailyTask(DailyTask task) async {
    final permanent = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Excluir tarefa'),
        content: Text('Como deseja excluir "${task.title}"? A opção para sempre remove também dos próximos dias.'),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context), child: const Text('Cancelar')),
          TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Só hoje')),
          FilledButton(onPressed: () => Navigator.pop(context, true), child: const Text('Para sempre')),
        ],
      ),
    );
    if (permanent == null) return;
    try {
      await widget.api.delete('/daily-tasks/${task.id}?permanent=$permanent');
      final updated = await widget.api.getToday();
      if (mounted) setState(() => routine = updated);
    } catch (e) {
      if (mounted) setState(() => error = e.toString());
    }
  }

  Future<void> _selectOption(DailyTask task, String option) async {
    try {
      await widget.api.request('/daily-tasks/' + task.id + '/option', method: 'PUT', body: {'selectedOption': option});
      final updated = await widget.api.getToday();
      if (mounted) setState(() => routine = updated);
    } catch (e) {
      if (mounted) setState(() => error = e.toString());
    }
  }

  Future<void> _editDailyTask(DailyTask task) async {
    final title = TextEditingController(text: task.title);
    final description = TextEditingController(text: task.description ?? '');
    final points = TextEditingController(text: task.points > 0 ? task.points.toString() : '1');
    String period = task.period;
    final payload = await showDialog<Map<String, dynamic>>(
      context: context,
      builder: (context) => StatefulBuilder(
        builder: (context, setDialog) => AlertDialog(
          title: const Text('Editar tarefa de hoje'),
          content: SizedBox(
            width: 460,
            child: Column(mainAxisSize: MainAxisSize.min, children: [
              TextField(controller: title, decoration: const InputDecoration(labelText: 'Título')),
              const SizedBox(height: 8),
              TextField(controller: description, decoration: const InputDecoration(labelText: 'Descrição')),
              const SizedBox(height: 8),
              TextField(controller: points, keyboardType: TextInputType.number, decoration: const InputDecoration(labelText: 'Pontos (1–10; sem desconto)')),
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
            ]),
          ),
          actions: [
            TextButton(onPressed: () => Navigator.pop(context), child: const Text('Cancelar')),
            FilledButton(
              onPressed: () {
                final value = int.tryParse(points.text);
                if (value == null || value < 1 || value > 10) {
                  ScaffoldMessenger.of(context).showSnackBar(
                    const SnackBar(content: Text('Use de 1 a 10 pontos. Tarefas não descontam pontos.')),
                  );
                  return;
                }
                Navigator.pop(context, {
                  'title': title.text.trim(),
                  'description': description.text.trim().isEmpty ? null : description.text.trim(),
                  'type': 'mandatory',
                  'period': period,
                  'points': value,
                  'options': task.options,
                  'reason': title.text.trim() == task.title.trim() ? task.reason : null,
                });
              },
              child: const Text('Salvar'),
            ),
          ],
        ),
      ),
    );
    title.dispose(); description.dispose(); points.dispose();
    if (payload == null) return;
    try {
      await widget.api.request('/daily-tasks/' + task.id, method: 'PUT', body: payload);
      final updated = await widget.api.getToday();
      if (mounted) setState(() => routine = updated);
    } catch (e) {
      if (mounted) setState(() => error = e.toString());
    }
  }

  Future<void> _setInitiative(DailyTask task) async {
    final value = await showDialog<String>(
      context: context,
      builder: (context) => SimpleDialog(
        title: const Text('Como você começou?'),
        children: [
          SimpleDialogOption(onPressed: () => Navigator.pop(context, 'selfStarted'), child: const Text('🟢 Percebi e comecei por conta própria')),
          SimpleDialogOption(onPressed: () => Navigator.pop(context, 'promptedByPacus'), child: const Text('🟡 O PACUS me ajudou a lembrar')),
          SimpleDialogOption(onPressed: () => Navigator.pop(context, 'promptedByAdult'), child: const Text('🔴 Um adulto me lembrou')),
        ],
      ),
    );
    if (value == null) return;
    try {
      await widget.api.request('/daily-tasks/' + task.id + '/initiative', method: 'PUT', body: {'initiative': value});
      final updated = await widget.api.getToday();
      if (mounted) setState(() => routine = updated);
    } catch (e) {
      if (mounted) setState(() => error = e.toString());
    }
  }

  Future<void> _setSkipReason(DailyTask task) async {
    final value = await showDialog<String>(
      context: context,
      builder: (context) => SimpleDialog(
        title: const Text('O que aconteceu?'),
        children: [
          for (final entry in const [
            ['sleepy', '😴 Estava com sono'],
            ['preferredOtherActivity', '🎮 Preferi outra atividade'],
            ['noTime', '⏰ Não deu tempo'],
            ['notInTheMood', '😐 Não estava com vontade'],
            ['disliked', '📚 Não gostei'],
            ['forgot', '🤷 Esqueci'],
            ['other', '✏️ Outro'],
          ])
            SimpleDialogOption(onPressed: () => Navigator.pop(context, entry[0]), child: Text(entry[1])),
        ],
      ),
    );
    if (value == null) return;
    try {
      await widget.api.request('/daily-tasks/' + task.id + '/skip-reason', method: 'PUT', body: {'reason': value, 'note': null});
      final updated = await widget.api.getToday();
      if (mounted) setState(() => routine = updated);
    } catch (e) {
      if (mounted) setState(() => error = e.toString());
    }
  }

  // Mesmo planejamento visual da noite, limitado ao periodo escolhido.
  // Reaproveita a API de ordenacao existente, sem criar pontos nem tarefas.
  Future<void> _planDayPeriod(DailyRoutine value, String period) async {
    final label = period == 'morning' ? 'manhã' : 'tarde';
    final active = value.tasks.where((t) => !t.isDeleted).toList()
      ..sort((a, b) => a.order.compareTo(b.order));
    final pending = active.where((t) =>
        !t.isDone && t.period.toLowerCase() == period).toList();
    if (pending.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Não há tarefas pendentes para a $label.')),
      );
      return;
    }
    final ordered = List<DailyTask>.from(pending);
    final accepted = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => StatefulBuilder(
        builder: (dialogContext, setDialog) => AlertDialog(
          title: Text('Como você quer organizar sua $label?'),
          content: SizedBox(
            width: 500,
            height: 360,
            child: ListView.builder(
              itemCount: ordered.length,
              itemBuilder: (_, i) => ListTile(
                leading: CircleAvatar(child: Text((i + 1).toString())),
                title: Text(ordered[i].title),
                trailing: Wrap(
                  spacing: 2,
                  children: [
                    IconButton(
                      onPressed: i == 0 ? null : () => setDialog(() {
                        final item = ordered.removeAt(i);
                        ordered.insert(i - 1, item);
                      }),
                      icon: const Icon(Icons.arrow_upward),
                      tooltip: 'Subir',
                    ),
                    IconButton(
                      onPressed: i == ordered.length - 1 ? null : () => setDialog(() {
                        final item = ordered.removeAt(i);
                        ordered.insert(i + 1, item);
                      }),
                      icon: const Icon(Icons.arrow_downward),
                      tooltip: 'Descer',
                    ),
                  ],
                ),
              ),
            ),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(dialogContext, false),
              child: const Text('Cancelar'),
            ),
            FilledButton(
              onPressed: () => Navigator.pop(dialogContext, true),
              child: const Text('Salvar ordem'),
            ),
          ],
        ),
      ),
    );
    if (accepted != true || !mounted) return;

    // Preserva a posicao das tarefas ja concluidas e de outros periodos.
    var next = 0;
    final reordered = active.map((task) {
      if (!task.isDone && task.period.toLowerCase() == period) {
        return ordered[next++];
      }
      return task;
    }).toList();
    try {
      await widget.api.request(
        '/daily-routines/today/order',
        method: 'PUT',
        body: reordered.map((t) => t.id).toList(),
      );
      final updated = await widget.api.getToday();
      if (mounted) setState(() => routine = updated);
    } catch (e) {
      if (mounted) setState(() => error = e.toString());
    }
  }

  Future<void> _planEvening(DailyRoutine value) async {
    final evening = value.tasks.where((t) => !t.isDeleted && !t.isDone && t.period.toLowerCase() == 'evening').toList();
    if (evening.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content: Text('Não há tarefas pendentes para a noite.')));
      return;
    }
    final ordered = List<DailyTask>.from(evening);
    final accepted = await showDialog<bool>(
      context: context,
      builder: (context) => StatefulBuilder(
        builder: (context, setDialog) => AlertDialog(
          title: const Text('Como você quer organizar sua noite?'),
          content: SizedBox(
            width: 500,
            height: 360,
            child: ListView.builder(
              itemCount: ordered.length,
              itemBuilder: (_, i) => ListTile(
                leading: CircleAvatar(child: Text((i + 1).toString())),
                title: Text(ordered[i].title),
                trailing: Wrap(
                  spacing: 2,
                  children: [
                    IconButton(
                      onPressed: i == 0 ? null : () => setDialog(() {
                        final item = ordered.removeAt(i);
                        ordered.insert(i - 1, item);
                      }),
                      icon: const Icon(Icons.arrow_upward),
                      tooltip: 'Subir',
                    ),
                    IconButton(
                      onPressed: i == ordered.length - 1 ? null : () => setDialog(() {
                        final item = ordered.removeAt(i);
                        ordered.insert(i + 1, item);
                      }),
                      icon: const Icon(Icons.arrow_downward),
                      tooltip: 'Descer',
                    ),
                  ],
                ),
              ),
            ),
          ),
          actions: [
            TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Cancelar')),
            FilledButton(onPressed: () => Navigator.pop(context, true), child: const Text('Salvar ordem')),
          ],
        ),
      ),
    );
    if (accepted != true) return;
    try {
      await widget.api.request('/daily-routines/today/evening-plan', method: 'PUT', body: {
        'items': ordered.map((t) => {'taskId': t.id, 'approxLabel': null}).toList(),
      });
      final updated = await widget.api.getToday();
      if (mounted) setState(() => routine = updated);
    } catch (e) {
      if (mounted) setState(() => error = e.toString());
    }
  }

  @override
  Widget build(BuildContext context) {
    if (loading) {
      return Scaffold(
        body: Center(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const CircularProgressIndicator(),
              if (slowLoading) ...[
                const SizedBox(height: 16),
                const Padding(
                  padding: EdgeInsets.symmetric(horizontal: 28),
                  child: Text(
                    'Ainda carregando... o servidor pode estar iniciando depois de um tempo parado.',
                    textAlign: TextAlign.center,
                  ),
                ),
              ],
            ],
          ),
        ),
      );
    }
    if (routine == null) {
      return Scaffold(body: Center(child: FilledButton(onPressed: _load, child: Text(error ?? 'Tentar novamente'))));
    }
    final r = routine!;
    return Scaffold(
      appBar: AppBar(
        backgroundColor: Colors.transparent,
        title: Row(
          children: [
            const PacusBrand(compact: true),
            const SizedBox(width: 14),
            Expanded(
              child: Text(
                'Olá, ${widget.session.name.isEmpty ? 'PACUS' : widget.session.name}',
                overflow: TextOverflow.ellipsis,
              ),
            ),
          ],
        ),
        actions: [IconButton(onPressed: widget.onLogout, tooltip: 'Sair', icon: const Icon(Icons.logout))],
      ),
      body: RefreshIndicator(
        onRefresh: _load,
        child: ListView(
          padding: EdgeInsets.zero,
          children: [
            PacusPageWidth(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  if (error != null) ...[
                    MaterialBanner(content: Text(error!), actions: [TextButton(onPressed: () => setState(() => error = null), child: const Text('Fechar'))]),
                    const SizedBox(height: 12),
                  ],
                  _progressCard(r),
                  if (r.tomorrowPlanConfirmedAt != null) ...[
                    const SizedBox(height: 12),
                    _plannedYesterdayCard(),
                  ],
                  const SizedBox(height: 20),
                  _tasksCard(r),
                  const SizedBox(height: 12),
                  Wrap(
                    spacing: 8,
                    runSpacing: 8,
                    children: [
                      OutlinedButton.icon(
                        onPressed: () => _planDayPeriod(r, 'morning'),
                        icon: const Icon(Icons.wb_sunny_outlined),
                        label: const Text('Planejar minha manhã'),
                      ),
                      OutlinedButton.icon(
                        onPressed: () => _planDayPeriod(r, 'afternoon'),
                        icon: const Icon(Icons.light_mode_outlined),
                        label: const Text('Planejar minha tarde'),
                      ),
                      OutlinedButton.icon(
                        onPressed: () => _planEvening(r),
                        icon: const Icon(Icons.nightlight_outlined),
                        label: const Text('Planejar minha noite'),
                      ),
                    ],
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _plannedYesterdayCard() {
    return Card(
      color: Theme.of(context).colorScheme.primaryContainer,
      child: const Padding(
        padding: EdgeInsets.all(16),
        child: Row(
          children: [
            Icon(Icons.auto_awesome),
            SizedBox(width: 10),
            Expanded(
              child: Text(
                'Você planejou este dia ontem. Veja o que foi criado e organizado por você.',
                style: TextStyle(fontWeight: FontWeight.w800),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _progressCard(DailyRoutine r) {
    final totalTasks = r.totalTasks;
    final doneTasks = r.doneTasks;
    final progress = totalTasks == 0 ? 0.0 : doneTasks / totalTasks;
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(22),
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Text(
            widget.session.isAdult ? 'Hoje' : 'Seu dia, no seu ritmo',
            style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w700),
          ),
          const SizedBox(height: 8),
          Text(
            widget.session.isAdult
                ? '$doneTasks de $totalTasks tarefas concluídas'
                : 'Você está avançando. Uma tarefa por vez.',
            style: const TextStyle(fontSize: 22, fontWeight: FontWeight.w800),
          ),
          const SizedBox(height: 14),
          LinearProgressIndicator(value: progress, minHeight: 10, borderRadius: BorderRadius.circular(99)),
          if (!widget.session.isAdult) ...[
            const SizedBox(height: 8),
            Text(
              'Pedir ajuda ou fazer uma pausa faz parte do processo.',
              style: Theme.of(context).textTheme.bodySmall,
            ),
          ],
        ]),
      ),
    );
  }

  Widget _tasksCard(DailyRoutine r) {
    final tasks = r.tasks.where((t) => !t.isDeleted).toList()
      ..sort((a, b) => a.order.compareTo(b.order));

    final morning = tasks.where((t) => t.period.toLowerCase() == 'morning').toList();
    final afternoon = tasks.where((t) => t.period.toLowerCase() == 'afternoon').toList();
    final evening = tasks.where((t) => t.period.toLowerCase() == 'evening').toList();
    final theme = Theme.of(context);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Row(
          children: [
            Expanded(
              child: Text(
                'Minha rotina',
                style: theme.textTheme.headlineMedium,
              ),
            ),
            IconButton(
              onPressed: _createDailyTask,
              icon: const Icon(Icons.add_task_rounded),
              tooltip: 'Adicionar tarefa',
            ),
          ],
        ),
        const SizedBox(height: 12),
        _periodSection(
          title: 'Manhã',
          icon: Icons.wb_sunny_outlined,
          tasks: morning,
          accent: theme.colorScheme.secondary,
        ),
        const SizedBox(height: 14),
        _periodSection(
          title: 'Tarde',
          icon: Icons.light_mode_outlined,
          tasks: afternoon,
          accent: theme.colorScheme.primary,
        ),
        const SizedBox(height: 14),
        _periodSection(
          title: 'Noite',
          icon: Icons.nightlight_round,
          tasks: evening,
          accent: theme.colorScheme.tertiary,
        ),
      ],
    );
  }

  Widget _periodSection({
    required String title,
    required IconData icon,
    required List<DailyTask> tasks,
    required Color accent,
  }) {
    final done = tasks.where((t) => t.isDone).length;

    return PacusSectionCard(
      title: title,
      icon: icon,
      accent: accent,
      subtitle: tasks.isEmpty
          ? 'Nenhuma tarefa neste período'
          : '$done de ${tasks.length} concluídas',
      trailing: PacusBadge(
        label: '$done/${tasks.length}',
        emphasis: tasks.isNotEmpty && done == tasks.length,
      ),
      child: tasks.isEmpty
          ? Padding(
              padding: const EdgeInsets.symmetric(vertical: 8),
              child: Text(
                'Você pode deixar este período livre ou adicionar uma tarefa.',
                style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                      color: Theme.of(context).colorScheme.onSurfaceVariant,
                    ),
              ),
            )
          : Column(
              children: [
                for (var i = 0; i < tasks.length; i++)
                  _taskTile(
                    tasks[i],
                    canMoveUp: i > 0,
                    canMoveDown: i < tasks.length - 1,
                  ),
              ],
            ),
    );
  }

  Widget _taskTile(
    DailyTask task, {
    required bool canMoveUp,
    required bool canMoveDown,
  }) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;
    final description = (task.description ?? '').trim();

    final actionMenu = PopupMenuButton<String>(
      tooltip: 'Mais opções',
      onSelected: (value) {
        if (value == 'up') _moveTaskWithinPeriod(task, -1);
        if (value == 'down') _moveTaskWithinPeriod(task, 1);
        if (value == 'edit') _editDailyTask(task);
        if (value == 'initiative') _setInitiative(task);
        if (value == 'skip') _setSkipReason(task);
        if (value == 'delete') _deleteDailyTask(task);
      },
      itemBuilder: (_) => [
        if (canMoveUp)
          const PopupMenuItem(value: 'up', child: Text('Mover para cima')),
        if (canMoveDown)
          const PopupMenuItem(value: 'down', child: Text('Mover para baixo')),
        const PopupMenuItem(value: 'edit', child: Text('Editar')),
        const PopupMenuItem(value: 'initiative', child: Text('Como comecei')),
        const PopupMenuItem(value: 'skip', child: Text('O que aconteceu')),
        const PopupMenuItem(value: 'delete', child: Text('Excluir tarefa')),
      ],
    );

    return PacusTaskSurface(
      done: task.isDone,
      onToggle: () => _toggleTask(task),
      actions: actionMenu,
      title: Text(
        task.title,
        style: theme.textTheme.titleMedium?.copyWith(
          decoration: task.isDone ? TextDecoration.lineThrough : null,
          color: task.isDone ? scheme.onSurfaceVariant : scheme.onSurface,
        ),
      ),
      body: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Wrap(
            spacing: 7,
            runSpacing: 7,
            children: [
              if (task.points > 0)
                PacusBadge(
                  label: '${task.points} ${task.points == 1 ? 'ponto' : 'pontos'}',
                  icon: Icons.stars_rounded,
                )
              else
                const PacusBadge(
                  label: 'Sem pontos associados',
                  icon: Icons.stars_rounded,
                ),
              if (task.createdByMember)
                const PacusBadge(
                  label: 'Criado por mim',
                  icon: Icons.auto_awesome_rounded,
                  emphasis: true,
                ),
            ],
          ),
          if (description.isNotEmpty) ...[
            const SizedBox(height: 9),
            Text(
              description,
              style: theme.textTheme.bodyMedium?.copyWith(
                color: scheme.onSurface,
              ),
            ),
          ],
          if ((task.planCue ?? '').trim().isNotEmpty) ...[
            const SizedBox(height: 8),
            _taskHint(
              icon: Icons.route_outlined,
              label: 'Plano',
              text: task.planCue!.trim(),
            ),
          ],
          if ((task.minimumGoalLabel ?? '').trim().isNotEmpty) ...[
            const SizedBox(height: 6),
            _taskHint(
              icon: Icons.flag_outlined,
              label: 'Meta mínima',
              text: task.minimumGoalLabel!.trim(),
            ),
          ],
          if (task.options.isNotEmpty && !task.isDone) ...[
            const SizedBox(height: 10),
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: task.options
                  .map(
                    (option) => ChoiceChip(
                      label: Text(option),
                      selected: task.selectedOption == option,
                      onSelected: (_) => _selectOption(task, option),
                    ),
                  )
                  .toList(),
            ),
          ],
        ],
      ),
    );
  }

  Widget _taskHint({
    required IconData icon,
    required String label,
    required String text,
  }) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;

    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Icon(icon, size: 17, color: scheme.onSurfaceVariant),
        const SizedBox(width: 6),
        Expanded(
          child: Text.rich(
            TextSpan(
              style: theme.textTheme.bodySmall?.copyWith(
                height: 1.45,
                color: scheme.onSurfaceVariant,
              ),
              children: [
                TextSpan(
                  text: '$label: ',
                  style: const TextStyle(fontWeight: FontWeight.w700),
                ),
                TextSpan(text: text),
              ],
            ),
          ),
        ),
      ],
    );
  }

}
