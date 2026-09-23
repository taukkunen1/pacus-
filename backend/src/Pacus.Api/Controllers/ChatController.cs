using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using Pacus.Api.Auth;
using Pacus.Application.Interfaces;
using Pacus.Domain.Entities;
using Pacus.Domain.Enums;

namespace Pacus.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/chat")]
public class ChatController : ControllerBase
{
    private const int MaxMessageLength = 2000;
    private static readonly HashSet<string> AllowedRequestTypes =
        ["help", "change_task", "extra_time"];

    private readonly IChatMessageRepository _chatRepository;
    private readonly IChatReadStateRepository _readStateRepository;
    private readonly IUserRepository _userRepository;
    private readonly IDailyRoutineService _dailyRoutineService;
    private readonly ICurrentUserService _currentUser;

    public ChatController(
        IChatMessageRepository chatRepository,
        IChatReadStateRepository readStateRepository,
        IUserRepository userRepository,
        IDailyRoutineService dailyRoutineService,
        ICurrentUserService currentUser)
    {
        _chatRepository = chatRepository;
        _readStateRepository = readStateRepository;
        _userRepository = userRepository;
        _dailyRoutineService = dailyRoutineService;
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

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var readState = await _readStateRepository.GetAsync(
            _currentUser.FamilyId,
            _currentUser.UserId);

        var count = await _chatRepository.CountUnreadAsync(
            _currentUser.FamilyId,
            _currentUser.UserId,
            readState?.LastReadMessageId);

        var pendingRequests = _currentUser.Role == UserRole.Adult
            ? await _chatRepository.CountPendingRequestsAsync(_currentUser.FamilyId)
            : 0;

        return Ok(new
        {
            unreadCount = count,
            pendingRequests
        });
    }

    [HttpPut("read")]
    public async Task<IActionResult> MarkRead([FromBody] MarkChatReadRequest request)
    {
        if (!ObjectId.TryParse(request.LastMessageId, out var messageId))
            return BadRequest(new { error = "lastMessageId invalido." });

        var message = await _chatRepository.GetByIdForFamilyAsync(
            _currentUser.FamilyId,
            messageId);

        if (message is null)
            return NotFound(new { error = "Mensagem nao encontrada." });

        await _readStateRepository.UpsertAsync(
            _currentUser.FamilyId,
            _currentUser.UserId,
            message.Id,
            message.CreatedAt);

        var count = await _chatRepository.CountUnreadAsync(
            _currentUser.FamilyId,
            _currentUser.UserId,
            message.Id);

        var pendingRequests = _currentUser.Role == UserRole.Adult
            ? await _chatRepository.CountPendingRequestsAsync(_currentUser.FamilyId)
            : 0;

        return Ok(new
        {
            unreadCount = count,
            pendingRequests
        });
    }

    [HttpPost("messages")]
    public async Task<IActionResult> SendMessage([FromBody] SendChatMessageRequest request)
    {
        var text = request.Text?.Trim() ?? string.Empty;

        if (text.Length == 0)
            return BadRequest(new { error = "Digite uma mensagem." });

        if (text.Length > MaxMessageLength)
            return BadRequest(new { error = $"A mensagem pode ter no maximo {MaxMessageLength} caracteres." });

        var sender = await GetCurrentSenderAsync();
        if (sender is null)
            return Unauthorized();

        var message = new ChatMessage
        {
            Id = ObjectId.GenerateNewId(),
            FamilyId = _currentUser.FamilyId,
            SenderId = sender.Id,
            SenderName = sender.Name,
            SenderRole = sender.Role,
            Text = text,
            Kind = "message",
            CreatedAt = DateTime.UtcNow,
        };

        await _chatRepository.CreateAsync(message);

        return Ok(ToResponse(message));
    }

    [RequireRole(UserRole.Child)]
    [HttpPost("requests")]
    public async Task<IActionResult> CreateQuickRequest(
        [FromBody] CreateChatQuickRequest request)
    {
        var type = request.Type?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!AllowedRequestTypes.Contains(type))
        {
            return BadRequest(new
            {
                error = "Tipo de pedido invalido."
            });
        }

        int? requestedMinutes = null;
        if (type == "extra_time")
        {
            requestedMinutes = request.Minutes ?? 10;
            if (requestedMinutes < 5 || requestedMinutes > 60)
            {
                return BadRequest(new
                {
                    error = "O pedido de tempo extra deve ser entre 5 e 60 minutos."
                });
            }
        }

        var note = request.Note?.Trim();
        if (note?.Length > 500)
            return BadRequest(new { error = "A observacao pode ter no maximo 500 caracteres." });

        var sender = await GetCurrentSenderAsync();
        if (sender is null)
            return Unauthorized();

        var message = new ChatMessage
        {
            Id = ObjectId.GenerateNewId(),
            FamilyId = _currentUser.FamilyId,
            SenderId = sender.Id,
            SenderName = sender.Name,
            SenderRole = sender.Role,
            Text = BuildRequestText(type, requestedMinutes, note),
            Kind = "request",
            RequestType = type,
            RequestStatus = "pending",
            RequestedMinutes = requestedMinutes,
            CreatedAt = DateTime.UtcNow,
        };

        await _chatRepository.CreateAsync(message);

