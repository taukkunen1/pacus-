
import 'package:flutter/material.dart';
import '../api.dart';

class PacusScreen extends StatefulWidget {
  const PacusScreen({super.key, required this.api});
  final PacusApi api;
  @override State<PacusScreen> createState() => _PacusScreenState();
}

class _PacusScreenState extends State<PacusScreen> with SingleTickerProviderStateMixin {
  Map<String, dynamic>? pacus;
  String? error;
  late final AnimationController swim;

  @override void initState() {
    super.initState();
    swim = AnimationController(vsync: this, duration: const Duration(seconds: 7))..repeat(reverse: true);
    _load();
  }

  @override void dispose() {
    swim.dispose();
    super.dispose();
  }

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
            clipBehavior: Clip.antiAlias,
            child: Column(children: [
              Container(
                height: 260,
                decoration: const BoxDecoration(
                  gradient: LinearGradient(
                    begin: Alignment.topCenter,
                    end: Alignment.bottomCenter,
                    colors: [Color(0xFFBDE8F2), Color(0xFF4FA7B8)],
                  ),
                ),
                child: AnimatedBuilder(
                  animation: swim,
                  builder: (context, _) {
                    final x = -0.72 + (swim.value * 1.44);
                    final wave = (swim.value - .5).abs();
                    return Stack(children: [
                      const Positioned(left: 20, top: 28, child: Text('○', style: TextStyle(fontSize: 30, color: Colors.white70))),
                      const Positioned(right: 40, top: 60, child: Text('○', style: TextStyle(fontSize: 20, color: Colors.white60))),
                      const Positioned(left: 55, bottom: 8, child: Text('🌿', style: TextStyle(fontSize: 54))),
                      const Positioned(right: 30, bottom: 4, child: Text('🪨', style: TextStyle(fontSize: 48))),
                      Align(
                        alignment: Alignment(x, .05 + wave * .22),
                        child: Transform.scale(
                          scaleX: swim.status == AnimationStatus.reverse ? -1 : 1,
                          child: const Text('🐟', style: TextStyle(fontSize: 92)),
                        ),
                      ),
                    ]);
                  },
                ),
              ),
              Padding(
                padding: const EdgeInsets.all(20),
                child: Column(children: [
                  Text(pacus?['name']?.toString() ?? 'Pacus', style: const TextStyle(fontSize: 30, fontWeight: FontWeight.w900)),
                  Text(_stage(pacus?['stage']), style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w700)),
                ]),
              ),
            ]),
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
