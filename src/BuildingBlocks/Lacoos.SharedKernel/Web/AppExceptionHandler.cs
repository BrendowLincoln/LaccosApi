using Lacoos.SharedKernel.Errors;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Lacoos.SharedKernel.Web;

/// <summary>Converte erros em ProblemDetails com mensagens em português.</summary>
public sealed class AppExceptionHandler(ILogger<AppExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        var (status, detail) = exception switch
        {
            AppException app => (app.StatusCode, app.Message),
            BadHttpRequestException bad when bad.StatusCode == StatusCodes.Status413PayloadTooLarge =>
                (413, "O arquivo enviado é maior que o limite permitido."),
            BadHttpRequestException => (400, "O corpo da requisição é inválido. Verifique os campos e os formatos enviados."),
            DbUpdateConcurrencyException => (409, "O registro foi alterado por outra pessoa. Atualize e tente novamente."),
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } } =>
                (409, "Já existe um registro com estes dados."),
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation } } =>
                (409, "O registro está em uso por outros dados."),
            _ => (500, "Ocorreu um erro inesperado. Tente novamente."),
        };

        if (status >= 500)
        {
            logger.LogError(exception, "Erro não tratado em {Method} {Path}", http.Request.Method, http.Request.Path);
        }

        var problem = exception is ValidationException validation
            ? new ValidationProblemDetails(validation.Errors.ToDictionary(e => e.Key, e => e.Value))
            : new ProblemDetails();

        problem.Status = status;
        problem.Title = Problems.TitleFor(status);
        problem.Detail = detail;

        http.Response.StatusCode = status;
        await http.Response.WriteAsJsonAsync(problem, problem.GetType(), options: null, "application/problem+json", ct);
        return true;
    }
}
