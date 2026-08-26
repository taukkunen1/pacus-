namespace Pacus.Application.DTOs;

// Liga/desliga a trava de tarefas-da-manha -> tempo de jogo para a familia do
// adulto autenticado. Fica desligado por padrao para toda familia nova.
public record UpdateGameTimerRequest(bool Enabled, int? Minutes);
