import { getToken, clearToken } from "./api/api-client.js";
import { renderLogin } from "./screens/login.js";
import { renderHome } from "./screens/home.js";
import { renderHistory } from "./screens/history.js";
import { renderPoints } from "./screens/points.js";
import { renderPacus } from "./screens/pacus.js";
import { setUser } from "./state/app-state.js";

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

function boot() {
  if (getToken()) navigate(location.hash.replace("#", "") || "today");
  else goToLogin();
}

window.addEventListener("hashchange", () => {
  if (getToken()) navigate(location.hash.replace("#", "") || "today");
});
window.addEventListener("pacus:unauthorized", () => { clearToken(); goToLogin(); });
boot();
