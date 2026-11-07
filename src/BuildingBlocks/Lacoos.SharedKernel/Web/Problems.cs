using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Lacoos.SharedKernel.Web;

public static class Problems
{
    public static Task WriteAsync(HttpContext http, int status, string detail)
    {
        http.Response.StatusCode = status;
        return http.Response.WriteAsJsonAsync(
            new ProblemDetails { Status = status, Title = TitleFor(status), Detail = detail },
            options: null,
            contentType: "application/problem+json");
    }

    public static string TitleFor(int status) => status switch
    {
        400 => "Requisição inválida",
        401 => "Não autenticado",
        403 => "Acesso negado",
        404 => "Não encontrado",
        409 => "Conflito",
        413 => "Arquivo muito grande",
        422 => "Regra de negócio violada",
        429 => "Muitas requisições",
        _ => "Erro interno",
    };
}
