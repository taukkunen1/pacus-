// Cor de nascimento do PACUS.
//
// Cada PACUS "nasce" com uma cor (derivada do seu id, que nunca muda) e
// carrega essa mesma cor a vida inteira — so a intensidade dela evolui: mais
// pastel quando filhote, cada vez mais viva e saturada ate a fase adulta.
// Nao depende do backend guardar nada novo: o id ja e permanente por si so.

const STAGE_INTENSITY = {
  egg: { saturation: 46, lightness: 70 },
  cracking: { saturation: 50, lightness: 68 },
  hatching: { saturation: 56, lightness: 64 },
  baby: { saturation: 58, lightness: 64 },
  young: { saturation: 74, lightness: 54 },
  adult: { saturation: 90, lightness: 45 },
};

// Hash simples e estavel (mesma string => mesmo numero, sempre).
function hashSeed(seed) {
  const text = String(seed ?? "pacus");
  let hash = 0;
  for (let i = 0; i < text.length; i++) {
    hash = (hash * 31 + text.charCodeAt(i)) | 0;
  }
  return Math.abs(hash);
}

// O hue de nascimento: fixo para o mesmo PACUS (mesmo id) para sempre, a
// menos que o adulto tenha corrigido manualmente pelo painel de Configurações
// (ver Pacus.ColorHue no backend) -- nesse caso o valor salvo manda.
export function getBirthHue(pacus) {
  if (typeof pacus?.colorHue === "number" && Number.isFinite(pacus.colorHue)) {
    return ((pacus.colorHue % 360) + 360) % 360;
  }
  const seed = pacus?.id ?? pacus?.name ?? "pacus";
  return hashSeed(seed) % 360;
}

// Variaveis CSS (--pacus-hue/--pacus-color/--pacus-color-dark) prontas para
// ir no atributo style do .pacus-tank — a cor de nascimento fica cada vez
// mais intensa (mais saturada, mais escura/viva) conforme o estagio avanca.
export function getPacusColorStyle(pacus, stage) {
  const hue = getBirthHue(pacus);
  const intensity = STAGE_INTENSITY[stage] ?? STAGE_INTENSITY.adult;
  const shadeLightness = Math.max(intensity.lightness - 20, 12);

  return (
    `--pacus-hue:${hue};` +
    `--pacus-sat:${intensity.saturation}%;` +
    `--pacus-light:${intensity.lightness}%;` +
    `--pacus-color:hsl(${hue} ${intensity.saturation}% ${intensity.lightness}%);` +
    `--pacus-color-dark:hsl(${hue} ${intensity.saturation}% ${shadeLightness}%);`
  );
}
