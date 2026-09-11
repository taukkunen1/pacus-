namespace Pacus.Domain.Enums;

// Autonomia e planejamento (2026-09-10): motivo pelo qual uma tarefa nao foi feita,
// autodeclarado pela crianca -- nunca usado para punir (sem perda de pontos, sem
// retirada de recompensa, sem mensagem negativa). So serve para o app (e o adulto,
// se quiser olhar) entenderem por que certas tarefas ficam pra tras com frequencia
// (ver docs/ESTADO_ATUAL.md, item "Nao utilizar punicao").
public enum TaskSkipReason
{
    Sleepy,                 // Estava com sono
    PreferredOtherActivity, // Preferi outra atividade
    NoTime,                 // Nao deu tempo
    NotInTheMood,           // Nao estava com vontade
    Disliked,               // Nao gostei
    Forgot,                 // Esqueci
    Other,                  // Outro -- ver DailyTask.SkipReasonNote
}

