import 'package:flutter/material.dart';

import '../api.dart';
import '../models.dart';
import 'history_screen.dart';
import 'home_screen.dart';
import 'pacus_screen.dart';
import 'points_screen.dart';
import 'settings_screen.dart';
import 'store_screen.dart';

class PacusShell extends StatefulWidget {
  const PacusShell({
    super.key,
    required this.api,
    required this.session,
    required this.onLogout,
  });

  final PacusApi api;
  final AuthSession session;
  final Future<void> Function() onLogout;

  @override
  State<PacusShell> createState() => _PacusShellState();
}

class _PacusShellState extends State<PacusShell> {
  int index = 0;

  List<_TabSpec> get tabs => [
        _TabSpec('Hoje', Icons.today_outlined,
            HomeScreen(api: widget.api, session: widget.session, onLogout: widget.onLogout)),
        _TabSpec('Histórico', Icons.history,
            HistoryScreen(api: widget.api)),
        _TabSpec('Pontos', Icons.stars_outlined,
            PointsScreen(api: widget.api)),
        _TabSpec('PACUS', Icons.water,
            PacusScreen(api: widget.api)),
        _TabSpec('Loja', Icons.storefront_outlined,
            StoreScreen(api: widget.api, session: widget.session)),
        if (widget.session.isAdult)
          _TabSpec('Config', Icons.settings_outlined,
              SettingsScreen(api: widget.api, onLogout: widget.onLogout)),
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
                  selectedIndex: index,
                  onDestinationSelected: (value) => setState(() => index = value),
                  labelType: NavigationRailLabelType.all,
                  destinations: [
                    for (final tab in items)
                      NavigationRailDestination(
                        icon: Icon(tab.icon),
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
          body: items[index].screen,
          bottomNavigationBar: NavigationBar(
            selectedIndex: index,
            onDestinationSelected: (value) => setState(() => index = value),
            destinations: [
              for (final tab in items)
                NavigationDestination(
                  icon: Icon(tab.icon),
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
