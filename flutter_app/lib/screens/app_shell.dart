import 'dart:async';

import 'package:flutter/material.dart';

import '../api.dart';
import '../brand.dart';
import '../models.dart';
import 'chat_screen.dart';
import 'history_screen.dart';
import 'home_screen.dart';
import 'pacus_screen.dart';
import 'points_screen.dart';
import 'settings_screen.dart';
import 'store_screen.dart';
import 'tomorrow_screen.dart';

class PacusShell extends StatefulWidget {
  const PacusShell({
    super.key,
    required this.api,
    required this.session,
    required this.onLogout,
    required this.themeMode,
    required this.onThemeChanged,
  });

  final PacusApi api;
  final AuthSession session;
  final Future<void> Function() onLogout;
  final ThemeMode themeMode;
  final ValueChanged<ThemeMode> onThemeChanged;

  @override
  State<PacusShell> createState() => _PacusShellState();
}

class _PacusShellState extends State<PacusShell> {
  int index = 0;
  Timer? badgeTimer;
  Map<String, int> badges = const {};

  @override
  void initState() {
    super.initState();
    _refreshBadges();
    badgeTimer = Timer.periodic(const Duration(seconds: 20), (_) => _refreshBadges());
  }

  @override
  void dispose() {
    badgeTimer?.cancel();
    super.dispose();
  }

  Future<void> _refreshBadges() async {
    try {
      if (widget.session.isAdult) {
        final pending = await widget.api.getList('/store/redemptions/pending');
        if (mounted) setState(() => badges = {'Loja': pending.length});
      } else {
        final today = await widget.api.getToday();
        final pending = today.tasks.where((task) => !task.isDeleted && !task.isDone).length;
        if (mounted) setState(() => badges = {'Hoje': pending});
      }
    } catch (_) {}
  }

  void _selectIndex(int value) {
    setState(() => index = value);
    _refreshBadges();
  }

  Widget _navIcon(_TabSpec tab) {
    final count = badges[tab.label] ?? 0;
    final icon = Icon(tab.icon);
    if (count <= 0) return icon;
    return Badge(label: Text(count > 99 ? '99+' : count.toString()), child: icon);
  }

  List<_TabSpec> get tabs => [
        _TabSpec('Hoje', Icons.today_outlined,
            HomeScreen(api: widget.api, session: widget.session, onLogout: widget.onLogout)),
        _TabSpec('Amanhã', Icons.edit_calendar_outlined,
            TomorrowScreen(api: widget.api, session: widget.session)),
        _TabSpec('Chat', Icons.chat_bubble_outline,
            ChatScreen(api: widget.api, session: widget.session)),
        _TabSpec('Histórico', Icons.history,
            HistoryScreen(api: widget.api)),
        _TabSpec('Pontos', Icons.stars_outlined,
            PointsScreen(api: widget.api, session: widget.session)),
        _TabSpec('PACUS', Icons.water,
            PacusScreen(api: widget.api, session: widget.session)),
        _TabSpec('Loja', Icons.storefront_outlined,
            StoreScreen(api: widget.api, session: widget.session)),
        if (widget.session.isAdult)
          _TabSpec('Config', Icons.settings_outlined,
              SettingsScreen(api: widget.api, onLogout: widget.onLogout, themeMode: widget.themeMode, onThemeChanged: widget.onThemeChanged)),
      ];

  @override
  Widget build(BuildContext context) {
    final items = tabs;
    if (index >= items.length) index = 0;

    return LayoutBuilder(
      builder: (context, constraints) {
        final wide = constraints.maxWidth >= 900;
        if (wide) {
          return Scaffold(
            body: Row(
              children: [
                NavigationRail(
                  leading: Padding(
                    padding: const EdgeInsets.only(top: 14, bottom: 12),
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        const PacusBrand(showName: false),
                        const SizedBox(height: 10),
                        _ThemeMenu(
                          themeMode: widget.themeMode,
                          onChanged: widget.onThemeChanged,
                        ),
                      ],
                    ),
                  ),
                  selectedIndex: index,
                  onDestinationSelected: _selectIndex,
                  labelType: NavigationRailLabelType.all,
                  destinations: [
                    for (final tab in items)
                      NavigationRailDestination(
                        icon: _navIcon(tab),
                        label: Text(tab.label),
                      ),
                  ],
                ),
                const VerticalDivider(width: 1),
                Expanded(child: items[index].screen),
              ],
            ),
          );
        }

        return Scaffold(
          body: Stack(
            children: [
              Positioned.fill(child: items[index].screen),
              Positioned(
                top: 8,
                right: 10,
                child: SafeArea(
                  child: _ThemeMenu(
                    themeMode: widget.themeMode,
                    onChanged: widget.onThemeChanged,
                    compact: true,
                  ),
                ),
              ),
            ],
          ),
          bottomNavigationBar: NavigationBar(
            labelBehavior: NavigationDestinationLabelBehavior.onlyShowSelected,
            selectedIndex: index,
            onDestinationSelected: _selectIndex,
            destinations: [
              for (final tab in items)
                NavigationDestination(
                  icon: _navIcon(tab),
                  label: tab.label,
                ),
            ],
          ),
        );
      },
    );
  }
}

class _TabSpec {
  const _TabSpec(this.label, this.icon, this.screen);
  final String label;
  final IconData icon;
  final Widget screen;
}


class _ThemeMenu extends StatelessWidget {
  const _ThemeMenu({
    required this.themeMode,
    required this.onChanged,
    this.compact = false,
  });

  final ThemeMode themeMode;
  final ValueChanged<ThemeMode> onChanged;
  final bool compact;

  IconData get _icon => switch (themeMode) {
        ThemeMode.light => Icons.light_mode_outlined,
        ThemeMode.dark => Icons.dark_mode_outlined,
        ThemeMode.system => Icons.brightness_auto_outlined,
      };

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    return Material(
      color: compact ? scheme.surfaceContainerLow.withValues(alpha: .94) : Colors.transparent,
      borderRadius: BorderRadius.circular(14),
      child: PopupMenuButton<ThemeMode>(
        tooltip: 'Aparência',
        initialValue: themeMode,
        onSelected: onChanged,
        icon: Icon(_icon),
        itemBuilder: (_) => const [
          PopupMenuItem(
            value: ThemeMode.light,
            child: ListTile(
              dense: true,
              leading: Icon(Icons.light_mode_outlined),
              title: Text('Modo diurno'),
            ),
          ),
          PopupMenuItem(
            value: ThemeMode.dark,
            child: ListTile(
              dense: true,
              leading: Icon(Icons.dark_mode_outlined),
              title: Text('Modo noturno'),
            ),
          ),
          PopupMenuItem(
            value: ThemeMode.system,
            child: ListTile(
              dense: true,
              leading: Icon(Icons.brightness_auto_outlined),
              title: Text('Seguir dispositivo'),
            ),
          ),
        ],
      ),
    );
  }
}
