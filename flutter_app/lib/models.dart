class DailyTask {
  const DailyTask({required this.id, required this.title, required this.period, required this.type, required this.status, required this.points, this.description, this.minimumGoalLabel, this.deletedAt});
  final String id, title, period, type, status;
  final int points;
  final String? description, minimumGoalLabel;
  final DateTime? deletedAt;
  bool get isDone => status.toLowerCase() == 'done';
  bool get isDeleted => deletedAt != null;
  factory DailyTask.fromJson(Map<String, dynamic> json) => DailyTask(
    id: json['id']?.toString() ?? '', title: json['title']?.toString() ?? 'Tarefa',
    period: json['period']?.toString() ?? 'morning', type: json['type']?.toString() ?? 'expected',
    status: json['status']?.toString() ?? 'pending', points: (json['points'] as num?)?.toInt() ?? 0,
    description: json['description']?.toString(), minimumGoalLabel: json['minimumGoalLabel']?.toString(),
    deletedAt: json['deletedAt'] == null ? null : DateTime.tryParse(json['deletedAt'].toString()),
  );
}

class DailyRoutine {
  const DailyRoutine({required this.id, required this.familyId, required this.date, required this.gameTimerEnabled, required this.gameTimerMinutes, required this.gameTimerExtraMinutes, required this.tasks});
  final String id, familyId, date;
  final bool gameTimerEnabled;
  final int gameTimerMinutes, gameTimerExtraMinutes;
  final List<DailyTask> tasks;
  int get availableGameMinutes => (gameTimerMinutes + gameTimerExtraMinutes).clamp(0, 1000000);
  int get doneTasks => tasks.where((t) => !t.isDeleted && t.isDone).length;
  int get totalTasks => tasks.where((t) => !t.isDeleted).length;
  factory DailyRoutine.fromJson(Map<String, dynamic> json) => DailyRoutine(
    id: json['id']?.toString() ?? '', familyId: json['familyId']?.toString() ?? '', date: json['date']?.toString() ?? '',
    gameTimerEnabled: json['gameTimerEnabled'] == true, gameTimerMinutes: (json['gameTimerMinutes'] as num?)?.toInt() ?? 120,
    gameTimerExtraMinutes: (json['gameTimerExtraMinutes'] as num?)?.toInt() ?? 0,
    tasks: ((json['tasks'] as List?) ?? const []).whereType<Map<String, dynamic>>().map(DailyTask.fromJson).toList(),
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
