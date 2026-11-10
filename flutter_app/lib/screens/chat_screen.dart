import 'dart:async';
import 'dart:convert';
import 'dart:typed_data';

import 'package:file_selector/file_selector.dart';
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
  bool _pickingImage = false;
  Uint8List? _attachedImage;
  static const _maxImageBytes = 1024 * 1024;
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
    if (_loading || _refreshing || (_messages.isEmpty && _error != null)) {
      return;
    }

    _refreshing = true;
    try {
      final wasNearBottom = _isNearBottom;
      final data = await widget.api.getList('/chat/messages');
      final refreshed = data
          .whereType<Map>()
          .map((item) => Map<String, dynamic>.from(item))
          .toList();

      if (!mounted) return;

      final changed = refreshed.length != _messages.length ||
          refreshed.asMap().entries.any((entry) {
            if (entry.key >= _messages.length) return true;
            final current = _messages[entry.key];
            final next = entry.value;
            return current['id']?.toString() != next['id']?.toString() ||
                current['requestStatus']?.toString() !=
                    next['requestStatus']?.toString();
          });

      if (!changed) return;

      setState(() {
        _messages
          ..clear()
          ..addAll(refreshed);
        _error = null;
      });

      if (wasNearBottom) {
        _scrollToBottom();
        await _markReadThroughLastMessage();
      }
    } catch (_) {
      // A proxima rodada tenta de novo sem interromper a conversa.
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
    String? note,
  }) async {
    if (_sending) return;
    setState(() => _sending = true);

    try {
      final body = <String, dynamic>{'type': type};
      if (minutes != null) {
        body['minutes'] = minutes;
      }
      if (note != null && note.trim().isNotEmpty) {
        body['note'] = note.trim();
      }

      final sent = await widget.api.postMap(
        '/chat/requests',
        body,
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

  Future<void> _openQuickRequestComposer(String type) async {
    final controller = TextEditingController();
    int minutes = 10;

    final submitted = await showModalBottomSheet<bool>(
      context: context,
      isScrollControlled: true,
      showDragHandle: true,
      builder: (context) {
        return StatefulBuilder(
          builder: (context, setModalState) {
            final isExtraTime = type == 'extra_time';
            final title = switch (type) {
              'help' => 'Pedir ajuda',
              'change_task' => 'Conversar sobre uma tarefa',
              _ => 'Pedir tempo extra',
            };

            return Padding(
              padding: EdgeInsets.fromLTRB(
                20,
                4,
                20,
                20 + MediaQuery.viewInsetsOf(context).bottom,
              ),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Text(title, style: Theme.of(context).textTheme.titleLarge),
                  const SizedBox(height: 8),
                  Text(
                    isExtraTime
                        ? 'Escolha o tempo e conte ao adulto por que ele seria útil.'
                        : 'Você pode enviar só o pedido ou explicar um pouco melhor.',
                  ),
                  if (isExtraTime) ...[
                    const SizedBox(height: 14),
                    Wrap(
                      spacing: 8,
                      children: [10, 20, 30, 45, 60]
                          .map(
                            (value) => ChoiceChip(
                              label: Text('+$value min'),
                              selected: minutes == value,
                              onSelected: (_) =>
                                  setModalState(() => minutes = value),
                            ),
                          )
                          .toList(),
                    ),
                  ],
                  const SizedBox(height: 14),
                  TextField(
                    controller: controller,
                    autofocus: !isExtraTime,
                    maxLength: 500,
                    minLines: 2,
                    maxLines: 4,
                    decoration: const InputDecoration(
                      labelText: 'Quer explicar? (opcional)',
                      hintText: 'Escreva aqui...',
                    ),
                  ),
                  const SizedBox(height: 10),
                  FilledButton.icon(
                    onPressed: () => Navigator.pop(context, true),
                    icon: const Icon(Icons.send_outlined),
                    label: const Text('Enviar pedido'),
                  ),
                ],
              ),
            );
          },
        );
      },
    );

    if (submitted != true || !mounted) {
      controller.dispose();
      return;
    }

    final note = controller.text;
    controller.dispose();
    await _sendQuickRequest(
      type,
      minutes: type == 'extra_time' ? minutes : null,
      note: note,
    );
  }

  void _useSuggestedReply(String text) {
    _messageController.text = text;
    _messageController.selection = TextSelection.collapsed(
      offset: _messageController.text.length,
    );
    _focusNode.requestFocus();
  }

  List<String> get _suggestedReplies {
    if (widget.session.isAdult) {
      return const [
        'Entendi. Me conta um pouco mais?',
        'Tudo bem, vamos resolver isso juntos.',
        'Quer conversar sobre isso agora?',
      ];
    }

    return const [
      'Pode me ajudar?',
      'Quero conversar sobre uma tarefa.',
      'Tudo bem, obrigado!',
    ];
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
            content: Text(
              chatRequestNeedsSupport(message['requestType']?.toString())
                  ? 'Pedido recebido. Converse pelo chat para oferecer apoio.'
                  : (approve ? 'Pedido aprovado.' : 'Pedido recusado.'),
            ),
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

  Future<void> _pickImage() async {
    if (_sending || _pickingImage) return;
    setState(() => _pickingImage = true);

    try {
      final file = await openFile(acceptedTypeGroups: const [
        XTypeGroup(
          label: 'Imagens PNG e JPEG',
          extensions: ['png', 'jpg', 'jpeg'],
          mimeTypes: ['image/png', 'image/jpeg'],
          uniformTypeIdentifiers: ['public.png', 'public.jpeg'],
        ),
      ]);
      if (file == null || !mounted) return;
      if (await file.length() > _maxImageBytes) {
        throw const FormatException('Escolha uma imagem de até 1 MB.');
      }
      final bytes = await file.readAsBytes();
      if (bytes.isEmpty || bytes.length > _maxImageBytes) {
        throw const FormatException('Escolha uma imagem PNG ou JPEG de até 1 MB.');
      }
      if (mounted) setState(() => _attachedImage = bytes);
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Não foi possível anexar a imagem. $e')),
        );
      }
    } finally {
      if (mounted) setState(() => _pickingImage = false);
    }
  }

  Future<void> _pickEmoji() async {
    final emoji = await showDialog<String>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Escolher emoji'),
        content: Wrap(
          children: [
            for (final emoji in const [
              '😀', '😊', '😂', '😍', '🥰', '😎', '😢', '😮',
              '❤️', '👍', '👏', '🙌', '🤗', '🎉', '⭐', '💙',
            ])
              TextButton(
                onPressed: () => Navigator.pop(context, emoji),
                child: Text(emoji, style: const TextStyle(fontSize: 24)),
              ),
          ],
        ),
      ),
    );
    if (emoji == null || !mounted) return;
    final value = _messageController.value;
    final selection = value.selection;
    final start = selection.isValid ? selection.start : value.text.length;
    final end = selection.isValid ? selection.end : value.text.length;
    final text = value.text.replaceRange(start, end, emoji);
    if (text.length > 2000) return;

    _messageController.value = TextEditingValue(
      text: text,
      selection: TextSelection.collapsed(offset: start + emoji.length),
    );
    _focusNode.requestFocus();
  }

  Future<Uint8List> _loadChatImage(String messageId) async {
    final data = await widget.api.getMap('/chat/messages/$messageId/image');
    return base64Decode(data['image'] as String);
  }

  Future<void> _sendMessage() async {
    final text = _messageController.text.trim();
    final image = _attachedImage;
    if ((text.isEmpty && image == null) || _sending || _pickingImage) return;

    setState(() => _sending = true);

    try {
      final sent = await widget.api.postMap(
        '/chat/messages',
        {'text': text, if (image != null) 'image': base64Encode(image)},
      );

      if (!mounted) return;

      setState(() {
        if (!_messages.any((m) => m['id']?.toString() == sent['id']?.toString())) {
          _messages.add(sent);
        }
        _messageController.clear();
        _attachedImage = null;
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
                          final messageId = message['id'].toString();
                          return _MessageBubble(
                            key: ValueKey(messageId),
                            message: message,
                            mine: mine,
                            image: message['hasImage'] == true
                                ? _ChatImage(
                                    messageId: messageId,
                                    loadImage: _loadChatImage,
                                  )
                                : null,
                          );
                        },
                      ),
              ),
            if (!widget.session.isAdult)
              Container(
                width: double.infinity,
                padding: const EdgeInsets.fromLTRB(12, 8, 12, 8),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'Precisa de apoio? Pedir ajuda não tira pontos.',
                      style: Theme.of(context).textTheme.bodySmall,
                    ),
                    const SizedBox(height: 8),
                    Wrap(
                      spacing: 8,
                      runSpacing: 8,
                      children: [
                        ActionChip(
                          avatar: const Icon(Icons.help_outline, size: 18),
                          label: const Text('Preciso de ajuda'),
                          onPressed: _sending
                              ? null
                              : () => _openQuickRequestComposer('help'),
                        ),
                        ActionChip(
                          avatar: const Icon(Icons.swap_horiz, size: 18),
                          label: const Text('Conversar sobre uma tarefa'),
                          onPressed: _sending
                              ? null
                              : () => _openQuickRequestComposer('change_task'),
                        ),
                        ActionChip(
                          avatar: const Icon(Icons.timer_outlined, size: 18),
                          label: const Text('Pedir tempo extra'),
                          onPressed: _sending
                              ? null
                              : () => _openQuickRequestComposer('extra_time'),
                        ),
                      ],
                    ),
                  ],
                ),
              ),
            SizedBox(
              height: 42,
              child: ListView.separated(
                padding: const EdgeInsets.symmetric(horizontal: 12),
                scrollDirection: Axis.horizontal,
                itemCount: _suggestedReplies.length,
                separatorBuilder: (_, _) => const SizedBox(width: 8),
                itemBuilder: (context, index) {
                  final reply = _suggestedReplies[index];
                  return ActionChip(
                    avatar: const Icon(Icons.auto_awesome_outlined, size: 17),
                    label: Text(reply),
                    onPressed: _sending ? null : () => _useSuggestedReply(reply),
                  );
                },
              ),
            ),
            if (_attachedImage case final image?)
              ListTile(
                dense: true,
                leading: Image.memory(
                  image,
                  width: 40,
                  height: 40,
                  fit: BoxFit.cover,
                  errorBuilder: (_, _, _) => const Icon(Icons.broken_image_outlined),
                ),
                title: const Text('Imagem anexada'),
                trailing: IconButton(
                  tooltip: 'Remover anexo',
                  onPressed: _sending ? null : () => setState(() => _attachedImage = null),
                  icon: const Icon(Icons.close),
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
                  IconButton(
                    tooltip: 'Anexar imagem',
                    onPressed: _sending || _pickingImage ? null : _pickImage,
                    icon: const Icon(Icons.image_outlined),
                  ),
                  IconButton(
                    tooltip: 'Escolher emoji',
                    onPressed: _sending ? null : _pickEmoji,
                    icon: const Icon(Icons.emoji_emotions_outlined),
                  ),
                  Expanded(
                    child: TextField(
                      controller: _messageController,
                      readOnly: _sending,
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
                    onPressed: _sending || _pickingImage ? null : _sendMessage,
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
    super.key,
    required this.message,
    required this.mine,
    this.image,
  });

  final Map<String, dynamic> message;
  final bool mine;
  final Widget? image;

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
              ?image,
              if (text.isNotEmpty) ...[
                if (image != null) const SizedBox(height: 6),
                SelectableText(
                  text,
                  style: const TextStyle(fontSize: 16, height: 1.3),
                ),
              ],
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


class _ChatImage extends StatefulWidget {
  const _ChatImage({required this.messageId, required this.loadImage});

  final String messageId;
  final Future<Uint8List> Function(String messageId) loadImage;

  @override
  State<_ChatImage> createState() => _ChatImageState();
}

class _ChatImageState extends State<_ChatImage> {
  late Future<Uint8List> _image = widget.loadImage(widget.messageId);

  @override
  Widget build(BuildContext context) => FutureBuilder<Uint8List>(
    future: _image,
    builder: (context, snapshot) {
      if (snapshot.hasError) {
        return TextButton.icon(
          onPressed: () => setState(() => _image = widget.loadImage(widget.messageId)),
          icon: const Icon(Icons.refresh),
          label: const Text('Recarregar imagem'),
        );
      }
      if (!snapshot.hasData) {
        return const SizedBox(
          height: 240,
          width: 320,
          child: Center(child: CircularProgressIndicator()),
        );
      }
      return ClipRRect(
        borderRadius: BorderRadius.circular(10),
        child: Image.memory(
          snapshot.data!,
          semanticLabel: 'Imagem da mensagem',
          width: 320,
          height: 240,
          fit: BoxFit.contain,
          errorBuilder: (_, _, _) => const Text('Não foi possível abrir a imagem.'),
        ),
      );
    },
  );
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
                      label: Text(chatRequestStatusLabel(status, type: type)),
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
                      if (!chatRequestNeedsSupport(type)) ...[
                        Expanded(
                          child: OutlinedButton(
                            onPressed: onReject,
                            child: const Text('Recusar'),
                          ),
                        ),
                        const SizedBox(width: 8),
                      ],
                      Expanded(
                        child: FilledButton(
                          onPressed: onApprove,
                          child: Text(chatRequestApproveLabel(type)),
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
