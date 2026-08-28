namespace Pacus.Application.DTOs;

// Ajuste manual de tempo do game timer, em minutos (positivo soma, negativo
// subtrai). Restrito a adulto — ver RequireRole no controller.
public record AdjustGameTimerRequest(int DeltaMinutes);
