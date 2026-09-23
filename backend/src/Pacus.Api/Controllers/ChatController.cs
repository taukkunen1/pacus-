using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using Pacus.Application.Interfaces;
using Pacus.Domain.Entities;

namespace Pacus.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/chat")]
public class ChatController : ControllerBase
{
    private const int MaxMessageLength = 2000;

    private readonly IChatMessageRepository _chatRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUserService _currentUser;

    public ChatController(
        IChatMessageRepository chatRepository,
        IUserRepository userRepository,
        ICurrentUserService currentUser)
    {
        _chatRepository = chatRepository;
        _userRepository = userRepository;
        _currentUser = currentUser;
    }

    [HttpGet("messages")]
    public async Task<IActionResult> GetMessages([FromQuery] string? afterId = null)
    {
        ObjectId? parsedAfterId = null;

        if (!string.IsNullOrWhiteSpace(afterId))
        {
            if (!ObjectId.TryParse(afterId, out var objectId))
                return BadRequest(new { error = "afterId invalido." });

            parsedAfterId = objectId;
        }

        var messages = await _chatRepository.GetRecentByFamilyAsync(
            _currentUser.FamilyId,
            parsedAfterId);

        return Ok(messages.Select(ToResponse));
    }

    [HttpPost("messages")]
    public async Task<IActionResult> SendMessage([FromBody] SendChatMessageRequest request)
    {
        var text = request.Text?.Trim() ?? string.Empty;

        if (text.Length == 0)
            return BadRequest(new { error = "Digite uma mensagem." });

        if (text.Length > MaxMessageLength)
            return BadRequest(new { error = $"A mensagem pode ter no maximo {MaxMessageLength} caracteres." });

        var sender = await _userRepository.GetByIdAsync(_currentUser.UserId);

        if (sender is null || sender.FamilyId != _currentUser.FamilyId)
            return Unauthorized();

        var message = new ChatMessage
        {
            Id = ObjectId.GenerateNewId(),
            FamilyId = _currentUser.FamilyId,
            SenderId = sender.Id,
            SenderName = sender.Name,
            SenderRole = sender.Role,
            Text = text,
            CreatedAt = DateTime.UtcNow,
        };

        await _chatRepository.CreateAsync(message);

        return Ok(ToResponse(message));
    }

    private static ChatMessageResponse ToResponse(ChatMessage message) =>
        new(
            message.Id.ToString(),
            message.SenderId.ToString(),
            message.SenderName,
            message.SenderRole.ToString(),
            message.Text,
            message.CreatedAt);
}

public record SendChatMessageRequest(string? Text);

public record ChatMessageResponse(
    string Id,
    string SenderId,
    string SenderName,
    string SenderRole,
    string Text,
    DateTime CreatedAt);
