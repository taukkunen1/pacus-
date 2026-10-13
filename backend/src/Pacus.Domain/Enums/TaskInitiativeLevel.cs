namespace Pacus.Domain.Enums;

// Autonomia e planejamento (2026-09-10): diferencia COMO a crianca comecou a
// tarefa, nao so SE ela terminou. Autodeclarado pela propria crianca (chip rapido
// na hora de iniciar/concluir -- ver docs/ESTADO_ATUAL.md), ja que o app hoje nao
// tem nenhuma notificacao push que permitiria inferir isso automaticamente.
//
// Usado somente para o relatorio semanal de autonomia (IAutonomyService).
// O autorrelato nao altera pontos, para nao incentivar respostas estrategicas.
public enum TaskInitiativeLevel
{
    // A crianca percebeu que precisava fazer e comecou por conta propria.
    SelfStarted,

    // A crianca comecou depois de usar alguma sugestao do proprio app (ex.:
    // botao "E agora?" -- ver docs/ESTADO_ATUAL.md).
    PromptedByPacus,

    // Um adulto precisou lembrar a crianca de fazer a tarefa.
    PromptedByAdult,
}

