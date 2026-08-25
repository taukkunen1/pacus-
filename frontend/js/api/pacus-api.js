import { apiClient } from "./api-client.js";

export function getTodayRoutine() {
  return apiClient("/daily-routines/today");
}

export function completeTask(taskId) {
  return apiClient(`/daily-tasks/${taskId}/complete`, { method: "POST" });
}

export function reopenTask(taskId) {
  return apiClient(`/daily-tasks/${taskId}/reopen`, { method: "POST" });
}

export function createTask({ title, description, type, period, points }) {
  return apiClient("/daily-tasks", {
    method: "POST",
    body: JSON.stringify({ title, description, type, period, points }),
  });
}

export function getPointsBalance() {
  return apiClient("/points");
}
