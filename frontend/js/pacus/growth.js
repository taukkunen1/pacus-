// Metadados dos estagios do PACUS. O backend (GET /pacus/me) e quem decide o
// estagio atual — este modulo so descreve como cada um deve aparecer/soar.
const STAGES = [
  {
    key: "egg",
    label: "Ovo",
    caption: "Um ovo quietinho no fundo do tanque... o que sera que vai nascer?",
    isEgg: true,
    sizeScale: 0.55
  },
  {
    key: "cracking",
    label: "Rachando",
    caption: "O ovo comecou a rachar!",
    isEgg: true,
    sizeScale: 0.62
  },
  {
    key: "hatching",
    label: "Eclodindo",
    caption: "Quase la — o PACUS esta eclodindo!",
    isEgg: true,
    sizeScale: 0.7
  },
  {
    key: "baby",
    label: "Filhote",
    caption: "Pacus esta por aqui em algum lugar 👀",
    isEgg: false,
    sizeScale: 0.6
  },
  {
    key: "young",
    label: "Jovem",
    caption: "Pacus esta por aqui em algum lugar 👀",
    isEgg: false,
    sizeScale: 0.82
  },
  {
    key: "adult",
    label: "Adulto",
    caption: "Pacus esta por aqui em algum lugar 👀",
    isEgg: false,
    sizeScale: 1
  }
];

const DEFAULT_STAGE = STAGES[0];

export function getStageInfo(stage) {
  const found = STAGES.find((item) => item.key === stage);
  return found ?? DEFAULT_STAGE;
}

export function getStageIndex(stage) {
  const index = STAGES.findIndex((item) => item.key === stage);
  return index === -1 ? 0 : index;
}

export function getNextStageInfo(stage) {
  const index = getStageIndex(stage);
  return index >= STAGES.length - 1 ? null : STAGES[index + 1];
}

export function getStageProgress(stage) {
  return getStageIndex(stage) / (STAGES.length - 1);
}

export function getAllStages() {
  return STAGES;
}
