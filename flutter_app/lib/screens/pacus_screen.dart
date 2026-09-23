
import 'dart:math' as math;
import 'package:flutter/material.dart';
import '../api.dart';
import '../models.dart';

class PacusScreen extends StatefulWidget {
  const PacusScreen({super.key, required this.api, required this.session});
  final PacusApi api;
  final AuthSession session;
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

  Future<void> _editState() async {
    final size = TextEditingController(text: ((pacus?['size'] as num?)?.toDouble() ?? 0).toString());
    final days = TextEditingController(text: (pacus?['totalClosedDays'] ?? 0).toString());
    final hue = TextEditingController(text: pacus?['colorHue']?.toString() ?? '');
    String stage = _stageKey(pacus?['stage']);

    final payload = await showDialog<Map<String, dynamic>>(
      context: context,
      builder: (context) => StatefulBuilder(
        builder: (context, setDialog) => AlertDialog(
          title: const Text('Ajustar estado do PACUS'),
          content: SizedBox(
            width: 440,
            child: Column(mainAxisSize: MainAxisSize.min, children: [
              DropdownButtonFormField<String>(
                initialValue: stage,
                decoration: const InputDecoration(labelText: 'Estágio'),
                items: const [
                  DropdownMenuItem(value: 'egg', child: Text('Ovo')),
                  DropdownMenuItem(value: 'cracking', child: Text('Rachando')),
                  DropdownMenuItem(value: 'hatching', child: Text('Eclosão')),
                  DropdownMenuItem(value: 'baby', child: Text('Bebê')),
                  DropdownMenuItem(value: 'young', child: Text('Jovem')),
                  DropdownMenuItem(value: 'adult', child: Text('Adulto')),
                ],
                onChanged: (v) => setDialog(() => stage = v ?? stage),
              ),
              const SizedBox(height: 10),
              TextField(controller: size, keyboardType: TextInputType.number, decoration: const InputDecoration(labelText: 'Tamanho')),
              const SizedBox(height: 10),
              TextField(controller: days, keyboardType: TextInputType.number, decoration: const InputDecoration(labelText: 'Dias vividos')),
              const SizedBox(height: 10),
              TextField(controller: hue, keyboardType: TextInputType.number, decoration: const InputDecoration(labelText: 'Cor (0–359, vazio mantém)')),
            ]),
          ),
          actions: [
            TextButton(onPressed: () => Navigator.pop(context), child: const Text('Cancelar')),
            FilledButton(
              onPressed: () => Navigator.pop(context, {
                'stage': stage,
                'size': double.tryParse(size.text),
                'totalClosedDays': int.tryParse(days.text),
                if (hue.text.trim().isNotEmpty) 'colorHue': int.tryParse(hue.text),
              }),
              child: const Text('Salvar'),
            ),
          ],
        ),
      ),
    );
    size.dispose(); days.dispose(); hue.dispose();
    if (payload == null) return;

    try {
      await widget.api.request('/pacus/me/state', method: 'PUT', body: payload);
      await _load();
    } catch (e) {
      if (mounted) ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text(e.toString())));
    }
  }

  String _stageKey(dynamic raw) {
    final key = raw?.toString().toLowerCase() ?? 'egg';
    const valid = {'egg', 'cracking', 'hatching', 'baby', 'young', 'juvenile', 'adult'};
    if (!valid.contains(key)) return 'egg';
    return key == 'juvenile' ? 'young' : key;
  }

  String _stage(dynamic raw) {
    const labels = {
      'egg': 'Ovo',
      'cracking': 'Rachando',
      'hatching': 'Eclosão',
      'baby': 'Bebê',
      'young': 'Jovem',
      'adult': 'Adulto',
    };
    return labels[_stageKey(raw)] ?? 'Ovo';
  }

  double _visualScale(String stage) {
    return switch (stage) {
      'baby' => .60,
      'young' => .82,
      'adult' => 1.0,
      _ => 1.0,
    };
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
                decoration: BoxDecoration(
                  gradient: LinearGradient(
                    begin: Alignment.topCenter,
                    end: Alignment.bottomCenter,
                    colors: Theme.of(context).brightness == Brightness.dark
                        ? const [Color(0xFF17333A), Color(0xFF0D2025)]
                        : const [Color(0xFFC8EFF5), Color(0xFF65B4C2)],
                  ),
                ),
                child: AnimatedBuilder(
                  animation: swim,
                  builder: (context, _) {
                    final stage = _stageKey(pacus?['stage']);
                    final isEggPhase = stage == 'egg' || stage == 'cracking' || stage == 'hatching';
                    final x = -0.72 + (swim.value * 1.44);
                    final wave = (swim.value - .5).abs();

                    return Stack(children: [
                      const Positioned(left: 20, top: 28, child: Text('○', style: TextStyle(fontSize: 30, color: Colors.white70))),
                      const Positioned(right: 40, top: 60, child: Text('○', style: TextStyle(fontSize: 20, color: Colors.white60))),
                      const Positioned(left: 55, bottom: 8, child: Text('🌿', style: TextStyle(fontSize: 54))),
                      const Positioned(right: 30, bottom: 4, child: Text('🪨', style: TextStyle(fontSize: 48))),
                      if (isEggPhase)
                        Align(
                          alignment: const Alignment(0, .30),
                          child: Transform.rotate(
                            angle: stage == 'egg' ? 0 : math.sin(swim.value * math.pi * 2) * .025,
                            child: SizedBox(
                              width: 105,
                              height: 135,
                              child: CustomPaint(painter: _EggPainter(stage)),
                            ),
                          ),
                        )
                      else
                        Align(
                          alignment: Alignment(x, .05 + wave * .22),
                          child: Transform.scale(
                            scale: _visualScale(stage),
                            child: Transform(
                              alignment: Alignment.center,
                              transform: Matrix4.diagonal3Values(
                                swim.status == AnimationStatus.reverse ? -1 : 1,
                                1,
                                1,
                              ),
                              child: const SizedBox(
                                width: 150,
                                height: 100,
                                child: CustomPaint(painter: _AxolotlPainter()),
                              ),
                            ),
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
                  Text('Axolote · ' + _stage(pacus?['stage']), style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w700)),
                ]),
              ),
            ]),
          ),
          if (widget.session.isAdult) ...[
            const SizedBox(height: 12),
            OutlinedButton.icon(onPressed: _editState, icon: const Icon(Icons.tune), label: const Text('Ajustar PACUS')),
          ],
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


