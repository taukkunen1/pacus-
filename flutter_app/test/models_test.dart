import 'package:flutter_test/flutter_test.dart';
import 'package:pacus_flutter/models.dart';

void main() {
  test('DailyTask parses autonomy fields and completion state', () {
    final task = DailyTask.fromJson({
      'id': 't1',
      'title': 'Organizar mochila',
      'period': 'evening',
      'type': 'challenge',
      'status': 'done',
      'points': 4,
      'order': 3,
      'options': ['Agora', 'Depois do banho'],
      'selectedOption': 'Agora',
      'reason': 'Preparar o dia seguinte',
      'plannedBy': 'child',
      'createdByMember': true,
      'requiresAdultApproval': true,
    });

    expect(task.isDone, isTrue);
    expect(task.options, ['Agora', 'Depois do banho']);
    expect(task.createdByMember, isTrue);
    expect(task.requiresAdultApproval, isTrue);
    expect(task.reason, 'Preparar o dia seguinte');
  });

  test('DailyTask recognizes deleted task', () {
    final task = DailyTask.fromJson({
      'id': 't2',
      'title': 'Antiga',
      'status': 'pending',
      'points': 1,
      'deletedAt': '2026-09-23T12:00:00Z',
    });

    expect(task.isDeleted, isTrue);
  });

  test('DailyRoutine counters ignore deleted tasks', () {
    final routine = DailyRoutine.fromJson({
      'id': 'r1',
      'familyId': 'f1',
      'date': '2026-09-23',
      'gameTimerEnabled': true,
      'gameTimerMinutes': 60,
      'gameTimerExtraMinutes': 15,
      'tasks': [
        {'id': '1', 'title': 'A', 'status': 'done', 'points': 1},
        {'id': '2', 'title': 'B', 'status': 'pending', 'points': 1},
        {
          'id': '3',
          'title': 'C',
          'status': 'done',
          'points': 1,
          'deletedAt': '2026-09-23T10:00:00Z',
        },
      ],
    });

    expect(routine.totalTasks, 2);
    expect(routine.doneTasks, 1);
    expect(routine.availableGameMinutes, 75);
  });

  test('DailyRoutine clamps negative available game time to zero', () {
    final routine = DailyRoutine.fromJson({
      'id': 'r2',
      'familyId': 'f1',
      'date': '2026-09-23',
      'gameTimerEnabled': true,
      'gameTimerMinutes': 10,
      'gameTimerExtraMinutes': -30,
      'tasks': [],
    });

    expect(routine.availableGameMinutes, 0);
  });

  test('AuthSession adult role is case insensitive', () {
    final session = AuthSession.fromJson({
      'token': 'token',
      'userId': 'u1',
      'role': 'ADULT',
      'name': 'Pedro',
    });

    expect(session.isAdult, isTrue);
  });
}
