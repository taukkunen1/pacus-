import 'package:flutter/material.dart';

import '../api.dart';
import '../models.dart';
import 'points_screen.dart';
import 'store_screen.dart';

enum _RewardsSection { store, statement }

class RewardsScreen extends StatefulWidget {
  const RewardsScreen({
    super.key,
    required this.api,
    required this.session,
    this.onPendingChanged,
  });

  final PacusApi api;
  final AuthSession session;
  final ValueChanged<int>? onPendingChanged;

  @override
  State<RewardsScreen> createState() => _RewardsScreenState();
}

class _RewardsScreenState extends State<RewardsScreen> {
  _RewardsSection _section = _RewardsSection.store;
  int? _points;
  String? _brl;

  void _updateBalance(Map<String, dynamic> balance) {
    final points = (balance['balance'] as num?)?.toInt();
    final brl = (balance['brl'] as num?)?.toStringAsFixed(2).replaceAll('.', ',');
    if (!mounted || (_points == points && _brl == brl)) return;
    setState(() {
      _points = points;
      _brl = brl;
    });
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colors = theme.colorScheme;
    final brl = _brl;

    return Scaffold(
      appBar: AppBar(title: const Text('Pontos e Loja')),
      body: SafeArea(
        top: false,
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 760),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Padding(
                  padding: const EdgeInsets.fromLTRB(16, 4, 16, 12),
                  child: Card(
                    margin: EdgeInsets.zero,
                    color: colors.primaryContainer,
                    child: Padding(
                      padding: const EdgeInsets.all(16),
                      child: Row(
                        children: [
                          CircleAvatar(
                            backgroundColor: colors.primary,
                            foregroundColor: colors.onPrimary,
                            child: const Icon(Icons.stars_outlined),
                          ),
                          const SizedBox(width: 12),
                          Expanded(
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Text(
                                  'Saldo de Pacus Points',
                                  style: theme.textTheme.labelLarge?.copyWith(
                                    color: colors.onPrimaryContainer,
                                  ),
                                ),
                                Text(
                                  _points == null ? '—' : '$_points PP',
                                  style: theme.textTheme.headlineSmall?.copyWith(
                                    color: colors.onPrimaryContainer,
                                    fontWeight: FontWeight.bold,
                                  ),
                                ),
                                if (_section == _RewardsSection.statement &&
                                    brl != null)
                                  Text(
                                    'R\$ $brl',
                                    style: TextStyle(
                                      color: colors.onPrimaryContainer,
                                    ),
                                  ),
                              ],
                            ),
                          ),
                        ],
                      ),
                    ),
                  ),
                ),
                Padding(
                  padding: const EdgeInsets.fromLTRB(16, 0, 16, 8),
                  child: SegmentedButton<_RewardsSection>(
                    showSelectedIcon: false,
                    segments: const [
                      ButtonSegment(
                        value: _RewardsSection.store,
                        label: Text('Loja'),
                        icon: Icon(Icons.storefront_outlined),
                      ),
                      ButtonSegment(
                        value: _RewardsSection.statement,
                        label: Text('Extrato'),
                        icon: Icon(Icons.receipt_long_outlined),
                      ),
                    ],
                    selected: {_section},
                    onSelectionChanged: (selection) {
                      setState(() => _section = selection.single);
                    },
                  ),
                ),
                Expanded(
                  child: _section == _RewardsSection.store
                      ? StoreScreen(
                          api: widget.api,
                          session: widget.session,
                          onPendingChanged: widget.onPendingChanged,
                          onBalanceChanged: _updateBalance,
                        )
                      : PointsScreen(
                          api: widget.api,
                          session: widget.session,
                          onBalanceChanged: _updateBalance,
                        ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
