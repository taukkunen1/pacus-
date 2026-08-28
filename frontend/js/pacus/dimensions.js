// Dimensionamento real (cm) do PACUS e do habitat.
// Ver docs/pacus-dimensionamento-3d.md para o raciocínio completo.
//
// Convenção adotada para o futuro export do .glb em metros reais
// (padrão glTF): 1 unidade do modelo = 1 metro. Enquanto o rig atual
// segue usando unidades "de cena" arbitrárias (ajustadas no olho para a
// câmera existente), este módulo guarda a régua real em cm e um fator
// único unidade-de-cena -> cm, para que:
//   1) a UI possa exibir o tamanho real do estágio atual (ex.: "16 cm");
//   2) o dia em que o rig/habitat forem recalibrados para metros reais
//      (ver seção 4 do doc - rig + habitat + câmera juntos) tenha um
//      ponto de partida documentado, em vez de números "no olho".
//
// IMPORTANTE: UNITS_PER_CM foi calibrado para reproduzir os valores que
// já existiam em `addHabitatDecor` (raio da água = 1.78 unidades <-> 40cm
// de raio / 80cm de diâmetro), então esta mudança não altera o visual
// atual - só documenta e nomeia o fator que já estava implícito.

export const UNITS_PER_CM = 1.78 / 40; // ~= 0.0445 unidades de cena por cm

export function cmToSceneUnits(cm) {
  return cm * UNITS_PER_CM;
}

// Pacus adulto = 24cm (nariz à ponta da nadadeira caudal), dentro da
// faixa realista de um axolote adulto de estimação (~23-28cm).
export const PACUS_ADULT_LENGTH_CM = 24;

// Espelha PACUS_STAGE_SCALE (pacus.js) multiplicado pela referência adulta.
// Mantido como tabela própria (em vez de importar PACUS_STAGE_SCALE) para
// não criar dependência circular entre pacus.js e este módulo; os dois
// devem ser mantidos em sincronia - ver PACUS_STAGE_SCALE em pacus.js.
export const PACUS_STAGE_SIZE_CM = Object.freeze({
  egg: 8,
  cracking: 10,
  hatching: 12.5,
  baby: 16,
  young: 20,
  adult: 24,
});

export function getPacusStageSizeCm(stage) {
  return PACUS_STAGE_SIZE_CM[stage] ?? PACUS_STAGE_SIZE_CM.baby;
}

// Habitat: tanque fixo desde o início - não cresce junto com o Pacus
// (assim como um axolote de estimação real não deveria trocar de tanque
// conforme cresce). `addHabitatDecor` em pacus.js já não varia por
// estágio; estes valores só documentam as medidas reais por trás dos
// números de cena usados lá.
export const HABITAT_DIMENSIONS_CM = Object.freeze({
  tankDiameter: 80,
  waterHeight: 20,
  substrateThickness: 3,
  decorSize: [10, 14],
});
