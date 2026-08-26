// Cliente HTTP base. Toda chamada a API passa por aqui — nunca acessa MongoDB diretamente.
// Ajuste API_BASE_URL para onde a Pacus.Api estiver rodando (local ou publicada).
export const API_BASE_URL = window.PACUS_API_BASE_URL || "http://localhost:5000/api/v1";

const TOKEN_KEY = "pacus.auth.token";

export function getToken() {
  return localStorage.getItem(TOKEN_KEY);
}

export function setToken(token) {
  localStorage.setItem(TOKEN_KEY, token);
}

export function clearToken() {
  localStorage.removeItem(TOKEN_KEY);
}

export async function apiClient(path, options = {}) {
  const token = getToken();
  const headers = {
    "Content-Type": "application/json",
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
    ...(options.headers || {}),
  };

  const response = await fetch(`${API_BASE_URL}${path}`, { ...options, headers });

  if (response.status === 401) {
    clearToken();
    window.dispatchEvent(new CustomEvent("pacus:unauthorized"));
    const error = new Error("Sessao expirada. Faca login novamente.");
    error.status = 401;
    throw error;
  }

  if (!response.ok) {
    let message = `Erro na API (${response.status})`;
    try {
      const body = await response.json();
      if (body?.error) message = body.error;
    } catch {
      // resposta sem corpo JSON — mantem a mensagem generica
    }
    const error = new Error(message);
    error.status = response.status;
    throw error;
  }

  if (response.status === 204) return null;
  return response.json();
}