class _EggPainter extends CustomPainter {
  const _EggPainter(this.stage);

  final String stage;

  @override
  void paint(Canvas canvas, Size size) {
    final shell = Paint()..color = const Color(0xFFF3E8D7);
    final shellShade = Paint()..color = const Color(0xFFD9C8B2);
    final crack = Paint()
      ..color = const Color(0xFF8C7764)
      ..style = PaintingStyle.stroke
      ..strokeWidth = 3
      ..strokeCap = StrokeCap.round
      ..strokeJoin = StrokeJoin.round;

    final center = Offset(size.width * .5, size.height * .54);
    final eggRect = Rect.fromCenter(
      center: center,
      width: size.width * .72,
      height: size.height * .82,
    );

    canvas.drawOval(eggRect, shell);
    canvas.drawArc(
      eggRect.deflate(5),
      .35,
      2.15,
      false,
      shellShade
        ..style = PaintingStyle.stroke
        ..strokeWidth = 3,
    );

    if (stage == 'cracking' || stage == 'hatching') {
      final first = Path()
        ..moveTo(size.width * .50, size.height * .18)
        ..lineTo(size.width * .44, size.height * .31)
        ..lineTo(size.width * .54, size.height * .39)
        ..lineTo(size.width * .46, size.height * .50);
      canvas.drawPath(first, crack);
    }

    if (stage == 'hatching') {
      final second = Path()
        ..moveTo(size.width * .54, size.height * .39)
        ..lineTo(size.width * .67, size.height * .33)
        ..lineTo(size.width * .72, size.height * .45);
      canvas.drawPath(second, crack);

      final third = Path()
        ..moveTo(size.width * .46, size.height * .50)
        ..lineTo(size.width * .35, size.height * .58)
        ..lineTo(size.width * .40, size.height * .69);
      canvas.drawPath(third, crack);

      final baby = Paint()..color = const Color(0xFFF4A7B9);
      final eye = Paint()..color = const Color(0xFF34252B);
      canvas.drawCircle(
        Offset(size.width * .51, size.height * .30),
        size.width * .105,
        baby,
      );
      canvas.drawCircle(
        Offset(size.width * .47, size.height * .285),
        2.2,
        eye,
      );
      canvas.drawCircle(
        Offset(size.width * .55, size.height * .285),
        2.2,
        eye,
      );
    }
  }

