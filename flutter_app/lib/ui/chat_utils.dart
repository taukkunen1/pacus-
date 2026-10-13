String formatChatTimestamp(String? raw, {DateTime? now}) {
  final parsed = raw == null ? null : DateTime.tryParse(raw);
  if (parsed == null) return '';

  final local = parsed.toLocal();
  final reference = (now ?? DateTime.now()).toLocal();
  final sameDay = local.year == reference.year &&
      local.month == reference.month &&
      local.day == reference.day;

  final hour = local.hour.toString().padLeft(2, '0');
  final minute = local.minute.toString().padLeft(2, '0');

  if (sameDay) return '$hour:$minute';

  final day = local.day.toString().padLeft(2, '0');
  final month = local.month.toString().padLeft(2, '0');
  return '$day/$month $hour:$minute';
}


String chatRequestTitle(String? type, {int? minutes}) {
  switch (type) {
    case 'help':
      return 'Preciso de ajuda';
    case 'change_task':
      return 'Quero mudar uma tarefa';
    case 'extra_time':
      return minutes == null
          ? 'Pedido de tempo extra'
          : '+$minutes min de tempo de tela';
    default:
      return 'Pedido';
  }
}

bool chatRequestNeedsSupport(String? type) =>
    type == 'help' || type == 'change_task';

String chatRequestApproveLabel(String? type) {
  switch (type) {
    case 'help':
      return 'Vou ajudar';
    case 'change_task':
      return 'Vamos conversar';
    default:
      return 'Aprovar';
  }
}

String chatRequestStatusLabel(String? status, {String? type}) {
  if (chatRequestNeedsSupport(type)) {
    switch (status) {
      case 'approved':
        return 'Recebido';
      case 'rejected':
        return 'Nao atendido';
      case 'processing':
        return 'Processando';
      default:
        return 'Aguardando resposta';
    }
  }

  switch (status) {
    case 'approved':
      return 'Aprovado';
    case 'rejected':
      return 'Recusado';
    case 'processing':
      return 'Processando';
    default:
      return 'Aguardando';
  }
}
