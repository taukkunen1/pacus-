class DailyTask {
  const DailyTask({required this.id, required this.title, required this.period, required this.type, required this.status, required this.points, this.order = 0, this.description, this.minimumGoalLabel, this.deletedAt, this.options = const [], this.selectedOption, this.reason, this.plannedBy, this.createdByMember = false, this.planCue, this.requiresAdultApproval = false, this.taskTemplateId});
  final String id, title, period, type, status;
  final int points, order;
  final String? description, minimumGoalLabel, selectedOption, reason, plannedBy, planCue, taskTemplateId;
  final bool createdByMember, requiresAdultApproval;
  final List<String> options;
  final DateTime? deletedAt;
  bool get isDone => status.toLowerCase() == 'done';
  bool get isDeleted => deletedAt != null;
  factory DailyTask.fromJson(Map<String, dynamic> json) => DailyTask(
    id: json['id']?.toString() ?? '', title: json['title']?.toString() ?? 'Tarefa',
    period: json['period']?.toString() ?? 'morning', type: json['type']?.toString() ?? 'expected',
    status: json['status']?.toString() ?? 'pending', points: (json['points'] as num?)?.toInt() ?? 0,
    order: (json['order'] as num?)?.toInt() ?? 0,
    description: json['description']?.toString(), minimumGoalLabel: json['minimumGoalLabel']?.toString(),
    options: ((json['options'] as List?) ?? const []).map((e) => e.toString()).toList(),
    selectedOption: json['selectedOption']?.toString(), reason: json['reason']?.toString(),
    plannedBy: json['plannedBy']?.toString(), createdByMember: json['createdByMember'] == true,
    planCue: json['planCue']?.toString(), requiresAdultApproval: json['requiresAdultApproval'] == true,
    taskTemplateId: json['taskTemplateId']?.toString(),
    deletedAt: json['deletedAt'] == null ? null : DateTime.tryParse(json['deletedAt'].toString()),
  );
}

class DailyRoutine {
  const DailyRoutine({required this.id, required this.familyId, required this.date, required this.gameTimerEnabled, required this.gameTimerMinutes, required this.gameTimerExtraMinutes, required this.tasks, this.reaction, this.eveningPlan = const [], this.tomorrowPlanConfirmedAt});
  final String id, familyId, date;
  final bool gameTimerEnabled;
  final int gameTimerMinutes, gameTimerExtraMinutes;
  final List<DailyTask> tasks;
  final Map<String, dynamic>? reaction;
  final List<Map<String, dynamic>> eveningPlan;
  final DateTime? tomorrowPlanConfirmedAt;
  int get availableGameMinutes => (gameTimerMinutes + gameTimerExtraMinutes).clamp(0, 1000000);
  int get doneTasks => tasks.where((t) => !t.isDeleted && t.isDone).length;
  int get totalTasks => tasks.where((t) => !t.isDeleted).length;
  factory DailyRoutine.fromJson(Map<String, dynamic> json) => DailyRoutine(
    id: json['id']?.toString() ?? '', familyId: json['familyId']?.toString() ?? '', date: json['date']?.toString() ?? '',
    gameTimerEnabled: json['gameTimerEnabled'] == true, gameTimerMinutes: (json['gameTimerMinutes'] as num?)?.toInt() ?? 120,
    gameTimerExtraMinutes: (json['gameTimerExtraMinutes'] as num?)?.toInt() ?? 0,
    tasks: ((json['tasks'] as List?) ?? const []).whereType<Map<String, dynamic>>().map(DailyTask.fromJson).toList(),
    reaction: json['reaction'] is Map ? Map<String, dynamic>.from(json['reaction'] as Map) : null,
    eveningPlan: ((json['eveningPlan'] as List?) ?? const []).whereType<Map>().map((e) => Map<String, dynamic>.from(e)).toList(),
    tomorrowPlanConfirmedAt: json['tomorrowPlanConfirmedAt'] == null ? null : DateTime.tryParse(json['tomorrowPlanConfirmedAt'].toString()),
  );
}

class AuthSession {
  const AuthSession({required this.token, required this.userId, required this.role, required this.name});
  final String token, userId, role, name;
  bool get isAdult => role.toLowerCase() == 'adult';
  factory AuthSession.fromJson(Map<String, dynamic> json) => AuthSession(
    token: json['token']?.toString() ?? '', userId: json['userId']?.toString() ?? '',
    role: json['role']?.toString() ?? '', name: json['name']?.toString() ?? '',
  );
}
