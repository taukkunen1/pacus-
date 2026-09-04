import { getToken, clearToken, decodeToken } from "./api/api-client.js";
import { renderLogin } from "./screens/login.js";
import { renderHome } from "./screens/home.js";
import { renderHistory } from "./screens/history.js";
import { renderPoints } from "./screens/points.js";
import { renderPacus } from "./screens/pacus.js";
import { renderStore } from "./screens/store.js";
import { renderSettings } from "./screens/settings.js";
import { setUser } from "./state/app-state.js";
import { watchForDayBoundary } from "./utils/day-boundary-watch.js";

const ROLE_CLAIM = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";

const root = document.getElementById("app");

// So recarrega sozinho quando a tela atual e a "Hoje" -- nas outras abas nao
// tem nada que fique desatualizado pela virada do dia, e forcar uma
// navegacao de volta pra "Hoje" atrapalharia quem esta vendo outra tela.
let currentScreen = "today";

function navigate(screen = "today") {
  currentScreen = screen;

  if (screen === "history") return renderHistory(root, navigate);
  if (screen === "points") return renderPoints(root, navigate);
  if (screen === "pacus") return renderPacus(root, navigate);
  if (screen === "store") return renderStore(root, navigate);
  // renderSettings se auto-protege (redireciona pra "today" se nao for
  // adulto) -- ver screens/settings.js -- entao nao precisa de checagem aqui.
  if (screen === "settings") return renderSettings(root, navigate);
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

// Corrige o bug de "a tarefa de hoje nao resetou": o back-end ja fecha o dia
// certinho a cada requisicao (DayClosingService), mas a tela "Hoje" so
// buscava a rotina uma vez, na montagem -- se o app ficasse aberto
// atravessando a meia-noite, continuava mostrando o dia anterior ate alguem
// mexer em alguma coisa. Isso aqui recarrega a tela sozinha nesse caso.
watchForDayBoundary(() => {
  if (getToken() && currentScreen === "today") navigate("today");
});

boot();
