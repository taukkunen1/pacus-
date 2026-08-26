namespace Pacus.Application.DTOs;

// Define o saldo de Pacus Points para um valor absoluto (em vez de um delta),
// pensado para migrar um saldo que a familia ja tinha antes deste app existir.
// Por baixo, isso gera uma unica transacao do tipo Adjustment com o delta
// necessario para chegar nesse valor — o extrato continua auditavel.
public record SetPointsBalanceRequest(int Balance, string? Reason);
