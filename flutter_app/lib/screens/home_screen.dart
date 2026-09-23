import 'dart:async';
import 'dart:math' as math;
import 'dart:typed_data';

import 'package:audioplayers/audioplayers.dart';
import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';

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
  Timer? timer;
  int? sessionMinutes;
  DateTime? sessionEndsAt;
  Duration remaining = Duration.zero;
  bool sessionPaused = false;
  bool completing = false;
  bool slowLoading = false;
  Timer? slowLoadTimer;
  Timer? dayBoundaryTimer;
  late String lastDayKey;

  String get _sessionKey => 'pacus.flutter.session:${routine?.familyId ?? ''}:${routine?.date ?? ''}';

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
    timer?.cancel();
    slowLoadTimer?.cancel();
    dayBoundaryTimer?.cancel();
    super.dispose();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed) {
      _checkDayBoundary();
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
      await _restoreSession();
      widget.onPendingChanged?.call(
        widget.session.isAdult ? 0 : math.max(0, value.totalTasks - value.doneTasks),
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

  Future<void> _restoreSession() async {
    if (routine == null) return;
    final prefs = await SharedPreferences.getInstance();
    final raw = prefs.getString(_sessionKey);
    if (raw == null) return;
    final parts = raw.split('|');
    if (parts.length != 2 && parts.length != 4) { await prefs.remove(_sessionKey); return; }
    final minutes = int.tryParse(parts[0]);
    final millis = int.tryParse(parts[1]);
    if (minutes == null || millis == null || minutes <= 0) { await prefs.remove(_sessionKey); return; }
    sessionMinutes = minutes;

    if (parts.length == 4 && parts[2] == '1') {
      final remainingSeconds = int.tryParse(parts[3]) ?? 0;
      sessionPaused = true;
      sessionEndsAt = null;
      remaining = Duration(seconds: math.max(0, remainingSeconds));
      if (remaining <= Duration.zero) {
        await prefs.remove(_sessionKey);
        sessionMinutes = null;
        sessionPaused = false;
      }
      return;
    }

    sessionPaused = false;
    sessionEndsAt = DateTime.fromMillisecondsSinceEpoch(millis);
    _startTicker();
  }

  Future<void> _startSession(int minutes) async {
    final r = routine;
    if (r == null || minutes <= 0 || minutes > r.availableGameMinutes) return;
    final end = DateTime.now().add(Duration(minutes: minutes));
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(_sessionKey, '$minutes|${end.millisecondsSinceEpoch}|0|${minutes * 60}');
    setState(() {
      sessionMinutes = minutes;
      sessionEndsAt = end;
      remaining = Duration(minutes: minutes);
      sessionPaused = false;
    });
    _startTicker();
  }

  void _startTicker() {
    timer?.cancel();
    _tick();
    timer = Timer.periodic(const Duration(seconds: 1), (_) => _tick());
  }

  void _tick() {
    final end = sessionEndsAt;
    if (end == null) return;
    final diff = end.difference(DateTime.now());
    if (diff <= Duration.zero) {
      remaining = Duration.zero;
      timer?.cancel();
      if (mounted) setState(() {});
      _finishSession();
      return;
    }
    if (mounted) setState(() => remaining = diff);
  }

  Future<void> _pauseSession() async {
    if (sessionMinutes == null || sessionPaused) return;
    final end = sessionEndsAt;
    if (end == null) return;

    final diff = end.difference(DateTime.now());
    final safeRemaining = diff <= Duration.zero ? Duration.zero : diff;
    timer?.cancel();

    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(
      _sessionKey,
      '${sessionMinutes!}|0|1|${safeRemaining.inSeconds}',
    );

    if (mounted) {
      setState(() {
        sessionPaused = true;
        sessionEndsAt = null;
        remaining = safeRemaining;
      });
    }
  }

  Future<void> _resumeSession() async {
    if (sessionMinutes == null || !sessionPaused || remaining <= Duration.zero) return;

    final end = DateTime.now().add(remaining);
    final prefs = await SharedPreferences.getInstance();
    await prefs.setString(
      _sessionKey,
      '${sessionMinutes!}|${end.millisecondsSinceEpoch}|0|${remaining.inSeconds}',
    );

    if (mounted) {
      setState(() {
        sessionPaused = false;
        sessionEndsAt = end;
      });
    }
    _startTicker();
  }
  Future<void> _finishSession() async {
    if (completing || sessionMinutes == null) return;
    completing = true;
    try {
      final updated = await widget.api.consumeGameTimer(sessionMinutes!);
      final prefs = await SharedPreferences.getInstance();
      await prefs.remove(_sessionKey);
      await _playDuck();
      if (!mounted) return;
      setState(() {
        routine = updated;
        sessionMinutes = null;
        sessionEndsAt = null;
        remaining = Duration.zero;
        sessionPaused = false;
      });
      await showDialog<void>(
        context: context,
        builder: (context) => AlertDialog(
          title: const Text('🦆 Quá quá!'),
          content: Text('Seu tempo terminou. Restam ${_formatMinutes(updated.availableGameMinutes)} hoje.'),
          actions: [TextButton(onPressed: () => Navigator.pop(context), child: const Text('Entendi'))],
        ),
      );
    } catch (e) {
      if (mounted) setState(() => error = e.toString());
    } finally {
      completing = false;
    }
  }

  Future<void> _playDuck() async {
    final player = AudioPlayer(playerId: 'pacus_timer_alarm');
    try {
      await player.setVolume(1.0);

      for (var i = 0; i < 3; i++) {
        await player.stop();
        await player.play(
          BytesSource(_duckWav(), mimeType: 'audio/wav'),
          volume: 1.0,
        );

        // O alerta sintetizado dura cerca de 620 ms.
        await Future<void>.delayed(const Duration(milliseconds: 700));
        await player.stop();

        if (i < 2) {
          await Future<void>.delayed(const Duration(milliseconds: 180));
        }
      }
    } catch (_) {
      // O modal visual continua funcionando caso o navegador bloqueie audio.
    } finally {
      await player.dispose();
    }
  }

  Uint8List _duckWav() {
    const rate = 22050;
    const seconds = 0.62;
    final samples = (rate * seconds).round();
    final data = ByteData(44 + samples * 2);
    void ascii(int offset, String s) {
      for (var i = 0; i < s.length; i++) { data.setUint8(offset + i, s.codeUnitAt(i)); }
    }
    ascii(0, 'RIFF'); data.setUint32(4, 36 + samples * 2, Endian.little); ascii(8, 'WAVE');
    ascii(12, 'fmt '); data.setUint32(16, 16, Endian.little); data.setUint16(20, 1, Endian.little);
    data.setUint16(22, 1, Endian.little); data.setUint32(24, rate, Endian.little);
    data.setUint32(28, rate * 2, Endian.little); data.setUint16(32, 2, Endian.little);
    data.setUint16(34, 16, Endian.little); ascii(36, 'data'); data.setUint32(40, samples * 2, Endian.little);
    for (var i = 0; i < samples; i++) {
      final t = i / rate;
      double burst(double start, double len, double high, double low) {
        final x = (t - start) / len;
        if (x < 0 || x > 1) return 0;
        final env = math.sin(math.pi * x) * (1 - x * .35);
        final freq = high + (low - high) * x;
        return math.sin(2 * math.pi * freq * (t - start)) * env;
      }
      final v = burst(0, .24, 340, 125) * .72 + burst(.30, .22, 295, 110) * .62;
      data.setInt16(44 + i * 2, (v.clamp(-1, 1) * 32700).round(), Endian.little);
    }
    return data.buffer.asUint8List();
  }

  Future<void> _adjust(int delta) async {
    try {
      final updated = await widget.api.adjustGameTimer(delta);
      if (mounted) setState(() => routine = updated);
    } catch (e) {
      if (mounted) setState(() => error = e.toString());
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

  Future<void> _setReaction(String icon) async {
    final controller = TextEditingController();
    final message = await showDialog<String>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Enviar reconhecimento'),
        content: TextField(
          controller: controller,
          decoration: const InputDecoration(labelText: 'Mensagem opcional'),
          maxLines: 3,
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context), child: const Text('Cancelar')),
          FilledButton(onPressed: () => Navigator.pop(context, controller.text.trim()), child: const Text('Enviar')),
        ],
      ),
    );
    controller.dispose();
    if (message == null) return;
    try {
      final data = await widget.api.request('/daily-routines/today/reaction', method: 'PUT', body: {
        'icon': icon,
        'message': message.isEmpty ? null : message,
      });
      if (data is Map<String, dynamic>) {
        final updated = await widget.api.getToday();
        if (mounted) setState(() => routine = updated);
      }
    } catch (e) {
      if (mounted) setState(() => error = e.toString());
    }
  }

  Future<void> _createDailyTask() async {
    final title = TextEditingController();
    final points = TextEditingController(text: '1');
    String period = 'morning';
    String type = 'expected';
    final payload = await showDialog<Map<String, dynamic>>(
      context: context,
      builder: (context) => StatefulBuilder(
        builder: (context, setDialog) => AlertDialog(
          title: const Text('Nova tarefa de hoje'),
          content: SizedBox(
            width: 460,
            child: Column(mainAxisSize: MainAxisSize.min, children: [
              TextField(controller: title, decoration: const InputDecoration(labelText: 'Título')),
              const SizedBox(height: 10),
              TextField(controller: points, keyboardType: TextInputType.number, decoration: const InputDecoration(labelText: 'Pontos')),
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
            ]),
          ),
          actions: [
            TextButton(onPressed: () => Navigator.pop(context), child: const Text('Cancelar')),
            FilledButton(onPressed: () => Navigator.pop(context, {
              'title': title.text.trim(),
              'description': null,
              'type': type,
              'period': period,
              'points': int.tryParse(points.text) ?? 1,
            }), child: const Text('Adicionar')),
          ],
        ),
      ),
    );
    title.dispose();
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
    try {
      await widget.api.delete('/daily-tasks/' + task.id);
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
    final points = TextEditingController(text: task.points.toString());
    String period = task.period;
    String type = task.type;
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
              TextField(controller: points, keyboardType: TextInputType.number, decoration: const InputDecoration(labelText: 'Pontos')),
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
                initialValue: type,
                decoration: const InputDecoration(labelText: 'Tipo'),
                items: const [
                  DropdownMenuItem(value: 'mandatory', child: Text('Obrigatória')),
                  DropdownMenuItem(value: 'expected', child: Text('Esperada')),
                  DropdownMenuItem(value: 'challenge', child: Text('Desafio')),
                ],
                onChanged: (v) => setDialog(() => type = v ?? type),
              ),
            ]),
          ),
          actions: [
            TextButton(onPressed: () => Navigator.pop(context), child: const Text('Cancelar')),
            FilledButton(
              onPressed: () => Navigator.pop(context, {
                'title': title.text.trim(),
                'description': description.text.trim().isEmpty ? null : description.text.trim(),
                'type': type,
                'period': period,
                'points': int.tryParse(points.text) ?? task.points,
                'options': task.options,
                'reason': task.reason,
              }),
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

  String _clock(Duration value) {
    final total = math.max(0, value.inSeconds);
    final h = total ~/ 3600, m = (total % 3600) ~/ 60, s = total % 60;
    String two(int n) => n.toString().padLeft(2, '0');
    return h > 0 ? '${two(h)}:${two(m)}:${two(s)}' : '${two(m)}:${two(s)}';
  }

  String _formatMinutes(int minutes) {
    if (minutes >= 60) {
      final h = minutes ~/ 60, m = minutes % 60;
      return m == 0 ? '${h}h' : '${h}h ${m}min';
    }
    return '${minutes}min';
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
                  if (r.reaction != null) ...[
                    const SizedBox(height: 16),
                    _receivedReactionCard(r.reaction!),
                  ],
                  if (widget.session.isAdult) ...[
                    const SizedBox(height: 16),
                    _reactionCard(),
                  ],
                  const SizedBox(height: 16),
                  if (r.gameTimerEnabled) _timerCard(r),
                  const SizedBox(height: 20),
                  _tasksCard(r),
                  const SizedBox(height: 12),
                  OutlinedButton.icon(onPressed: () => _planEvening(r), icon: const Icon(Icons.nightlight_outlined), label: const Text('Planejar minha noite')),
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
    final progress = r.totalTasks == 0 ? 0.0 : r.doneTasks / r.totalTasks;
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(22),
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          const Text('Hoje', style: TextStyle(fontSize: 14, fontWeight: FontWeight.w700)),
          const SizedBox(height: 8),
          Text('${r.doneTasks} de ${r.totalTasks} tarefas concluídas', style: const TextStyle(fontSize: 22, fontWeight: FontWeight.w800)),
          const SizedBox(height: 14),
          LinearProgressIndicator(value: progress, minHeight: 10, borderRadius: BorderRadius.circular(99)),
        ]),
      ),
    );
  }

  Widget _timerCard(DailyRoutine r) {
    final active = sessionMinutes != null && (sessionEndsAt != null || sessionPaused);
    final available = r.availableGameMinutes;
    if (active) {
      final totalSeconds = math.max(1, sessionMinutes! * 60);
      final progress = (1 - remaining.inSeconds / totalSeconds).clamp(0.0, 1.0);
      return Card(
        color: Theme.of(context).colorScheme.primaryContainer,
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(children: [
            const Text('TEMPO DESTA SESSÃO', style: TextStyle(fontWeight: FontWeight.w800, letterSpacing: 1)),
            const SizedBox(height: 12),
            FittedBox(child: Text(_clock(remaining), style: const TextStyle(fontSize: 66, fontWeight: FontWeight.w900))),
            const SizedBox(height: 14),
            LinearProgressIndicator(value: progress, minHeight: 12, borderRadius: BorderRadius.circular(99)),
            const SizedBox(height: 12),
            Text(
              sessionPaused
                  ? 'Sessão pausada • o tempo não está correndo'
                  : 'Você escolheu ${_formatMinutes(sessionMinutes!)} • depois restam ${_formatMinutes(math.max(0, available - sessionMinutes!))}',
              style: TextStyle(fontWeight: sessionPaused ? FontWeight.w800 : FontWeight.w500),
            ),
            const SizedBox(height: 14),
            FilledButton.icon(
              onPressed: sessionPaused ? _resumeSession : _pauseSession,
              icon: Icon(sessionPaused ? Icons.play_arrow_rounded : Icons.pause_rounded),
              label: Text(sessionPaused ? 'Retomar temporizador' : 'Pausar temporizador'),
            ),
          ]),
        ),
      );
    }

    final presets = [15, 30, 45, 60].where((m) => m <= available).toList();
    return Card(
      color: Theme.of(context).colorScheme.primaryContainer,
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
          const Text('TEMPO DISPONÍVEL HOJE', style: TextStyle(fontWeight: FontWeight.w800, letterSpacing: 1)),
          const SizedBox(height: 8),
          FittedBox(alignment: Alignment.centerLeft, fit: BoxFit.scaleDown,
            child: Text(_formatMinutes(available), style: const TextStyle(fontSize: 58, fontWeight: FontWeight.w900))),
          const SizedBox(height: 18),
          Text(available > 0 ? 'Quanto tempo você quer usar agora?' : 'Seu tempo de tela de hoje acabou. 🦆',
            style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w700)),
          if (presets.isNotEmpty) ...[
            const SizedBox(height: 14),
            Wrap(spacing: 10, runSpacing: 10, children: presets.map((m) =>
              FilledButton.tonal(onPressed: () => _startSession(m), child: Padding(
                padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 12), child: Text(_formatMinutes(m)),
              ))).toList()),
          ],
          if (widget.session.isAdult) ...[
            const Divider(height: 34),
            const Text('Adulto · adicionar tempo', style: TextStyle(fontWeight: FontWeight.w700)),
            const SizedBox(height: 10),
            Wrap(spacing: 8, runSpacing: 8, children: [
              OutlinedButton(onPressed: () => _adjust(15), child: const Text('+15 min')),
              OutlinedButton(onPressed: () => _adjust(30), child: const Text('+30 min')),
              OutlinedButton(onPressed: () => _adjust(60), child: const Text('+1 hora')),
            ]),
          ],
        ]),
      ),
    );
  }

  Widget _receivedReactionCard(Map<String, dynamic> reaction) {
    const icons = {
      'heart': '❤️',
      'clap': '👏',
      'star': '⭐',
      'hug': '🤗',
    };
    final icon = icons[reaction['icon']?.toString()] ?? '✨';
    final message = reaction['message']?.toString().trim() ?? '';
    return Card(
      color: Theme.of(context).colorScheme.secondaryContainer,
      child: Padding(
        padding: const EdgeInsets.all(18),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(icon, style: const TextStyle(fontSize: 34)),
            const SizedBox(width: 12),
            Expanded(
              child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                const Text('Reconhecimento de hoje', style: TextStyle(fontSize: 17, fontWeight: FontWeight.w900)),
                if (message.isNotEmpty) ...[
                  const SizedBox(height: 4),
                  Text(message),
                ],
              ]),
            ),
          ],
        ),
      ),
    );
  }

  Widget _reactionCard() => Card(
    child: Padding(
      padding: const EdgeInsets.all(18),
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        const Text('Reconhecimento do dia', style: TextStyle(fontSize: 18, fontWeight: FontWeight.w900)),
        const SizedBox(height: 8),
        const Text('Envie uma reação positiva para aparecer na rotina de hoje.'),
        const SizedBox(height: 10),
        Wrap(spacing: 8, children: [
          FilledButton.tonal(onPressed: () => _setReaction('heart'), child: const Text('❤️')),
          FilledButton.tonal(onPressed: () => _setReaction('clap'), child: const Text('👏')),
          FilledButton.tonal(onPressed: () => _setReaction('star'), child: const Text('⭐')),
          FilledButton.tonal(onPressed: () => _setReaction('hug'), child: const Text('🤗')),
        ]),
      ]),
    ),
  );

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
              tooltip: 'Adicionar tarefa de hoje',
            ),
          ],
        ),
        const SizedBox(height: 12),
        _periodSection(
          title: 'Manhã',
          icon: Icons.wb_sunny_outlined,
          tasks: morning,
          accent: const Color(0xFFE59A22),
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
    final reason = (task.reason ?? '').trim();
    final showReason = reason.isNotEmpty &&
        description.toLowerCase() != reason.toLowerCase();

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
        const PopupMenuItem(value: 'delete', child: Text('Remover do dia')),
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
              PacusBadge(
                label: '${task.points} ${task.points == 1 ? 'ponto' : 'pontos'}',
                icon: Icons.stars_rounded,
              ),
              PacusBadge(label: _typeLabel(task.type)),
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
          if (showReason) ...[
            const SizedBox(height: 6),
            _taskHint(
              icon: Icons.lightbulb_outline_rounded,
              label: 'Por quê',
              text: reason,
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

  String _typeLabel(String type) {
    switch (type.toLowerCase()) {
      case 'mandatory': return 'Obrigatória';
      case 'challenge': return 'Desafio';
      default: return 'Esperada';
    }
  }

}
