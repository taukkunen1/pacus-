
import 'package:flutter/material.dart';
import '../api.dart';

class PacusScreen extends StatefulWidget {
  const PacusScreen({super.key, required this.api});
  final PacusApi api;
  @override State<PacusScreen> createState() => _PacusScreenState();
}

class _PacusScreenState extends State<PacusScreen> {
  Map<String, dynamic>? pacus;
  String? error;
  @override void initState() { super.initState(); _load(); }

  Future<void> _load() async {
    try {
      final data = await widget.api.getMap('/pacus/me');
      if (mounted) setState(() { pacus = data; error = null; });
    } catch (e) {
      if (mounted) setState(() => error = e.toString());
    }
  }

  String _stage(dynamic raw) {
    const labels = {'egg':'Ovo','cracking':'Rachando','hatching':'Eclodindo','baby':'Filhote','young':'Jovem','juvenile':'Jovem','adult':'Adulto'};
    final key = raw?.toString().toLowerCase() ?? 'juvenile';
    return labels[key] ?? raw?.toString() ?? 'Jovem';
  }

  @override Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: Text(pacus?['name']?.toString() ?? 'PACUS')),
    body: RefreshIndicator(
      onRefresh: _load,
      child: ListView(
        padding: const EdgeInsets.all(18),
        children: [
          if (error != null) Text(error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
          Card(
            color: const Color(0xFFDDEFEA),
            child: Padding(
              padding: const EdgeInsets.all(28),
              child: Column(children: [
                const Text('🐟', style: TextStyle(fontSize: 96)),
                const SizedBox(height: 10),
                Text(pacus?['name']?.toString() ?? 'Pacus', style: const TextStyle(fontSize: 30, fontWeight: FontWeight.w900)),
                Text(_stage(pacus?['stage']), style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w700)),
              ]),
            ),
          ),
          const SizedBox(height: 16),
          Row(children: [
            Expanded(child: _stat('Estágio', _stage(pacus?['stage']))),
            const SizedBox(width: 10),
            Expanded(child: _stat('Dias vividos', (pacus?['totalClosedDays'] ?? 0).toString())),
            const SizedBox(width: 10),
            Expanded(child: _stat('Tamanho', ((pacus?['size'] as num?)?.toDouble() ?? 0).toStringAsFixed(1))),
          ]),
          const SizedBox(height: 20),
          if ((pacus?['stageHistory'] as List?)?.isNotEmpty == true) ...[
            const Text('Estágios anteriores', style: TextStyle(fontSize: 22, fontWeight: FontWeight.w900)),
            for (final raw in (pacus!['stageHistory'] as List))
              ListTile(
                leading: const Icon(Icons.auto_awesome),
                title: Text(_stage((raw as Map)['stage'])),
                subtitle: Text(raw['reachedAt']?.toString().split('T').first ?? ''),
              ),
          ],
        ],
      ),
    ),
  );

  Widget _stat(String label, String value) => Card(
    child: Padding(
      padding: const EdgeInsets.symmetric(vertical: 20, horizontal: 10),
      child: Column(children: [
        Text(value, textAlign: TextAlign.center, style: const TextStyle(fontSize: 20, fontWeight: FontWeight.w900)),
        const SizedBox(height: 4),
        Text(label, textAlign: TextAlign.center),
      ]),
    ),
  );
}
