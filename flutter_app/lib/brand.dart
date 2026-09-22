import 'package:flutter/material.dart';

class PacusBrand extends StatelessWidget {
  const PacusBrand({
    super.key,
    this.compact = false,
    this.showName = true,
  });

  final bool compact;
  final bool showName;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final size = compact ? 30.0 : 38.0;

    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Container(
          width: size,
          height: size,
          decoration: BoxDecoration(
            shape: BoxShape.circle,
            gradient: LinearGradient(
              begin: Alignment.topLeft,
              end: Alignment.bottomRight,
              colors: [
                scheme.primary,
                scheme.tertiary,
              ],
            ),
            boxShadow: [
              BoxShadow(
                color: scheme.primary.withValues(alpha: .22),
                blurRadius: 10,
                offset: const Offset(0, 4),
              ),
            ],
          ),
          child: CustomPaint(
            painter: _PacusMarkPainter(
              foreground: scheme.onPrimary,
            ),
          ),
        ),
        if (showName) ...[
          SizedBox(width: compact ? 8 : 10),
          Text(
            'PACUS',
            style: Theme.of(context).textTheme.titleLarge?.copyWith(
                  fontWeight: FontWeight.w900,
                  letterSpacing: 1.3,
                ),
          ),
        ],
      ],
    );
  }
}

class _PacusMarkPainter extends CustomPainter {
  const _PacusMarkPainter({required this.foreground});

  final Color foreground;

  @override
  void paint(Canvas canvas, Size size) {
    final p = Paint()
      ..color = foreground
      ..style = PaintingStyle.stroke
      ..strokeWidth = size.width * .075
      ..strokeCap = StrokeCap.round
      ..strokeJoin = StrokeJoin.round;

    final cx = size.width * .50;
    final cy = size.height * .50;

    final body = Rect.fromCenter(
      center: Offset(cx, cy),
      width: size.width * .44,
      height: size.height * .25,
    );
    canvas.drawOval(body, p);

    final head = Offset(size.width * .36, cy);
    canvas.drawCircle(head, size.width * .105, p);

    for (final dy in [-.11, 0.0, .11]) {
      final start = Offset(size.width * .28, size.height * (.50 + dy));
      final end = Offset(size.width * .16, size.height * (.42 + dy));
      canvas.drawLine(start, end, p);
    }

    final tail = Path()
      ..moveTo(size.width * .67, size.height * .44)
      ..quadraticBezierTo(
        size.width * .85,
        size.height * .31,
        size.width * .83,
        size.height * .54,
      )
      ..quadraticBezierTo(
        size.width * .80,
        size.height * .68,
        size.width * .66,
        size.height * .56,
      );
    canvas.drawPath(tail, p);

    canvas.drawCircle(
      Offset(size.width * .33, size.height * .47),
      size.width * .017,
      Paint()..color = foreground,
    );
  }

  @override
  bool shouldRepaint(covariant _PacusMarkPainter oldDelegate) =>
      oldDelegate.foreground != foreground;
}
