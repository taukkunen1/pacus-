
import 'package:flutter/material.dart';
import '../api.dart';
import '../models.dart';

class StoreScreen extends StatefulWidget {
  const StoreScreen({
    super.key,
    required this.api,
    required this.session,
    this.onPendingChanged,
  });
  final PacusApi api;
  final AuthSession session;
  final ValueChanged<int>? onPendingChanged;
  @override State<StoreScreen> createState() => _StoreScreenState();
}

class _StoreScreenState extends State<StoreScreen> {
  List<Map<String, dynamic>> items = [];
  List<Map<String, dynamic>> pending = [];
  int balance = 0;
  bool loading = true;
  String? error;

  @override void initState() { super.initState(); _load(); }

  Future<void> _load() async {
    setState(() => loading = true);
    try {
      final rawItems = await widget.api.getList(widget.session.isAdult ? '/store/items/all' : '/store/items');
      final points = await widget.api.getMap('/points');
      final rawPending = widget.session.isAdult ? await widget.api.getList('/store/redemptions/pending') : <dynamic>[];
      if (!mounted) return;
      final pendingItems = rawPending.map((e) => Map<String, dynamic>.from(e as Map)).toList();
      widget.onPendingChanged?.call(pendingItems.length);
      setState(() {
        items = rawItems.map((e) => Map<String, dynamic>.from(e as Map)).toList();
        pending = pendingItems;
        balance = (points['balance'] as num?)?.toInt() ?? 0;
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

  Future<void> _redeem(String id) async {
    try {
      await widget.api.request('/store/redemptions', method: 'POST', body: {'storeItemId': id});
      _snack('Resgate solicitado. Aguarde a aprovação de um adulto.');
      await _load();
    } catch (e) { _snack(e.toString()); }
  }

  Future<void> _review(String id, bool approve) async {
    try {
      await widget.api.request('/store/redemptions/' + id + (approve ? '/approve' : '/reject'), method: 'PUT');
      _snack(approve ? 'Resgate aprovado.' : 'Resgate rejeitado.');
      await _load();
    } catch (e) { _snack(e.toString()); }
  }

  Future<void> _toggle(Map<String, dynamic> item) async {
    try {
      await widget.api.request('/store/items/' + item['id'].toString() + '/active',
        method: 'PUT', body: {'active': item['active'] != true});
      await _load();
    } catch (e) { _snack(e.toString()); }
  }

  Future<void> _edit([Map<String, dynamic>? item]) async {
    final title = TextEditingController(text: item?['title']?.toString() ?? '');
    final description = TextEditingController(text: item?['description']?.toString() ?? '');
    final cost = TextEditingController(text: (item?['cost'] ?? 100).toString());
    final icon = TextEditingController(text: item?['icon']?.toString() ?? '🎁');
    final stock = TextEditingController(text: item?['stock']?.toString() ?? '');
    final daily = TextEditingController(text: item?['dailyLimit']?.toString() ?? '');
    final screen = TextEditingController(text: item?['screenTimeMinutes']?.toString() ?? '');
    String category = item?['category']?.toString() ?? 'other';

    final result = await showDialog<Map<String, dynamic>>(
      context: context,
      builder: (context) => StatefulBuilder(
        builder: (context, setDialog) => AlertDialog(
          title: Text(item == null ? 'Novo item' : 'Editar item'),
          content: SizedBox(
            width: 480,
            child: SingleChildScrollView(
              child: Column(mainAxisSize: MainAxisSize.min, children: [
                TextField(controller: title, decoration: const InputDecoration(labelText: 'Título')),
                const SizedBox(height: 10),
                TextField(controller: description, decoration: const InputDecoration(labelText: 'Descrição')),
                const SizedBox(height: 10),
                Row(children: [
                  Expanded(child: TextField(controller: cost, keyboardType: TextInputType.number, decoration: const InputDecoration(labelText: 'Custo em PP'))),
                  const SizedBox(width: 10),
                  Expanded(child: TextField(controller: icon, decoration: const InputDecoration(labelText: 'Ícone'))),
                ]),
                const SizedBox(height: 10),
                DropdownButtonFormField<String>(
                  initialValue: category,
                  decoration: const InputDecoration(labelText: 'Categoria'),
                  items: const [
                    DropdownMenuItem(value: 'screen_time', child: Text('Tempo de tela')),
                    DropdownMenuItem(value: 'toy', child: Text('Brinquedo')),
                    DropdownMenuItem(value: 'activity', child: Text('Atividade')),
                    DropdownMenuItem(value: 'other', child: Text('Outro')),
                  ],
                  onChanged: (v) => setDialog(() => category = v ?? 'other'),
                ),
                const SizedBox(height: 10),
                Row(children: [
                  Expanded(child: TextField(controller: stock, keyboardType: TextInputType.number, decoration: const InputDecoration(labelText: 'Estoque opcional'))),
                  const SizedBox(width: 10),
                  Expanded(child: TextField(controller: daily, keyboardType: TextInputType.number, decoration: const InputDecoration(labelText: 'Limite/dia'))),
                ]),
                const SizedBox(height: 10),
                TextField(controller: screen, keyboardType: TextInputType.number, decoration: const InputDecoration(labelText: 'Minutos de tela concedidos')),
              ]),
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
                  'cost': int.tryParse(cost.text) ?? 0,
                  'category': category,
                  'icon': icon.text.trim().isEmpty ? null : icon.text.trim(),
                  'stock': int.tryParse(stock.text),
                  'dailyLimit': int.tryParse(daily.text),
                  'screenTimeMinutes': int.tryParse(screen.text),
                });
              },
              child: const Text('Salvar'),
            ),
          ],
        ),
      ),
    );

    title.dispose(); description.dispose(); cost.dispose(); icon.dispose(); stock.dispose(); daily.dispose(); screen.dispose();
    if (result == null) return;

    try {
      if (item == null) {
        await widget.api.request('/store/items', method: 'POST', body: result);
      } else {
        await widget.api.request('/store/items/' + item['id'].toString(), method: 'PUT', body: result);
      }
      await _load();
    } catch (e) { _snack(e.toString()); }
  }

  String _category(dynamic value) {
    const labels = {'screen_time':'Tempo de tela','toy':'Brinquedo','activity':'Atividade','other':'Outro'};
    return labels[value?.toString()] ?? value?.toString() ?? 'Outro';
  }

  @override Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(
      title: const Text('Loja'),
      actions: [if (widget.session.isAdult) IconButton(onPressed: () => _edit(), icon: const Icon(Icons.add), tooltip: 'Novo item')],
    ),
    body: RefreshIndicator(
      onRefresh: _load,
      child: ListView(
        padding: const EdgeInsets.all(18),
        children: [
          Card(
            child: Padding(
              padding: const EdgeInsets.all(22),
              child: Column(children: [
                Text(balance.toString(), style: const TextStyle(fontSize: 44, fontWeight: FontWeight.w900)),
                const Text('Pacus Points'),
              ]),
            ),
          ),
          if (error != null) Padding(padding: const EdgeInsets.only(top: 12), child: Text(error!, style: TextStyle(color: Theme.of(context).colorScheme.error))),
          const SizedBox(height: 18),
          const Text('Itens', style: TextStyle(fontSize: 22, fontWeight: FontWeight.w900)),
          const SizedBox(height: 10),
          if (items.isEmpty && loading) const Center(child: CircularProgressIndicator()),
          for (final item in items) ...[
            Card(
              child: Padding(
                padding: const EdgeInsets.all(16),
                child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
                  Row(children: [
                    Text(item['icon']?.toString() ?? '🎁', style: const TextStyle(fontSize: 28)),
                    const SizedBox(width: 10),
                    Expanded(child: Text(item['title']?.toString() ?? 'Item', style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w900))),
                    Text((item['cost'] ?? 0).toString() + ' PP', style: const TextStyle(fontWeight: FontWeight.w900)),
                  ]),
                  if ((item['description']?.toString() ?? '').isNotEmpty) ...[
                    const SizedBox(height: 6),
                    Text(item['description'].toString()),
                  ],
                  const SizedBox(height: 8),
                  Wrap(spacing: 8, runSpacing: 6, children: [
                    Chip(label: Text(_category(item['category']))),
                    if (item['dailyLimit'] != null) Chip(label: Text('limite ' + item['dailyLimit'].toString() + 'x/dia')),
                    if (item['screenTimeMinutes'] != null) Chip(label: Text('+' + item['screenTimeMinutes'].toString() + ' min de tela')),
                    if (item['active'] == false) const Chip(label: Text('desativado')),
                  ]),
                  const SizedBox(height: 8),
                  Wrap(spacing: 8, children: [
                    if (item['active'] != false)
                      FilledButton.tonal(
                        onPressed: balance >= ((item['cost'] as num?)?.toInt() ?? 0) ? () => _redeem(item['id'].toString()) : null,
                        child: const Text('Resgatar'),
                      ),
                    if (widget.session.isAdult) ...[
                      OutlinedButton(onPressed: () => _edit(item), child: const Text('Editar')),
                      OutlinedButton(onPressed: () => _toggle(item), child: Text(item['active'] == false ? 'Reativar' : 'Desativar')),
                    ],
                  ]),
                ]),
              ),
            ),
            const SizedBox(height: 10),
          ],
          if (widget.session.isAdult) ...[
            const SizedBox(height: 14),
            const Text('Aguardando aprovação', style: TextStyle(fontSize: 22, fontWeight: FontWeight.w900)),
            const SizedBox(height: 10),
            if (pending.isEmpty) const Text('Nenhum resgate pendente.'),
            for (final p in pending)
              Card(
                child: ListTile(
                  title: Text(p['itemTitle']?.toString() ?? 'Item'),
                  subtitle: Text((p['cost'] ?? 0).toString() + ' PP'),
                  trailing: Wrap(spacing: 6, children: [
                    IconButton(onPressed: () => _review(p['id'].toString(), true), icon: const Icon(Icons.check_circle), tooltip: 'Aprovar'),
                    IconButton(onPressed: () => _review(p['id'].toString(), false), icon: const Icon(Icons.cancel_outlined), tooltip: 'Rejeitar'),
                  ]),
                ),
              ),
          ],
        ],
      ),
    ),
  );
}
