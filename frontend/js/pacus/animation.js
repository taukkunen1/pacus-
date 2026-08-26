// Helpers pequenos de animacao, compartilhados por movement.js e behavior.js.
// Nada aqui depende de canvas/rAF — tudo e classe CSS + timers, para ficar leve.

export function prefersReducedMotion() {
  return (
    window.matchMedia?.(
      "(prefers-reduced-motion: reduce)"
    ).matches ?? false
  );
}

export function randomBetween(min, max) {
  return min + Math.random() * (max - min);
}

// Adiciona uma classe por `durationMs` e remove sozinha (ex.: um "dart"
// pontual). Retorna uma funcao de cancelamento, caso o elemento suma antes.
export function pulse(el, className, durationMs) {
  if (!el) return () => {};
  el.classList.add(className);
  const timeoutId = window.setTimeout(() => {
    el.classList.remove(className);
  }, durationMs);
  return () => {
    window.clearTimeout(timeoutId);
    el.classList.remove(className);
  };
}

// Dispara `callback` repetidamente em intervalos aleatorios dentro de
// [minMs, maxMs], em vez de um setInterval fixo (fica menos mecanico).
// Retorna uma funcao de cancelamento — sempre guarde e chame no cleanup.
export function createRandomLoop([minMs, maxMs], callback) {
  let timeoutId = null;
  let cancelled = false;

  function tick() {
    if (cancelled) return;
    callback();
    if (cancelled) return;
    timeoutId = window.setTimeout(tick, randomBetween(minMs, maxMs));
  }

  timeoutId = window.setTimeout(tick, randomBetween(minMs, maxMs));

  return () => {
    cancelled = true;
    if (timeoutId !== null) window.clearTimeout(timeoutId);
  };
}
