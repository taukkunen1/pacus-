import 'package:flutter/material.dart';

import '../models.dart';

const _days = <String, String>{
  'Monday': 'Seg',
  'Tuesday': 'Ter',
  'Wednesday': 'Qua',
  'Thursday': 'Qui',
  'Friday': 'Sex',
  'Saturday': 'Sáb',
  'Sunday': 'Dom',
};

Future<Map<String, dynamic>?> showQuickTaskDialog(
  BuildContext context, {
  DailyTask? task,
  required bool allowPermanent,
  String initialPeriod = 'morning',
}) async {
  final title = TextEditingController(text: task?.title ?? '');
  final description = TextEditingController(text: task?.description ?? '');
  final points = TextEditingController(text: (task?.points ?? 1).toString());
  final options = TextEditingController(text: task?.options.join('\n') ?? '');
  final reason = TextEditingController(text: task?.reason ?? '');

  var type = task?.type.toLowerCase() ?? 'challenge';
  var period = task?.period.toLowerCase() ?? initialPeriod;
  var permanent = false;
  var recurrence = 'daily';
  final customDays = <String>{};
  String? validationError;

  final result = await showDialog<Map<String, dynamic>>(
    context: context,
    builder: (context) => StatefulBuilder(
      builder: (context, setDialog) => AlertDialog(
        title: Text(task == null ? 'Nova tarefa' : 'Editar tarefa'),
        content: SizedBox(
          width: 520,
          child: SingleChildScrollView(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                TextField(
                  controller: title,
                  autofocus: task == null,
                  decoration: const InputDecoration(labelText: 'Nome'),
                ),
                const SizedBox(height: 10),
                TextField(
                  controller: description,
                  minLines: 2,
                  maxLines: 4,
                  decoration: const InputDecoration(
                    labelText: 'Descrição (opcional)',
                    hintText: 'Como fazer ou detalhes da tarefa',
                  ),
                ),
                const SizedBox(height: 10),
                TextField(
                  controller: points,
                  keyboardType: const TextInputType.numberWithOptions(signed: true),
                  decoration: const InputDecoration(
                    labelText: 'Pontos',
                    helperText: 'Use de 1 a 10; penalidades, quando aplicáveis, de -1 a -10.',
                  ),
                ),
                const SizedBox(height: 10),
                DropdownButtonFormField<String>(
                  initialValue: type,
                  decoration: const InputDecoration(labelText: 'Tipo'),
                  items: const [
                    DropdownMenuItem(value: 'mandatory', child: Text('Obrigatória')),
                    DropdownMenuItem(value: 'expected', child: Text('Deve fazer')),
                    DropdownMenuItem(value: 'challenge', child: Text('Desafio')),
                  ],
                  onChanged: (value) => setDialog(() => type = value ?? type),
                ),
                const SizedBox(height: 10),
                DropdownButtonFormField<String>(
                  initialValue: period,
                  decoration: const InputDecoration(labelText: 'Período'),
                  items: const [
                    DropdownMenuItem(value: 'morning', child: Text('Manhã')),
                    DropdownMenuItem(value: 'afternoon', child: Text('Tarde')),
                    DropdownMenuItem(value: 'evening', child: Text('Noite')),
                  ],
                  onChanged: (value) => setDialog(() => period = value ?? period),
                ),
                const SizedBox(height: 10),
                TextField(
                  controller: options,
                  minLines: 2,
                  maxLines: 5,
                  decoration: const InputDecoration(
                    labelText: 'Opções para escolher (opcional)',
                    hintText: 'Uma opção por linha, de 2 a 4 opções',
                  ),
                ),
                const SizedBox(height: 10),
                TextField(
                  controller: reason,
                  decoration: const InputDecoration(
                    labelText: 'Por que isso importa (opcional)',
                  ),
                ),
                if (allowPermanent) ...[
                  const SizedBox(height: 8),
                  SwitchListTile(
                    contentPadding: EdgeInsets.zero,
                    title: const Text('Repetir nos próximos dias'),
                    subtitle: const Text('Transforma em tarefa permanente da família.'),
                    value: permanent,
                    onChanged: (value) => setDialog(() => permanent = value),
                  ),
                  if (permanent) ...[
                    const SizedBox(height: 6),
                    DropdownButtonFormField<String>(
                      initialValue: recurrence,
                      decoration: const InputDecoration(labelText: 'Repetição'),
                      items: const [
                        DropdownMenuItem(value: 'daily', child: Text('Todos os dias')),
                        DropdownMenuItem(value: 'interval', child: Text('Dia sim, dia não')),
                        DropdownMenuItem(value: 'custom', child: Text('Dias específicos')),
                      ],
                      onChanged: (value) => setDialog(() => recurrence = value ?? recurrence),
                    ),
                    if (recurrence == 'custom') ...[
                      const SizedBox(height: 10),
                      Wrap(
                        spacing: 6,
                        runSpacing: 6,
                        children: _days.entries
                            .map(
                              (entry) => FilterChip(
                                label: Text(entry.value),
                                selected: customDays.contains(entry.key),
                                onSelected: (selected) => setDialog(() {
                                  if (selected) {
                                    customDays.add(entry.key);
                                  } else {
                                    customDays.remove(entry.key);
                                  }
                                }),
                              ),
                            )
                            .toList(),
                      ),
                    ],
                  ],
                ],
                if (validationError != null) ...[
                  const SizedBox(height: 12),
                  Align(
                    alignment: Alignment.centerLeft,
                    child: Text(
                      validationError!,
                      style: TextStyle(color: Theme.of(context).colorScheme.error),
                    ),
                  ),
                ],
              ],
            ),
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context),
            child: const Text('Cancelar'),
          ),
          FilledButton(
            onPressed: () {
              final normalizedTitle = title.text.trim();
              final parsedPoints = int.tryParse(points.text.trim());
              final parsedOptions = options.text
                  .split(RegExp(r'[\r\n]+'))
                  .map((e) => e.trim())
                  .where((e) => e.isNotEmpty)
                  .toList();

              if (normalizedTitle.isEmpty) {
                setDialog(() => validationError = 'O nome da tarefa é obrigatório.');
                return;
              }
              if (parsedPoints == null || parsedPoints == 0 || parsedPoints.abs() > 10) {
                setDialog(() => validationError = 'Use pontos entre 1 e 10 ou entre -1 e -10.');
                return;
              }
              if (parsedOptions.length == 1 || parsedOptions.length > 4) {
                setDialog(() => validationError = 'Preencha de 2 a 4 opções, ou deixe o campo vazio.');
                return;
              }
              if (permanent && recurrence == 'custom' && customDays.isEmpty) {
                setDialog(() => validationError = 'Escolha pelo menos um dia da semana.');
                return;
              }

              final now = DateTime.now();
              final dateKey =
                  '${now.year.toString().padLeft(4, '0')}-${now.month.toString().padLeft(2, '0')}-${now.day.toString().padLeft(2, '0')}';

              Navigator.pop(context, {
                'title': normalizedTitle,
                'description': description.text.trim().isEmpty ? null : description.text.trim(),
                'type': type,
                'period': period,
                'points': parsedPoints,
                'options': parsedOptions,
                'reason': reason.text.trim().isEmpty ? null : reason.text.trim(),
                'permanent': permanent,
                if (permanent) 'recurrence': recurrence,
                if (permanent && recurrence == 'custom') 'customDays': customDays.toList(),
                if (permanent && recurrence == 'interval') 'anchorDate': dateKey,
                if (permanent && recurrence == 'interval') 'intervalDays': 2,
              });
            },
            child: Text(task == null ? 'Adicionar' : 'Salvar'),
          ),
        ],
      ),
    ),
  );

  title.dispose();
  description.dispose();
  points.dispose();
  options.dispose();
  reason.dispose();
  return result;
}
