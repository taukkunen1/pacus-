import 'dart:async';

import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../api.dart';
import '../brand.dart';
import '../models.dart';
import 'activity_screen.dart';
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
  int todayPending = 0;
  int storePending = 0;
  int chatUnread = 0;
  bool refreshingBadges = false;
  Timer? badgeTimer;
  Timer? waterReminderTimer;
  bool waterReminderOpen = false;
  bool waterReminderChecking = false;
  static const waterReminderInterval = Duration(hours: 3);

  String get _waterReminderKey =>
      'pacus.water.reminder.v1:${widget.session.userId}';

  @override
  void initState() {
    super.initState();
    _refreshBadges();
    _checkWaterReminder();
    waterReminderTimer = Timer.periodic(
      const Duration(minutes: 1),
      (_) => _checkWaterReminder(),
    );
    badgeTimer = Timer.periodic(
      const Duration(seconds: 10),
      (_) => _refreshBadges(),
    );
  }

  @override
  void dispose() {
    badgeTimer?.cancel();
    waterReminderTimer?.cancel();
    super.dispose();
  }


  // O aviso nao gera PP. A proxima verificacao persiste por conta no navegador.
  // Com a pagina fechada, push exigiria service worker e permissao explicita.
  Future<void> _checkWaterReminder() async {
    if (!mounted || widget.session.isAdult ||
        waterReminderOpen || waterReminderChecking) {
      return;
    }
    waterReminderChecking = true;
    try {
      final prefs = await SharedPreferences.getInstance();
      final now = DateTime.now();
      final last = prefs.getInt(_waterReminderKey);
      if (last == null || last > now.millisecondsSinceEpoch) {
        await prefs.setInt(_waterReminderKey, now.millisecondsSinceEpoch);
        return;
      }
      if (now.difference(DateTime.fromMillisecondsSinceEpoch(last)) <
          waterReminderInterval) {
        return;
      }
      if (!mounted || waterReminderOpen) return;

      waterReminderOpen = true;
      await prefs.setInt(_waterReminderKey, now.millisecondsSinceEpoch);
      if (!mounted) return;
      await _showWaterReminder();
    } catch (_) {
      // Falha de preferencias nao bloqueia o aplicativo.
    } finally {
      waterReminderChecking = false;
      waterReminderOpen = false;
    }
  }

  Future<void> _showWaterReminder() async {
    final amount = await showDialog<int>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: const Row(
          children: [
            Icon(Icons.water_drop_outlined),
            SizedBox(width: 8),
            Expanded(child: Text('Hora de beber água!')),
          ],
        ),
        content: const Text(
          'Que tal fazer uma pausa para beber água? '
          'Se já bebeu, escolha quanto tomou para registrar no PACUS.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(),
            child: const Text('Ainda não tomei'),
          ),
          PopupMenuButton<int>(
            tooltip: 'Já tomei água',
            onSelected: (ml) => Navigator.of(dialogContext).pop(ml),
            itemBuilder: (_) => const [
              PopupMenuItem(value: 150, child: Text('Já tomei 150 mL')),
              PopupMenuItem(value: 250, child: Text('Já tomei 250 mL')),
              PopupMenuItem(value: 300, child: Text('Já tomei 300 mL')),
              PopupMenuItem(value: 500, child: Text('Já tomei 500 mL')),
            ],
            child: const Padding(
              padding: EdgeInsets.symmetric(horizontal: 12, vertical: 10),
              child: Text('Já tomei água'),
            ),
          ),
        ],
      ),
    );
    if (!mounted || amount == null) return;

    try {
      final eventId =
          '${widget.session.userId}-reminder-${DateTime.now().microsecondsSinceEpoch}';
      await widget.api.postMap('/water', {
        'amountMl': amount,
        'eventId': eventId,
      });
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('$amount mL registrados!')),
      );
      _refreshBadges();
    } catch (_) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Não foi possível registrar a água. Tente pela tela Hoje.'),
        ),
      );
    }
  }

  Future<void> _refreshBadges() async {
    if (refreshingBadges) return;
    refreshingBadges = true;

    try {
      try {
        final chat = await widget.api.getMap('/chat/unread-count');
        final unread = (chat['unreadCount'] as num?)?.toInt() ?? 0;
        final pendingRequests = widget.session.isAdult
            ? (chat['pendingRequests'] as num?)?.toInt() ?? 0
            : 0;
        final value = unread + pendingRequests;
        if (mounted && chatUnread != value) {
          setState(() => chatUnread = value);
        }
      } catch (_) {
        // Falha do badge de chat nao bloqueia a navegacao.
      }

      try {
        if (widget.session.isAdult) {
          final pending = await widget.api.getList('/store/redemptions/pending');
          if (mounted && storePending != pending.length) {
            setState(() => storePending = pending.length);
          }
        } else {
          final today = await widget.api.getToday();
          final water = await widget.api.getMap('/water/today');
          final waterTotal = (water['totalMl'] as num?)?.toInt() ?? 0;
          final waterGoal = (water['goalMl'] as num?)?.toInt() ?? 2000;
          final hydrationPending = waterTotal >= waterGoal ? 0 : 1;
          final pending =
              (today.totalTasks - today.doneTasks + hydrationPending).clamp(0, 999).toInt();
          if (mounted && todayPending != pending) {
            setState(() => todayPending = pending);
          }
        }
      } catch (_) {
        // Badges operacionais sao informativos e nao bloqueiam o app.
      }
    } finally {
      refreshingBadges = false;
    }
  }

  void _selectTab(int value) {
    setState(() => index = value);
    _refreshBadges();
  }

  List<_TabSpec> get tabs => [
        _TabSpec(
          'Hoje',
          Icons.today_outlined,
          HomeScreen(
            api: widget.api,
            session: widget.session,
            onLogout: widget.onLogout,
            onPendingChanged: (value) {
              if (mounted && todayPending != value) setState(() => todayPending = value);
            },
          ),
          badge: widget.session.isAdult ? 0 : todayPending,
        ),
        _TabSpec('Amanhã', Icons.edit_calendar_outlined,
            TomorrowScreen(api: widget.api, session: widget.session)),
        _TabSpec(
          'Chat',
          Icons.chat_bubble_outline,
          ChatScreen(
            api: widget.api,
            session: widget.session,
            onUnreadChanged: (value) {
              if (mounted && chatUnread != value) {
                setState(() => chatUnread = value);
              }
            },
          ),
          badge: chatUnread,
        ),
        _TabSpec('Histórico', Icons.history,
            HistoryScreen(api: widget.api)),
        _TabSpec('Pontos', Icons.stars_outlined,
            PointsScreen(api: widget.api, session: widget.session)),
        _TabSpec('PACUS', Icons.water,
            PacusScreen(api: widget.api, session: widget.session)),
        _TabSpec(
          'Loja',
          Icons.storefront_outlined,
          StoreScreen(
            api: widget.api,
            session: widget.session,
            onPendingChanged: (value) {
              if (mounted && storePending != value) setState(() => storePending = value);
            },
          ),
          badge: widget.session.isAdult ? storePending : 0,
        ),
        if (widget.session.isAdult)
          _TabSpec('Atividade', Icons.receipt_long_outlined,
              ActivityScreen(api: widget.api)),
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
                  onDestinationSelected: _selectTab,
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
            onDestinationSelected: _selectTab,
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

  Widget _navIcon(_TabSpec tab) {
    final icon = Icon(tab.icon);
    if (tab.badge <= 0) return icon;
    return Badge(
      label: Text(tab.badge > 99 ? '99+' : tab.badge.toString()),
      child: icon,
    );
  }
}

class _TabSpec {
  const _TabSpec(this.label, this.icon, this.screen, {this.badge = 0});
  final String label;
  final IconData icon;
  final Widget screen;
  final int badge;
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
