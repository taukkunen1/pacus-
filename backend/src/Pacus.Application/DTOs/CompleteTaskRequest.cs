namespace Pacus.Application.DTOs;

// Cobre tanto marcar quanto desmarcar (toggle livre, a qualquer momento).
public record CompleteTaskRequest(bool Completed);
