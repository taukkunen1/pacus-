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

  function scheduleNextMidnight() {
    const now = new Date();
    // +5s de folga pra garantir que ja virou o dia local quando disparar.
    const nextMidnight = new Date(now.getFullYear(), now.getMonth(), now.getDate() + 1, 0, 0, 5);
    const msUntil = nextMidnight.getTime() - now.getTime();

    timerId = setTimeout(() => {
      onBoundary();
      scheduleNextMidnight();
    }, msUntil);
  }

  function handleVisibilityChange() {
    if (!document.hidden) onBoundary();
  }

  scheduleNextMidnight();
  document.addEventListener("visibilitychange", handleVisibilityChange);
  window.addEventListener("focus", onBoundary);

  return function stopWatching() {
    if (timerId) clearTimeout(timerId);
    document.removeEventListener("visibilitychange", handleVisibilityChange);
    window.removeEventListener("focus", onBoundary);
  };
}
