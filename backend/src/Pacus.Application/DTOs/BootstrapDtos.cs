namespace Pacus.Application.DTOs;

public record BootstrapRequest(
    string AdultName,
    string AdultEmail,
    string AdultPassword,
    string ChildName,
    string ChildPin
);

public record BootstrapResponse(
    string AdultUserId,
    string ChildUserId,
    string FamilyId,
    string PacusId,
    string Message,
    // Codigo de recuperacao de senha do adulto -- so vem preenchido aqui, uma unica vez.
    // Depois disso so o hash fica salvo no banco.
    string RecoveryCode
);