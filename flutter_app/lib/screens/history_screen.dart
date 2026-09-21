
import 'package:flutter/material.dart';
import '../api.dart';

class HistoryScreen extends StatefulWidget {
  const HistoryScreen({super.key, required this.api});
  final PacusApi api;
  @override State<HistoryScreen> createState() => _HistoryScreenState();
}

class _HistoryScreenState extends State<HistoryScreen> {
  final days = <Map<String, dynamic>>[];
  bool loading = true;
  String? error;
  int page = 1;
  int totalPages = 1;

  @override void initState() { super.initState(); _load(reset: true); }

  Future<void> _load({bool reset = false}) async {
    if (reset) { page = 1; days.clear(); }
    setState(() => loading = true);
    try {
      final data = await widget.api.getMap('/history?page=' + page.toString() + '&pageSize=20');
      days.addAll((data['items'] as List? ?? []).map((e) => Map<String, dynamic>.from(e as Map)));
      totalPages = (data['totalPages'] as num?)?.toInt() ?? 1;
      if (mounted) setState(() { loading = false; error = null; });
    } catch (e) {
      if (mounted) setState(() { loading = false; error = e.toString(); });
    }
  }

  Future<void> _openDay(String date) async {
    try {
      final day = await widget.api.getMap('/history?date=' + date);
      if (!mounted) return;
      await showModalBottomSheet<void>(
        context: context, isScrollControlled: true,
        builder: (_) => DraggableScrollableSheet(
          expand: false, initialChildSize: .82,
          builder: (_, controller) => ListView(
            controller: controller, padding: const EdgeInsets.all(24),
            children: [
              Text(date, style: Theme.of(context).textTheme.headlineMedium?.copyWith(fontWeight: FontWeight.w900)),
              const SizedBox(height: 16),
              for (final raw in (day['tasks'] as List? ?? []))
                Builder(builder: (_) {
                  final t = Map<String, dynamic>.from(raw as Map);
                  final done = t['status']?.toString().toLowerCase() == 'done';
                  return ListTile(
                    leading: Icon(done ? Icons.check_circle : Icons.radio_button_unchecked),
                    title: Text(t['title']?.toString() ?? 'Tarefa'),
                    trailing: Text(done ? '+' + (t['points'] ?? 0).toString() + ' PP' : '0 PP'),
                  );
                }),
            ],
          ),
        ),
      );
    } catch (e) {
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.toString())));
    }
  }

  @override Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Histórico')),
    body: RefreshIndicator(
      onRefresh: () => _load(reset: true),
      child: ListView(
        padding: const EdgeInsets.all(18),
        children: [
          if (error != null) Text(error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
          if (days.isEmpty && loading) const Center(child: Padding(padding: EdgeInsets.all(40), child: CircularProgressIndicator())),
          if (days.isEmpty && !loading) const Center(child: Padding(padding: EdgeInsets.all(40), child: Text('Nenhum dia encerrado ainda.'))),
          for (final day in days) ...[
            Card(
              child: ListTile(
                onTap: () => _openDay(day['date']?.toString() ?? ''),
                title: Text(day['date']?.toString() ?? '-', style: const TextStyle(fontWeight: FontWeight.w800)),
                subtitle: Text((((day['stats'] as Map?)?['mandatory'] as Map?)?['done'] ?? 0).toString() + '/' + (((day['stats'] as Map?)?['mandatory'] as Map?)?['total'] ?? 0).toString() + ' obrigatórias'),
                trailing: Text(((((day['stats'] as Map?)?['completionRate'] as num?)?.toDouble() ?? 0) * 100).round().toString() + '% · +' + (day['pointsEarned'] ?? 0).toString() + ' PP'),
              ),
            ),
            const SizedBox(height: 10),
          ],
          if (page < totalPages) OutlinedButton(
            onPressed: loading ? null : () { page++; _load(); },
            child: const Text('Carregar mais'),
          ),
        ],
      ),
    ),
  );
}
