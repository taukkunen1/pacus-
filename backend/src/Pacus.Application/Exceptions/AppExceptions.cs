namespace Pacus.Application.Exceptions;

// Tipos de excecao proprios do dominio, pra parar de usar InvalidOperationException
// (do .NET, pensada pra estado interno inconsistente, nao validacao de entrada do
// usuario) como sinal generico de "isso deu errado" em todo o app -- ver achado #2
// da auditoria de API de 2026-09-01. Mapeados pro status HTTP certo por
// Pacus.Api.Middleware.AppExceptionHandler; os services so lancam, nunca escolhem o
// status code diretamente (mantem a camada de aplicacao livre de HTTP).
//
// Convencao de quando usar cada um (services, nao controllers, sao quem decide):
// - NotFoundException: o recurso pedido nao existe (ou nao pertence a esta familia --
//   ver o padrao de "posse por FamilyId" no resto do codigo). 404.
// - ConflictException: o recurso existe, mas a acao pedida conflita com o estado atual
//   dele (ja existe, ja foi revisado, etc). 409.
// - ValidationException: a entrada do usuario nao passa nas regras de negocio (tipo
//   invalido, campo obrigatorio faltando, fora do intervalo permitido). 400.
// UnauthorizedAccessException (built-in do .NET) continua sendo o sinal de "sem
// permissao pra esta acao" -- ja era usado de forma consistente antes desta mudanca,
// nao precisou de tipo novo.

public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}

public class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}

public class ValidationException : Exception
{
    public ValidationException(string message) : base(message) { }
}
