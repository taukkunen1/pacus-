# PACUS 3D — status

Módulo em standby, criado a partir de `PACUS_3D_Especificacao_Completa.docx`
(seções 5, 6, 7, 8, 10, 15). **Ainda não está ligado nas telas** — `home.js`
e `pacus.js` continuam usando o habitat 2D (`habitat.js`, sprites reais
recortados do `pacus-reference.png`).

## Arquivos

- `controller.js` — API conceitual (`play`, `expression`, `lookAt`, `grow`,
  `react`), por cima de `THREE.AnimationMixer` + morph targets.
- `animations.js` — nomes de clipe esperados (seção 8) + crossfade.
- `expressions.js` — nomes/mistura de morph target por expressão (seção 7).
- `behavior.js` — máquina de estados IDLE / INTERACTION / NEEDS (seções 9-10).
- `renderer.js` — monta a cena Three.js, carrega `assets/pacus/pacus.glb`,
  liga tudo. Mesmo formato de retorno (`{dispose()}`) que o `habitat.js` 2D,
  pra trocar sem mexer em `home.js`/`pacus.js`.

## O que falta pra ligar

1. Receber o `pacus.glb` novo (gerado a partir da espec — Meshy/Tripo/Rodin
   ou modelador humano), com adulto rigado, pelo menos os clipes do MVP
   (`IDLE`, `BLINK`, `CURIOUS`, `HAPPY`, `SURPRISED`) e idealmente os nomes
   de osso da seção 6 (`Head`, `Eye.L`, `Eye.R`, `Tail.01..05`, etc).
2. Validar a malha e os skin weights (mesmo processo do rig anterior que
   quebrou — inspecionar o glb antes de subir).
3. Conferir os nomes reais dos morph targets exportados
   (`mesh.morphTargetDictionary`) e ajustar `EXPRESSION_MORPHS` em
   `expressions.js` se não baterem com o palpite atual.
4. Trocar `frontend/assets/pacus/pacus.glb` pelo arquivo novo.
5. Em `home.js`/`pacus.js`, trocar o import de `renderTank/mountTank3D` de
   `../pacus/habitat.js` para `../pacus/renderer.js` (`mountPacus3D`).
6. Re-adicionar o import map do Three.js em `index.html` (foi removido
   quando o 2D substituiu o 3D quebrado — ver histórico do git).
