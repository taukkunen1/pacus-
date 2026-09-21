namespace Pacus.Application.DTOs;

// Minutos escolhidos pela crianca para uma sessao de tempo de tela.
// O debito acontece quando a sessao termina no frontend.
public record ConsumeGameTimerRequest(int Minutes);
