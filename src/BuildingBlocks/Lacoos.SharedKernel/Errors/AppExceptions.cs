namespace Lacoos.SharedKernel.Errors;

/// <summary>Erro esperado de negócio, convertido em ProblemDetails com mensagem em português.</summary>
public abstract class AppException(string message, int statusCode) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public sealed class NotFoundException(string message) : AppException(message, 404);

public sealed class ForbiddenException(string message) : AppException(message, 403);

public sealed class UnauthorizedException(string message) : AppException(message, 401);

/// <summary>A operação conflita com o estado atual, como excluir um registro em uso.</summary>
public sealed class ConflictException(string message) : AppException(message, 409);

/// <summary>A requisição é bem formada, mas viola uma regra de negócio.</summary>
public sealed class BusinessRuleException(string message) : AppException(message, 422);

public sealed class ValidationException(IReadOnlyDictionary<string, string[]> errors)
    : AppException("Um ou mais campos são inválidos.", 400)
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}
