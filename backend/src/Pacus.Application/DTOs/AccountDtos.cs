namespace Pacus.Application.DTOs;

// Exclusao de conta (LGPD, item B3). Exige a senha do adulto para confirmar a
// operacao -- irreversivel e apaga os dados de toda a familia, entao uma
// reautenticacao simples evita exclusao por sessao esquecida aberta/token roubado.
public record AccountDeletionRequest(string Password);
