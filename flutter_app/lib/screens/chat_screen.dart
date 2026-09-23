import 'dart:async';

import 'package:flutter/material.dart';

import '../api.dart';
import '../models.dart';
import '../ui/chat_utils.dart';

class ChatScreen extends StatefulWidget {
  const ChatScreen({
    super.key,
    required this.api,
    required this.session,
    this.onUnreadChanged,
  });

  final PacusApi api;
  final AuthSession session;
  final ValueChanged<int>? onUnreadChanged;

  @override
  State<ChatScreen> createState() => _ChatScreenState();
}

class _ChatScreenState extends State<ChatScreen> {
  final _messages = <Map<String, dynamic>>[];
  final _messageController = TextEditingController();
  final _scrollController = ScrollController();
  final _focusNode = FocusNode();

  Timer? _pollTimer;
  bool _loading = true;
  bool _refreshing = false;
  bool _sending = false;
  bool _markingRead = false;
  String? _error;

  @override
  void initState() {
    super.initState();
    _scrollController.addListener(_handleScroll);
    _loadInitial();
    _pollTimer = Timer.periodic(
      const Duration(seconds: 2),
      (_) => _refreshNewMessages(),
    );
  }

  @override
  void dispose() {
    _pollTimer?.cancel();
    _scrollController.removeListener(_handleScroll);
    _messageController.dispose();
    _scrollController.dispose();
    _focusNode.dispose();
    super.dispose();
  }

