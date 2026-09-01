// Frases de reforço de ESFORÇO mostradas ao concluir uma tarefa — não elogio de
// resultado/traço ("você é ótimo"), mas de esforço/estratégia/persistência ("você
// tentou", "você lembrou sozinho"). Baseado na pesquisa de Dweck & Mueller (1998) sobre
// mindset de crescimento: elogiar esforço aumenta persistência e busca por desafio;
// elogiar habilidade/resultado reduz resiliência. Ver docs/PROPOSITO.md.
//
// A escolha da frase é sensível ao contexto (mais alinhado à pesquisa do que uma frase
// genérica solta): o tipo/pontuação da tarefa e se ela fecha o período ou o dia inteiro
// mudam qual banco de frases é usado. Dentro do banco escolhido, a frase é aleatória
// pra não ficar repetitiva.
//
// Contextos mais específicos (ex.: "primeira vez fazendo isso" ou "dias seguidos
// cumprindo essa tarefa") ficaram de fora desta primeira versão -- exigiriam consultar
// o histórico de rotinas passadas (API extra), não só os dados que a tela "Hoje" já
// carrega. Ver nota em docs/ESTADO_ATUAL.md.

const GENERIC_EFFORT = [
  "Você conseguiu!",
  "Boa, você cuidou disso!",
  "Isso é responsabilidade!",
  "Você se organizou sozinho!",
  "Mandou bem!",
  "Show, você fez!",
];

const CHALLENGE_EFFORT = [
  "Você topou o desafio!",
  "Coragem de tentar algo novo!",
  "Isso não era fácil, e você foi!",
  "Adorei sua criatividade nisso!",
];

const HIGH_EFFORT_POINTS_THRESHOLD = 5;
const HIGH_EFFORT = [
  "Essa exigiu esforço de verdade!",
  "Você persistiu até o fim!",
  "Trabalho difícil, bem feito!",
];

const PERIOD_COMPLETE = [
  "Você fechou tudo desse período!",
  "Período inteiro em dia, muito bem!",
];

const DAY_COMPLETE = [
  "Você cuidou do dia inteiro sozinho!",
  "Dia completo — isso é consistência!",
  "Você deu conta de tudo hoje!",
];

function pickRandom(list) {
  return list[Math.floor(Math.random() * list.length)];
}

// `task` é a tarefa recem-concluida; `allTasks` e a lista completa da rotina do dia
// (routine.tasks) JA COM o status atualizado (pos-conclusao), pra poder checar se
// fechou o periodo/dia inteiro.
export function pickEffortMessage(task, allTasks = []) {
  const active = allTasks.filter((t) => !t.deletedAt);

  const dayComplete =
    active.length > 0 && active.every((t) => t.status === "done");
  if (dayComplete) return pickRandom(DAY_COMPLETE);

  const periodTasks = active.filter((t) => t.period === task.period);
  const periodComplete =
    periodTasks.length > 0 && periodTasks.every((t) => t.status === "done");
  if (periodComplete) return pickRandom(PERIOD_COMPLETE);

  if (task.type === "challenge") return pickRandom(CHALLENGE_EFFORT);

  if (Math.abs(task.points ?? 0) >= HIGH_EFFORT_POINTS_THRESHOLD) {
    return pickRandom(HIGH_EFFORT);
  }

  return pickRandom(GENERIC_EFFORT);
}
