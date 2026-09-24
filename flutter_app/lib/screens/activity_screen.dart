import 'package:flutter/material.dart';

import '../api.dart';

class ActivityScreen extends StatefulWidget {
  const ActivityScreen({super.key, required this.api});
  final PacusApi api;

  @override
  State<ActivityScreen> createState() => _ActivityScreenState();
}

class _ActivityScreenState extends State<ActivityScreen> {
  bool loading = true;
  String? error;
  List<Map<String, dynamic>> items = const [];

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    if (mounted) setState(() { loading = true; error = null; });
    try {
      final data = await widget.api.getList('/activity?limit=200');
      if (!mounted) return;
      setState(() {
        items = data.whereType<Map<String, dynamic>>().toList();
        loading = false;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() { error = e.toString(); loading = false; });
    }
  }

  String _when(dynamic raw) {
    final dt = DateTime.tryParse(raw?.toString() ?? '')?.toLocal();
    if (dt == null) return '';
    String two(int n) => n.toString().padLeft(2, '0');
    return '${two(dt.day)}/${two(dt.month)} ${two(dt.hour)}:${two(dt.minute)}';
  }

  IconData _icon(String kind) => switch (kind) {
        'points' => Icons.stars_outlined,
        'growth' => Icons.water_drop_outlined,
        _ => Icons.receipt_long_outlined,
      };

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Atividade'),
        actions: [IconButton(onPressed: loading ? null : _load, icon: const Icon(Icons.refresh))],
      ),
      body: loading
          ? const Center(child: CircularProgressIndicator())
          : error != null
              ? Center(child: Padding(
                  padding: const EdgeInsets.all(24),
                  child: Column(mainAxisSize: MainAxisSize.min, children: [
                    Text(error!, textAlign: TextAlign.center),
                    const SizedBox(height: 12),
                    FilledButton(onPressed: _load, child: const Text('Tentar novamente')),
                  ]),
                ))
              : items.isEmpty
                  ? const Center(child: Text('Nenhuma atividade registrada ainda.'))
                  : RefreshIndicator(
                      onRefresh: _load,
                      child: ListView.separated(
                        padding: const EdgeInsets.all(16),
                        itemCount: items.length,
                        separatorBuilder: (_, __) => const Divider(height: 1),
                        itemBuilder: (context, index) {
                          final item = items[index];
                          final kind = item['kind']?.toString() ?? 'audit';
                          final delta = (item['delta'] as num?)?.toInt();
                          final details = item['details']?.toString();
                          return ListTile(
                            leading: CircleAvatar(child: Icon(_icon(kind))),
                            title: Text(item['title']?.toString() ?? item['action']?.toString() ?? 'Atividade'),
                            subtitle: Text([
                              _when(item['at']),
                              if (details != null && details.isNotEmpty) details,
                              'Origem: ${item['actorRole'] ?? 'System'}',
                            ].where((x) => x.isNotEmpty).join(' • ')),
                            trailing: delta == null
                                ? null
                                : Text(
                                    '${delta > 0 ? '+' : ''}$delta PP',
                                    style: const TextStyle(fontWeight: FontWeight.w800),
                                  ),
                          );
                        },
                      ),
                    ),
    );
  }
}
