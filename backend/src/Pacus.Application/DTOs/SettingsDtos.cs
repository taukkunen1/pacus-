namespace Pacus.Application.DTOs;

// Liga/desliga a trava de tarefas-da-manha -> tempo de jogo para a familia do
// adulto autenticado. Fica desligado por padrao para toda familia nova.
public record UpdateGameTimerRequest(bool Enabled, int? Minutes);

// Valor de referencia de 1 PP em reais. Nao representa moeda, saldo bancario
// nem promessa de conversao financeira; e uma referencia definida pela familia.
public record UpdatePointToBrlRateRequest(double Rate);

// Estagio de crescimento do PACUS a partir de qual data do calendario (ex.: Egg 2026-08-26
// -> Adult 2026-09-26). Stage aceita os nomes de Pacus.Domain.Enums.PacusStage (qualquer
// caixa -- convertido com ignoreCase, mesmo padrao usado no resto do backend).
public record GrowthStageConfigDto(string Stage, string Date);

public record UpdateGrowthStagesRequest(List<GrowthStageConfigDto> Stages);