  Future<void> _loadInitial() async {
    try {
      final data = await widget.api.getList('/chat/messages');
      final loaded = data
          .whereType<Map>()
          .map((item) => Map<String, dynamic>.from(item))
          .toList();

      if (!mounted) return;
      setState(() {
        _messages
          ..clear()
          ..addAll(loaded);
        _loading = false;
        _error = null;
      });
      _scrollToBottom();
      await _markReadThroughLastMessage();
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _loading = false;
        _error = e.toString();
      });
    }
  }

  Future<void> _refreshNewMessages() async {
    if (_loading || _refreshing || _messages.isEmpty && _error != null) return;

    _refreshing = true;
    try {
      final lastId = _messages.isEmpty ? null : _messages.last['id']?.toString();
      final path = lastId == null || lastId.isEmpty
          ? '/chat/messages'
          : '/chat/messages?afterId=$lastId';

      final wasNearBottom = _isNearBottom;
      final data = await widget.api.getList(path);
      final existingIds = _messages.map((m) => m['id']?.toString()).toSet();
      final incoming = data
          .whereType<Map>()
          .map((item) => Map<String, dynamic>.from(item))
          .where((item) => !existingIds.contains(item['id']?.toString()))
          .toList();

      if (!mounted || incoming.isEmpty) return;

      setState(() {
        _messages.addAll(incoming);
        _error = null;
      });

      if (wasNearBottom) {
        _scrollToBottom();
        await _markReadThroughLastMessage();
      }
    } catch (_) {
      // Falha de sincronizacao em segundo plano nao apaga o historico nem
      // interrompe a digitacao. A proxima rodada tenta novamente.
    } finally {
      _refreshing = false;
    }
  }

  void _handleScroll() {
    if (_isNearBottom && _messages.isNotEmpty) {
      unawaited(_markReadThroughLastMessage());
    }
  }

  Future<void> _markReadThroughLastMessage() async {
    if (_markingRead || _messages.isEmpty) return;
    final lastId = _messages.last['id']?.toString();
    if (lastId == null || lastId.isEmpty) return;

    _markingRead = true;
    try {
      final result = await widget.api.putMap(
        '/chat/read',
        {'lastMessageId': lastId},
      );
      widget.onUnreadChanged?.call(_attentionCount(result));
    } catch (_) {
      // A leitura sera tentada de novo no proximo polling/scroll.
    } finally {
      _markingRead = false;
    }
  }

  int _attentionCount(Map<String, dynamic> payload) {
    final unread = (payload['unreadCount'] as num?)?.toInt() ?? 0;
    final pending = widget.session.isAdult
        ? (payload['pendingRequests'] as num?)?.toInt() ?? 0
        : 0;
    return unread + pending;
  }

  Future<void> _sendQuickRequest(
    String type, {
    int? minutes,
  }) async {
    if (_sending) return;
    setState(() => _sending = true);

    try {
      final sent = await widget.api.postMap(
        '/chat/requests',
        {
          'type': type,
          if (minutes != null) 'minutes': minutes,
        },
      );

      if (!mounted) return;
      setState(() {
        if (!_messages.any(
          (m) => m['id']?.toString() == sent['id']?.toString(),
        )) {
          _messages.add(sent);
        }
      });
      _scrollToBottom();
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Pedido enviado para o adulto.')),
      );
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Nao foi possivel enviar o pedido: $e')),
        );
      }
    } finally {
      if (mounted) setState(() => _sending = false);
    }
  }

  Future<void> _reviewRequest(
    Map<String, dynamic> message,
    bool approve,
  ) async {
    final id = message['id']?.toString();
    if (id == null || id.isEmpty) return;

    try {
      final updated = await widget.api.putMap(
        '/chat/requests/$id/' + (approve ? 'approve' : 'reject'),
        const {},
      );

      if (!mounted) return;
      setState(() {
        final index = _messages.indexWhere(
          (m) => m['id']?.toString() == updated['id']?.toString(),
        );
        if (index >= 0) {
          _messages[index] = updated;
        }
      });

      final summary = await widget.api.getMap('/chat/unread-count');
      widget.onUnreadChanged?.call(_attentionCount(summary));

      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(approve ? 'Pedido aprovado.' : 'Pedido recusado.'),
          ),
        );
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Nao foi possivel revisar o pedido: $e')),
        );
      }
    }
  }

  Future<void> _sendMessage() async {
    final text = _messageController.text.trim();
    if (text.isEmpty || _sending) return;

    setState(() => _sending = true);

    try {
      final sent = await widget.api.postMap(
        '/chat/messages',
        {'text': text},
      );

      if (!mounted) return;

      setState(() {
        if (!_messages.any((m) => m['id']?.toString() == sent['id']?.toString())) {
          _messages.add(sent);
        }
        _messageController.clear();
        _error = null;
      });

      _focusNode.requestFocus();
      _scrollToBottom();
    } catch (e) {
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Nao foi possivel enviar: $e')),
      );
    } finally {
      if (mounted) setState(() => _sending = false);
    }
  }

  bool get _isNearBottom {
    if (!_scrollController.hasClients) return true;
    return _scrollController.position.maxScrollExtent -
            _scrollController.position.pixels <
        140;
  }

  void _scrollToBottom() {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted || !_scrollController.hasClients) return;
      _scrollController.animateTo(
        _scrollController.position.maxScrollExtent,
        duration: const Duration(milliseconds: 220),
        curve: Curves.easeOut,
      );
    });
  }

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;

    return Scaffold(
      appBar: AppBar(
        title: const Text('Chat da familia'),
        actions: [
          IconButton(
            tooltip: 'Atualizar',
            onPressed: _refreshNewMessages,
            icon: const Icon(Icons.refresh),
          ),
        ],
      ),
      body: SafeArea(
        child: Column(
          children: [
            if (_loading)
              const Expanded(
                child: Center(child: CircularProgressIndicator()),
              )
            else if (_error != null && _messages.isEmpty)
              Expanded(
                child: Center(
                  child: Padding(
                    padding: const EdgeInsets.all(24),
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Icon(
                          Icons.cloud_off_outlined,
                          size: 44,
                          color: scheme.error,
                        ),
                        const SizedBox(height: 12),
                        Text(
                          _error!,
                          textAlign: TextAlign.center,
                          style: TextStyle(color: scheme.error),
                        ),
                        const SizedBox(height: 14),
                        FilledButton.icon(
                          onPressed: _loadInitial,
                          icon: const Icon(Icons.refresh),
                          label: const Text('Tentar novamente'),
                        ),
                      ],
                    ),
                  ),
                ),
              )
            else
              Expanded(
                child: _messages.isEmpty
                    ? const Center(
                        child: Padding(
                          padding: EdgeInsets.all(24),
                          child: Text(
                            'Nenhuma mensagem ainda.\nEnvie a primeira mensagem.',
                            textAlign: TextAlign.center,
                          ),
                        ),
                      )
                    : ListView.builder(
                        controller: _scrollController,
                        padding: const EdgeInsets.fromLTRB(14, 18, 14, 12),
                        itemCount: _messages.length,
                        itemBuilder: (context, index) {
                          final message = _messages[index];
                          final mine = message['senderId']?.toString() ==
                              widget.session.userId;
                          if (message['kind']?.toString() == 'request') {
                            return _RequestBubble(
                              message: message,
                              mine: mine,
                              canReview: widget.session.isAdult &&
                                  message['requestStatus']?.toString() == 'pending',
                              onApprove: () => _reviewRequest(message, true),
                              onReject: () => _reviewRequest(message, false),
                            );
                          }
                          return _MessageBubble(
                            message: message,
                            mine: mine,
                          );
                        },
                      ),
              ),
            if (!widget.session.isAdult)
              Container(
                width: double.infinity,
                padding: const EdgeInsets.fromLTRB(12, 8, 12, 4),
                child: Wrap(
                  spacing: 8,
                  runSpacing: 8,
                  children: [
                    ActionChip(
                      avatar: const Icon(Icons.help_outline, size: 18),
                      label: const Text('Preciso de ajuda'),
                      onPressed: _sending
                          ? null
                          : () => _sendQuickRequest('help'),
                    ),
                    ActionChip(
                      avatar: const Icon(Icons.swap_horiz, size: 18),
                      label: const Text('Mudar tarefa'),
                      onPressed: _sending
                          ? null
                          : () => _sendQuickRequest('change_task'),
                    ),
                    ActionChip(
                      avatar: const Icon(Icons.timer_outlined, size: 18),
                      label: const Text('+10 min'),
                      onPressed: _sending
                          ? null
                          : () => _sendQuickRequest(
                                'extra_time',
                                minutes: 10,
                              ),
                    ),
                    ActionChip(
                      avatar: const Icon(Icons.timer_outlined, size: 18),
                      label: const Text('+20 min'),
                      onPressed: _sending
                          ? null
                          : () => _sendQuickRequest(
                                'extra_time',
                                minutes: 20,
                              ),
                    ),
                  ],
                ),
              ),
            Container(
              decoration: BoxDecoration(
                color: scheme.surface,
                border: Border(
                  top: BorderSide(color: scheme.outlineVariant),
                ),
              ),
              padding: const EdgeInsets.fromLTRB(12, 10, 12, 12),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.end,
                children: [
                  Expanded(
                    child: TextField(
                      controller: _messageController,
                      focusNode: _focusNode,
                      minLines: 1,
                      maxLines: 4,
                      maxLength: 2000,
                      textCapitalization: TextCapitalization.sentences,
                      decoration: const InputDecoration(
                        hintText: 'Escreva uma mensagem...',
                        counterText: '',
                      ),
                    ),
                  ),
                  const SizedBox(width: 8),
                  IconButton.filled(
                    tooltip: 'Enviar',
                    onPressed: _sending ? null : _sendMessage,
                    icon: _sending
                        ? const SizedBox.square(
                            dimension: 20,
                            child: CircularProgressIndicator(strokeWidth: 2),
                          )
                        : const Icon(Icons.send_rounded),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _MessageBubble extends StatelessWidget {
  const _MessageBubble({
    required this.message,
    required this.mine,
  });

  final Map<String, dynamic> message;
  final bool mine;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final senderName = message['senderName']?.toString().trim();
    final text = message['text']?.toString() ?? '';
    final timestamp = formatChatTimestamp(message['createdAt']?.toString());

    return Align(
      alignment: mine ? Alignment.centerRight : Alignment.centerLeft,
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 560),
        child: Container(
          margin: const EdgeInsets.only(bottom: 9),
          padding: const EdgeInsets.fromLTRB(14, 10, 14, 9),
          decoration: BoxDecoration(
            color: mine
                ? scheme.primaryContainer
                : scheme.surfaceContainerHighest,
            borderRadius: BorderRadius.only(
              topLeft: const Radius.circular(18),
              topRight: const Radius.circular(18),
              bottomLeft: Radius.circular(mine ? 18 : 5),
              bottomRight: Radius.circular(mine ? 5 : 18),
            ),
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              if (!mine && senderName != null && senderName.isNotEmpty) ...[
                Text(
                  senderName,
                  style: TextStyle(
                    fontSize: 12,
                    fontWeight: FontWeight.w800,
                    color: scheme.primary,
                  ),
                ),
                const SizedBox(height: 2),
              ],
              SelectableText(
                text,
                style: const TextStyle(fontSize: 16, height: 1.3),
              ),
              const SizedBox(height: 4),
              Text(
                timestamp,
                style: TextStyle(
                  fontSize: 11,
                  color: scheme.onSurfaceVariant,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

}


class _RequestBubble extends StatelessWidget {
  const _RequestBubble({
    required this.message,
    required this.mine,
    required this.canReview,
    required this.onApprove,
    required this.onReject,
  });

  final Map<String, dynamic> message;
  final bool mine;
  final bool canReview;
  final VoidCallback onApprove;
  final VoidCallback onReject;

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final type = message['requestType']?.toString();
    final status = message['requestStatus']?.toString();
    final minutes = (message['requestedMinutes'] as num?)?.toInt();
    final sender = message['senderName']?.toString() ?? 'Membro';

    final statusIcon = switch (status) {
      'approved' => Icons.check_circle_outline,
      'rejected' => Icons.cancel_outlined,
      _ => Icons.schedule_outlined,
    };

    return Align(
      alignment: mine ? Alignment.centerRight : Alignment.centerLeft,
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 560),
        child: Card(
          margin: const EdgeInsets.only(bottom: 10),
          child: Padding(
            padding: const EdgeInsets.all(14),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Icon(statusIcon, color: scheme.primary),
                    const SizedBox(width: 8),
                    Expanded(
                      child: Text(
                        chatRequestTitle(type, minutes: minutes),
                        style: const TextStyle(fontWeight: FontWeight.w800),
                      ),
                    ),
                    Chip(
                      visualDensity: VisualDensity.compact,
                      label: Text(chatRequestStatusLabel(status)),
                    ),
                  ],
                ),
                const SizedBox(height: 8),
                Text(
                  sender + ': ' + (message['text']?.toString() ?? ''),
                  style: TextStyle(color: scheme.onSurfaceVariant),
                ),
                if (canReview) ...[
                  const SizedBox(height: 12),
                  Row(
                    children: [
                      Expanded(
                        child: OutlinedButton(
                          onPressed: onReject,
                          child: const Text('Recusar'),
                        ),
                      ),
                      const SizedBox(width: 8),
                      Expanded(
                        child: FilledButton(
                          onPressed: onApprove,
                          child: const Text('Aprovar'),
                        ),
                      ),
                    ],
                  ),
                ],
              ],
            ),
          ),
        ),
      ),
    );
  }
}
