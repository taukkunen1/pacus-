import { getToken, clearToken, decodeToken } from "./api/api-client.js";
import { renderLogin } from "./screens/login.js";
import { renderHome } from "./screens/home.js";
import { renderHistory } from "./screens/history.js";
import { renderPoints } from "./screens/points.js";
import { renderPacus } from "./screens/pacus.js";
import { setUser } from "./state/app-state.js";

const ROLE_CLAIM = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";

const root = document.getElementById("app");

function navigate(screen = "today") {
  if (screen === "history") return renderHistory(root, navigate);
  if (screen === "points") return renderPoints(root, navigate);
  if (screen === "pacus") return renderPacus(root, navigate);
  return renderHome(root, navigate);
}

function goToLogin() {
  renderLogin(root, (authResult) => {
    setUser({ userId: authResult.userId, role: authResult.role, name: authResult.name });
    navigate("today");
  });
}

// Ao recarregar a pagina com um token salvo, o app perdia quem estava logado
// (appState.user so era preenchido no momento do login em si). Isso escondia
// os botoes de adulto (editar/excluir/nova tarefa) mesmo com um token valido.
function restoreUserFromToken() {
  const token = getToken();
  if (!token) return null;

  const payload = decodeToken(token);
  if (!payload) return null;

  return {
    userId: payload.sub,
    role: payload.role ?? payload[ROLE_CLAIM],
    name: payload.name,
  };
}

function boot() {
  const token = getToken();

  if (!token) {
    goToLogin();
    return;
  }

  const user = restoreUserFromToken();
  if (user) setUser(user);

  navigate(location.hash.replace("#", "") || "today");
}

window.addEventListener("hashchange", () => {
  if (getToken()) navigate(location.hash.replace("#", "") || "today");
});
window.addEventListener("pacus:unauthorized", () => { clearToken(); goToLogin(); });
boot();