  @override
  bool shouldRepaint(covariant _EggPainter oldDelegate) => oldDelegate.stage != stage;
}


class _AxolotlPainter extends CustomPainter {
  const _AxolotlPainter();

  @override
  void paint(Canvas canvas, Size size) {
    final body = Paint()..color = const Color(0xFFF4A7B9);
    final dark = Paint()..color = const Color(0xFF8A4560);
    final gill = Paint()
      ..color = const Color(0xFFE66B8C)
      ..strokeWidth = 6
      ..strokeCap = StrokeCap.round;
    final eye = Paint()..color = const Color(0xFF34252B);

    final center = Offset(size.width * .53, size.height * .52);
    canvas.drawOval(
      Rect.fromCenter(center: center, width: size.width * .58, height: size.height * .5),
      body,
    );

    final head = Offset(size.width * .28, size.height * .50);
    canvas.drawCircle(head, size.height * .23, body);

    for (final dy in [-.15, 0.0, .15]) {
      final start = Offset(size.width * .16, size.height * (.50 + dy));
      final end = Offset(size.width * .03, size.height * (.42 + dy));
      canvas.drawLine(start, end, gill);
      canvas.drawCircle(end, 4, gill);
    }

    for (final dy in [-.15, 0.0, .15]) {
      final start = Offset(size.width * .18, size.height * (.50 + dy));
      final end = Offset(size.width * .07, size.height * (.60 + dy));
      canvas.drawLine(start, end, gill);
      canvas.drawCircle(end, 4, gill);
    }

    canvas.drawCircle(Offset(size.width * .22, size.height * .44), 4, eye);
    canvas.drawCircle(Offset(size.width * .34, size.height * .44), 4, eye);

    final mouth = Path()
      ..moveTo(size.width * .24, size.height * .57)
      ..quadraticBezierTo(size.width * .28, size.height * .61, size.width * .33, size.height * .57);
    canvas.drawPath(
      mouth,
      Paint()
        ..color = dark.color
        ..style = PaintingStyle.stroke
        ..strokeWidth = 2,
    );

    final tail = Path()
      ..moveTo(size.width * .77, size.height * .42)
      ..quadraticBezierTo(size.width * .98, size.height * .30, size.width * .96, size.height * .56)
      ..quadraticBezierTo(size.width * .92, size.height * .76, size.width * .76, size.height * .60)
      ..close();
    canvas.drawPath(tail, body);

    final leg = Paint()
      ..color = body.color
      ..strokeWidth = 8
      ..strokeCap = StrokeCap.round;
    for (final x in [.42, .62]) {
      canvas.drawLine(
        Offset(size.width * x, size.height * .66),
        Offset(size.width * (x - .05), size.height * .84),
        leg,
      );
      canvas.drawLine(
        Offset(size.width * (x + .06), size.height * .66),
        Offset(size.width * (x + .10), size.height * .84),
        leg,
      );
    }

    final bubble = Paint()
      ..color = Colors.white.withValues(alpha: .55)
      ..style = PaintingStyle.stroke
      ..strokeWidth = 2;
    for (final p in [
      const Offset(.72, .18),
      const Offset(.82, .10),
      const Offset(.88, .26),
    ]) {
      canvas.drawCircle(Offset(size.width * p.dx, size.height * p.dy), 5 + math.sin(p.dx * 10).abs() * 3, bubble);
    }
  }

  @override
  bool shouldRepaint(covariant CustomPainter oldDelegate) => false;
}
