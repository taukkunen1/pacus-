import { apiClient, setToken } from "./api-client.js";

export async function loginAdult(email, password) {
  const result = await apiClient("/auth/adult/login", {
    method: "POST",
    body: JSON.stringify({ email, password }),
  });
  setToken(result.token);
  return result;
}

export async function loginChild(userId, pin) {
  const result = await apiClient("/auth/child/login", {
    method: "POST",
    body: JSON.stringify({ userId, pin }),
  });
  setToken(result.token);
  return result;
}
