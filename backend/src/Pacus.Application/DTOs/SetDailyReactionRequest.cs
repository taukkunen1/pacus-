namespace Pacus.Application.DTOs;

// Reacao pessoal do adulto sobre o dia (relatedness -- ver docs/PROPOSITO.md e
// Domain/Entities/DailyReaction.cs). Restrito a adulto -- ver RequireRole no controller.
// Icon precisa ser uma das chaves conhecidas (ver DailyRoutineService.AllowedReactionIcons);
// Message e opcional (null/vazio = so o icone).
public record SetDailyReactionRequest(string Icon, string? Message);
