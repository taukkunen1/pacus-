import 'package:flutter/material.dart';

class PacusPageWidth extends StatelessWidget {
  const PacusPageWidth({
    super.key,
    required this.child,
    this.maxWidth = 1120,
    this.padding = const EdgeInsets.fromLTRB(20, 10, 20, 40),
  });

  final Widget child;
  final double maxWidth;
  final EdgeInsets padding;

  @override
  Widget build(BuildContext context) {
    return Align(
      alignment: Alignment.topCenter,
      child: ConstrainedBox(
        constraints: BoxConstraints(maxWidth: maxWidth),
        child: Padding(
          padding: padding,
          child: child,
        ),
      ),
    );
  }
}

class PacusSectionCard extends StatelessWidget {
  const PacusSectionCard({
    super.key,
    required this.title,
    required this.icon,
    required this.accent,
    required this.child,
    this.trailing,
    this.subtitle,
  });

  final String title;
  final IconData icon;
  final Color accent;
  final Widget child;
  final Widget? trailing;
  final String? subtitle;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;

    return Card(
      clipBehavior: Clip.antiAlias,
      color: scheme.surfaceContainerLowest,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Container(height: 5, color: accent),
          Padding(
            padding: const EdgeInsets.fromLTRB(18, 16, 18, 14),
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                DecoratedBox(
                  decoration: BoxDecoration(
                    color: accent.withValues(alpha: .12),
                    borderRadius: BorderRadius.circular(12),
                  ),
                  child: SizedBox(
                    width: 42,
                    height: 42,
                    child: Icon(icon, color: accent, size: 24),
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(title, style: theme.textTheme.titleLarge),
                      if (subtitle != null && subtitle!.isNotEmpty) ...[
                        const SizedBox(height: 3),
                        Text(
                          subtitle!,
                          style: theme.textTheme.bodyMedium?.copyWith(
                            color: scheme.onSurfaceVariant,
                          ),
                        ),
                      ],
                    ],
                  ),
                ),
                if (trailing != null) trailing!,
              ],
            ),
          ),
          Divider(height: 1, color: scheme.outlineVariant.withValues(alpha: .7)),
          Padding(
            padding: const EdgeInsets.all(14),
            child: child,
          ),
        ],
      ),
    );
  }
}

class PacusBadge extends StatelessWidget {
  const PacusBadge({
    super.key,
    required this.label,
    this.icon,
    this.emphasis = false,
  });

  final String label;
  final IconData? icon;
  final bool emphasis;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final background = emphasis
        ? scheme.primaryContainer
        : scheme.surfaceContainerHigh;
    final foreground = emphasis
        ? scheme.onPrimaryContainer
        : scheme.onSurfaceVariant;

    return Container(
      constraints: const BoxConstraints(minHeight: 30),
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
      decoration: BoxDecoration(
        color: background,
        borderRadius: BorderRadius.circular(999),
        border: Border.all(
          color: emphasis
              ? scheme.primary.withValues(alpha: .35)
              : scheme.outlineVariant,
        ),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          if (icon != null) ...[
            Icon(icon, size: 15, color: foreground),
            const SizedBox(width: 5),
          ],
          Text(
            label,
            style: Theme.of(context).textTheme.labelMedium?.copyWith(
                  color: foreground,
                  fontWeight: FontWeight.w700,
                ),
          ),
        ],
      ),
    );
  }
}

class PacusTaskSurface extends StatelessWidget {
  const PacusTaskSurface({
    super.key,
    required this.done,
    required this.onToggle,
    required this.title,
    required this.body,
    required this.actions,
  });

  final bool done;
  final VoidCallback onToggle;
  final Widget title;
  final Widget body;
  final Widget actions;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final scheme = theme.colorScheme;

    return AnimatedContainer(
      duration: const Duration(milliseconds: 180),
      curve: Curves.easeOut,
      margin: const EdgeInsets.only(bottom: 10),
      decoration: BoxDecoration(
        color: done
            ? scheme.surfaceContainerLow.withValues(alpha: .65)
            : scheme.surfaceContainerLowest,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(
          color: done
              ? scheme.primary.withValues(alpha: .26)
              : scheme.outlineVariant,
        ),
      ),
      child: Padding(
        padding: const EdgeInsets.fromLTRB(12, 10, 8, 10),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Semantics(
              button: true,
              checked: done,
              label: done ? 'Marcar tarefa como pendente' : 'Marcar tarefa como concluída',
              child: InkWell(
                onTap: onToggle,
                borderRadius: BorderRadius.circular(14),
                child: SizedBox(
                  width: 48,
                  height: 48,
                  child: Center(
                    child: AnimatedSwitcher(
                      duration: const Duration(milliseconds: 160),
                      child: Icon(
                        done ? Icons.check_circle_rounded : Icons.radio_button_unchecked_rounded,
                        key: ValueKey(done),
                        color: done ? scheme.primary : scheme.onSurfaceVariant,
                        size: 30,
                      ),
                    ),
                  ),
                ),
              ),
            ),
            const SizedBox(width: 8),
            Expanded(
              child: Padding(
                padding: const EdgeInsets.only(top: 4, bottom: 4),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    title,
                    const SizedBox(height: 7),
                    body,
                  ],
                ),
              ),
            ),
            SizedBox(width: 48, height: 48, child: Center(child: actions)),
          ],
        ),
      ),
    );
  }
}
