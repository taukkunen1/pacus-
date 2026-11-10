
import 'package:flutter/material.dart';
import '../api.dart';
import '../models.dart';

class PointsScreen extends StatefulWidget {
  const PointsScreen({
    super.key,
    required this.api,
    required this.session,
    required this.onBalanceChanged,
  });
  final PacusApi api;
  final AuthSession session;
  final ValueChanged<Map<String, dynamic>> onBalanceChanged;
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

  Future<void> _adjustBalance() async {
    final balanceController = TextEditingController(text: (balance?['balance'] ?? 0).toString());
    final reasonController = TextEditingController();
    final payload = await showDialog<Map<String, dynamic>>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Ajustar saldo de pontos'),
        content: SizedBox(
          width: 420,
          child: Column(mainAxisSize: MainAxisSize.min, children: [
            TextField(
              controller: balanceController,
              keyboardType: TextInputType.number,
              decoration: const InputDecoration(labelText: 'Novo saldo absoluto'),
            ),
            const SizedBox(height: 10),
            TextField(
              controller: reasonController,
              decoration: const InputDecoration(labelText: 'Motivo do ajuste'),
            ),
          ]),
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context), child: const Text('Cancelar')),
          FilledButton(
            onPressed: () => Navigator.pop(context, {
              'balance': int.tryParse(balanceController.text) ?? 0,
              'reason': reasonController.text.trim().isEmpty ? null : reasonController.text.trim(),
            }),
            child: const Text('Salvar'),
          ),
        ],
      ),
    );
    balanceController.dispose();
    reasonController.dispose();
    if (payload == null) return;

    try {
      await widget.api.request('/points/adjust', method: 'POST', body: payload);
      await _load(reset: true);
    } catch (e) {
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.toString())));
    }
  }

  Future<void> _load({bool reset = false}) async {
    if (!mounted) return;
    if (reset) { page = 1; transactions.clear(); balance = null; autonomy = null; }
    setState(() => loading = true);
    try {
      balance ??= await widget.api.getMap('/points');
      if (!mounted) return;
      widget.onBalanceChanged(balance!);
      try { autonomy ??= await widget.api.getMap('/autonomy/weekly'); } catch (_) {}
      final data = await widget.api.getMap('/points/transactions?page=' + page.toString() + '&pageSize=20');
      transactions.addAll((data['items'] as List? ?? []).map((e) => Map<String, dynamic>.from(e as Map)));
      totalPages = (data['totalPages'] as num?)?.toInt() ?? 1;
      if (mounted) setState(() { loading = false; error = null; });
    } catch (e) {
      if (mounted) setState(() { loading = false; error = e.toString(); });
    }
  }

  @override
  Widget build(BuildContext context) => RefreshIndicator(
      onRefresh: () => _load(reset: true),
      child: ListView(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.fromLTRB(16, 8, 16, 24),
        children: [
          if (error != null) Text(error!, style: TextStyle(color: Theme.of(context).colorScheme.error)),
          if (widget.session.isAdult)
            Align(
              alignment: Alignment.centerLeft,
              child: OutlinedButton.icon(
                onPressed: loading || balance == null ? null : _adjustBalance,
                icon: const Icon(Icons.tune),
                label: const Text('Ajustar saldo'),
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
                  Text('Criadas por mim: ' + (autonomy!['memberCreatedTasks'] ?? 0).toString()),
                  Text('Dias planejados: ' + (autonomy!['plannedDays'] ?? 0).toString()),
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
    );
}
