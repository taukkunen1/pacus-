// Estado minimo em memoria — sem framework, sem localStorage alem do token de auth
// (ver api-client.js). Um pub-sub simples o suficiente para as poucas telas atuais.
const listeners = new Set();

export const appState = {
  user: null, // { userId, role, name }
};

export function setUser(user) {
  appState.user = user;
  listeners.forEach((fn) => fn(appState));
}

export function subscribe(fn) {
  listeners.add(fn);
  return () => listeners.delete(fn);
}
