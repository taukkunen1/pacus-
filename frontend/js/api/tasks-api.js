import { apiClient } from "./api-client.js";

// Tarefas da rotina de hoje
export const createDailyTask = (payload) =>
  apiClient("/daily-tasks", {
    method: "POST",
    body: JSON.stringify(payload),
  });

export const updateDailyTask = (id, payload) =>
  apiClient(`/daily-tasks/${id}`, {
    method: "PUT",
    body: JSON.stringify(payload),
  });

export const deleteDailyTask = (id) =>
  apiClient(`/daily-tasks/${id}`, {
    method: "DELETE",
  });

export const reorderDailyTasks = (orderedTaskIds) =>
  apiClient("/daily-routines/today/order", {
    method: "PUT",
    body: JSON.stringify(orderedTaskIds),
  });

// Tarefas permanentes
export const getTasks = () =>
  apiClient("/tasks");

export const createTask = (payload) =>
  apiClient("/tasks", {
    method: "POST",
    body: JSON.stringify(payload),
  });

export const updateTask = (id, payload) =>
  apiClient(`/tasks/${id}`, {
    method: "PUT",
    body: JSON.stringify(payload),
  });

export const deleteTask = (id) =>
  apiClient(`/tasks/${id}`, {
    method: "DELETE",
  });

export const activateTask = (id) =>
  apiClient(`/tasks/${id}/activate`, {
    method: "PUT",
  });