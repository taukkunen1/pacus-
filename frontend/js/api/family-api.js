import { apiClient } from "./api-client.js";

export const getFamilyChildren = () => apiClient("/family/children");

// Anonimo (sem token) -- e o primeiro passo do login da crianca num aparelho
// novo, antes de existir qualquer sessao (ver screens/login.js FAMILY_CODE_KEY).
// O codigo e o mesmo mostrado ao adulto no cadastro (ver api/bootstrap-api.js)
// e na tela de Configuracoes.
export const getChildrenByFamilyCode = (familyCode) =>
  apiClient(`/family/by-code/${encodeURIComponent(familyCode)}/children`);

// So o adulto consegue reconsultar o codigo da propria familia (ex.: tela de
// Configuracoes, ou pra repassar pra criança de novo se ela esquecer).
export const getFamilyCode = () => apiClient("/family/code");

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
