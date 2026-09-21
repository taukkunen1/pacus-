
import 'package:flutter/material.dart';
import '../api.dart';

class PointsScreen extends StatefulWidget {
  const PointsScreen({super.key, required this.api});
  final PacusApi api;
  @override State<PointsScreen> createState() => _PointsScreenState();
}

class _PointsScreenState extends State<PointsScreen> {
  Map<String, dynamic>? balance;
  Map<String, dynamic>? autonomy;
  final transactions = <Map<String, dynamic>>[];
  int page = 1;
  int totalPages = 1;
  bool loading = true;
  String? error;

  @override void initState() { super.initState(); _load(reset: true); }

  Future<void> _load({bool reset = false}) async {
    if (reset) { page = 1; transactions.clear(); balance = null; autonomy = null; }
    setState(() => loading = true);
    try {
      balance ??= await widget.api.getMap('/points');
      try { autonomy ??= await widget.api.getMap('/autonomy/weekly'); } catch (_) {}
      final data = await widget.api.getMap('/points/transactions?page=' + page.toString() + '&pageSize=20');
      transactions.addAll((data['items'] as List? ?? []).map((e) => Map<String, dynamic>.from(e as Map)));
      totalPages = (data['totalPages'] as num?)?.toInt() ?? 1;
      if (mounted) setState(() { loading = false; error = null; });
    } catch (e) {
      if (mounted) setState(() { loading = false; error = e.toString(); });
    }
  }

  @override Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Pacus Points')),
    body: RefreshIndicator(
      onRefresh: () => _load(reset: true),
      child: ListView(
        padding: const EdgeInsets.all(18),
        children: [
          if (error != null) Text(error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
          Card(
            child: Padding(
              padding: const EdgeInsets.all(24),
              child: Column(children: [
                Text((balance?['balance'] ?? 0).toString(), style: const TextStyle(fontSize: 56, fontWeight: FontWeight.w900)),
                const Text('Pacus Points', style: TextStyle(fontSize: 18, fontWeight: FontWeight.w700)),
                const SizedBox(height: 4),
                Text('R\$ ' + (((balance?['brl'] as num?)?.toDouble() ?? 0).toStringAsFixed(2).replaceAll('.', ','))),
              ]),
            ),
          ),
          if (autonomy != null) ...[
            const SizedBox(height: 16),
            Card(
              child: Padding(
                padding: const EdgeInsets.all(20),
                child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                  const Text('Autonomia nesta semana', style: TextStyle(fontSize: 18, fontWeight: FontWeight.w900)),
                  const SizedBox(height: 8),
                  Text('Iniciadas por conta própria: ' + (autonomy!['selfStarted'] ?? autonomy!['selfStartedCount'] ?? 0).toString()),
                  Text('Com lembrete do PACUS: ' + (autonomy!['promptedByPacus'] ?? autonomy!['promptedByPacusCount'] ?? 0).toString()),
                  Text('Com lembrete de adulto: ' + (autonomy!['promptedByAdult'] ?? autonomy!['promptedByAdultCount'] ?? 0).toString()),
                ]),
              ),
            ),
          ],
          const SizedBox(height: 18),
          const Text('Movimentações', style: TextStyle(fontSize: 22, fontWeight: FontWeight.w900)),
          const SizedBox(height: 10),
          if (transactions.isEmpty && loading) const Center(child: CircularProgressIndicator()),
          for (final t in transactions)
            ListTile(
              contentPadding: EdgeInsets.zero,
              title: Text((t['taskTitle'] ?? t['reason'] ?? t['type'] ?? 'Movimentação').toString(), style: const TextStyle(fontWeight: FontWeight.w700)),
              subtitle: Text(t['date']?.toString() ?? ''),
              trailing: Text((((t['points'] as num?)?.toInt() ?? 0) > 0 ? '+' : '') + (t['points'] ?? 0).toString() + ' PP', style: const TextStyle(fontWeight: FontWeight.w900)),
            ),
          if (page < totalPages) OutlinedButton(onPressed: loading ? null : () { page++; _load(); }, child: const Text('Carregar mais')),
        ],
      ),
    ),
  );
}
