import { apiClient } from "./api-client.js";

export const getFamilyChildren = () => apiClient("/family/children");

export const updateChildPin = (childId, newPin) =>
  apiClient(`/family/children/${childId}/pin`, {
    method: "PUT",
    body: JSON.stringify({ newPin }),
  });

export const getFamilyTimezone = () => apiClient("/family/timezone");

export const updateFamilyTimezone = (timezone) =>
  apiClient("/family/timezone", {
    method: "PUT",
    body: JSON.stringify({ timezone }),
  });

export const generateRecoveryCode = () =>
  apiClient("/family/recovery-code", { method: "POST" });

// Usado pela tela de login da crianca pra encontrar a familia por um codigo
// curto (ver User.FamilyCode) em vez de colar um id do Mongo. Anonimo no
// backend -- nao exige token.
export const getChildrenByFamilyCode = (code) =>
  apiClient(`/family/by-code/${encodeURIComponent(code)}/children`);

// Pro adulto reconsultar o codigo da propria familia (ex.: pra mostrar de novo
// na tela de Configurações, se o código mostrado uma vez no cadastro já não
// estiver mais à mão).
export const getFamilyCode = () => apiClient("/family/code");