        return Ok(ToResponse(message));
    }

    [RequireRole(UserRole.Adult)]
    [HttpPut("requests/{id}/approve")]
    public async Task<IActionResult> ApproveRequest(string id)
    {
        if (!ObjectId.TryParse(id, out var messageId))
            return BadRequest(new { error = "Id de pedido invalido." });

        var claimed = await _chatRepository.TryTransitionRequestAsync(
            _currentUser.FamilyId,
            messageId,
            "pending",
            "processing");

        if (claimed is null)
        {
            return Conflict(new
            {
                error = "Este pedido ja foi revisado ou nao esta mais pendente."
            });
        }

        try
        {
            if (claimed.RequestType == "extra_time")
            {
                var minutes = claimed.RequestedMinutes ?? 0;
                if (minutes <= 0)
                    throw new InvalidOperationException("Pedido de tempo extra sem minutos validos.");

                await _dailyRoutineService.AdjustGameTimerAsync(
                    _currentUser.FamilyId,
                    minutes,
                    _currentUser.UserId,
                    _currentUser.Role.ToString());
            }

            var approved = await _chatRepository.TryTransitionRequestAsync(
                _currentUser.FamilyId,
                messageId,
                "processing",
                "approved",
                _currentUser.UserId,
                DateTime.UtcNow);

            if (approved is null)
                return Conflict(new { error = "Nao foi possivel concluir a aprovacao." });

            await CreateReviewNotificationAsync(approved, approved: true);
            return Ok(ToResponse(approved));
        }
        catch
        {
            await _chatRepository.TryTransitionRequestAsync(
                _currentUser.FamilyId,
                messageId,
                "processing",
                "pending");
            throw;
        }
    }

    [RequireRole(UserRole.Adult)]
    [HttpPut("requests/{id}/reject")]
    public async Task<IActionResult> RejectRequest(string id)
    {
        if (!ObjectId.TryParse(id, out var messageId))
            return BadRequest(new { error = "Id de pedido invalido." });

        var rejected = await _chatRepository.TryTransitionRequestAsync(
            _currentUser.FamilyId,
            messageId,
            "pending",
            "rejected",
            _currentUser.UserId,
            DateTime.UtcNow);

        if (rejected is null)
        {
            return Conflict(new
            {
                error = "Este pedido ja foi revisado ou nao esta mais pendente."
            });
        }

        await CreateReviewNotificationAsync(rejected, approved: false);
        return Ok(ToResponse(rejected));
    }

    private async Task CreateReviewNotificationAsync(
        ChatMessage request,
        bool approved)
    {
        var reviewer = await GetCurrentSenderAsync();
        if (reviewer is null)
            return;

        var requestLabel = request.RequestType switch
        {
            "help" => "pedido de ajuda",
            "change_task" => "pedido para conversar sobre uma tarefa",
            "extra_time" => $"pedido de +{request.RequestedMinutes ?? 0} min",
            _ => "pedido"
        };

        var message = new ChatMessage
        {
            Id = ObjectId.GenerateNewId(),
            FamilyId = _currentUser.FamilyId,
            SenderId = reviewer.Id,
            SenderName = reviewer.Name,
            SenderRole = reviewer.Role,
            Text = approved
                ? $"Aprovado: {requestLabel}."
                : $"Recusado: {requestLabel}.",
            Kind = "message",
            CreatedAt = DateTime.UtcNow,
        };

        try
        {
            await _chatRepository.CreateAsync(message);
        }
        catch
        {
            // A decisao do pedido ja foi persistida. Falha na mensagem de retorno
            // nao pode transformar uma aprovacao/rejeicao concluida em erro para o usuario.
        }
    }

    private async Task<User?> GetCurrentSenderAsync()
    {
        var sender = await _userRepository.GetByIdAsync(_currentUser.UserId);
        return sender is not null && sender.FamilyId == _currentUser.FamilyId
            ? sender
            : null;
    }

    private static string BuildRequestText(
        string type,
        int? minutes,
        string? note)
    {
        var baseText = type switch
        {
            "help" => "Preciso de ajuda.",
            "change_task" => "Quero conversar sobre mudar uma tarefa.",
            "extra_time" => $"Posso ter mais {minutes} minutos de tempo de tela?",
            _ => "Tenho um pedido."
        };

        return string.IsNullOrWhiteSpace(note)
            ? baseText
            : $"{baseText} {note}";
    }

    private static ChatMessageResponse ToResponse(ChatMessage message) =>
        new(
            message.Id.ToString(),
            message.SenderId.ToString(),
            message.SenderName,
            message.SenderRole.ToString(),
            message.Text,
            message.Kind,
            message.RequestType,
            message.RequestStatus,
            message.RequestedMinutes,
            message.ReviewedBy?.ToString(),
            message.ReviewedAt,
            message.CreatedAt);
}

public record SendChatMessageRequest(string? Text);
public record MarkChatReadRequest(string? LastMessageId);
public record CreateChatQuickRequest(string? Type, int? Minutes, string? Note);

public record ChatMessageResponse(
    string Id,
    string SenderId,
    string SenderName,
    string SenderRole,
    string Text,
    string Kind,
    string? RequestType,
    string? RequestStatus,
    int? RequestedMinutes,
    string? ReviewedBy,
    DateTime? ReviewedAt,
    DateTime CreatedAt);
