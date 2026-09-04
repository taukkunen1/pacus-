import { apiClient } from "./api-client.js";

// Cadastro inicial da familia (1 adulto + 1 crianca) -- endpoint anonimo, e o
// proprio ponto de entrada que cria a conta (ver BootstrapService no backend).
// Devolve, so nesta resposta, o codigo de recuperacao de senha do adulto e o
// codigo da familia que a crianca vai usar pra logar -- a tela de cadastro
// (screens/login.js) deve orientar o usuario a guardar os dois.
export const createFamily = (adultName, adultEmail, adultPassword, childName, childPin) =>
  apiClient("/bootstrap", {
    method: "POST",
    body: JSON.stringify({ adultName, adultEmail, adultPassword, childName, childPin }),
  });
