// Feedback pra espera de "servidor acordando" (a API roda no Render, que em planos
// gratuitos hiberna depois de um tempo sem uso -- o primeiro acesso do dia pode levar
// dezenas de segundos pra responder). Sem isso, a tela so mostra "Carregando..."
// parado esse tempo todo, o que parece travado/quebrado em vez de "aguarde, o servidor
// esta iniciando". Ver docs/ESTADO_ATUAL.md, secao sobre lentidao no primeiro acesso.
//
// Uso: withSlowLoadHint(minhaPromise, () => atualizaTextoNaTela(), 4000)
// -- se minhaPromise ainda nao resolveu/rejeitou depois de delayMs, chama onSlow().
export function withSlowLoadHint(promise, onSlow, delayMs = 4000) {
  const timer = setTimeout(onSlow, delayMs);
  return promise.finally(() => clearTimeout(timer));
}

export const SLOW_LOAD_MESSAGE =
  "Ainda carregando... o servidor pode estar iniciando depois de um tempo parado. Isso pode levar até um minuto na primeira tentativa.";
