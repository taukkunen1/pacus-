// Garante que a tela "Hoje" nao fique presa mostrando o dia anterior se o
// app ficar aberto (aba do navegador, ou instalado como PWA num tablet fixo
// da casa) atravessando a virada do dia. O back-end ja fecha o dia certinho
// a cada chamada nova (ver DayClosingService.CloseIfDueAsync, que roda antes
// de todo GET /daily-routines/today e recupera qualquer atraso, mesmo de
// varios dias) -- o que faltava era algo recarregando a TELA sozinha, ja que
// ela so buscava a rotina uma vez, na montagem.
//
// Dispara `onBoundary` em dois casos, que cobrem os cenarios reais:
// (a) perto da meia-noite local do navegador, mesmo com a tela em primeiro
//     plano o tempo todo (ex.: tablet fixo, nunca minimizado);
// (b) sempre que a aba/janela volta a ficar visivel ou em foco -- cobre o
//     caso mais comum, de alguem ter deixado o navegador aberto durante a
//     noite e voltar a olhar a tela so na manha seguinte.
//
// Nao tenta calcular a meia-noite no fuso horario da familia (isso e feito
// no servidor, que e sempre a fonte da verdade) -- e so um gatilho pra
// buscar de novo; se o navegador estiver num fuso diferente do da familia, o
// pior caso e recarregar um pouco antes/depois da virada real, e o retorno
// da aba a primeiro plano (b) ainda cobre a diferenca.
export function watchForDayBoundary(onBoundary) {
  let timerId = null;

  // Dia local atual, so pra comparar depois -- "voltar o foco" (troca de aba,
  // alt-tab, minimizar) acontece a toda hora e NAO significa que o dia virou.
  // Antes disparava onBoundary() incondicionalmente em todo focus/
  // visibilitychange, o que forcava a tela "Hoje" inteira a recarregar (com o
  // flash de "Carregando sua rotina...") sempre que alguem so trocava de aba
  // por um instante -- o timer de jogo em si nao precisa disso, ja que
  // startGameTimerCountdown (screens/home.js) recalcula sozinho a cada
  // segundo sem bater no servidor. Agora so dispara de verdade quando o dia
  // (ano/mes/dia local) mudou desde a ultima checagem.
  function dayKey() {
    const now = new Date();
    return `${now.getFullYear()}-${now.getMonth()}-${now.getDate()}`;
  }

  let lastDayKey = dayKey();

  function checkAndFireIfNewDay() {
    const key = dayKey();
    if (key === lastDayKey) return;
    lastDayKey = key;
    onBoundary();
  }

  function scheduleNextMidnight() {
    const now = new Date();
    // +5s de folga pra garantir que ja virou o dia local quando disparar.
    const nextMidnight = new Date(now.getFullYear(), now.getMonth(), now.getDate() + 1, 0, 0, 5);
    const msUntil = nextMidnight.getTime() - now.getTime();

    timerId = setTimeout(() => {
      checkAndFireIfNewDay();
      scheduleNextMidnight();
    }, msUntil);
  }

  function handleVisibilityChange() {
    if (!document.hidden) checkAndFireIfNewDay();
  }

  scheduleNextMidnight();
  document.addEventListener("visibilitychange", handleVisibilityChange);
  window.addEventListener("focus", checkAndFireIfNewDay);

  return function stopWatching() {
    if (timerId) clearTimeout(timerId);
    document.removeEventListener("visibilitychange", handleVisibilityChange);
    window.removeEventListener("focus", checkAndFireIfNewDay);
  };
}
