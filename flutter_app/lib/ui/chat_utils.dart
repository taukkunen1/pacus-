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

String chatRequestStatusLabel(String? status) {
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
