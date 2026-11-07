namespace Lacoos.Api.Infrastructure;

internal static class SwaggerUiSetup
{
    /// <summary>Copia o cookie CSRF para o cabeçalho, como o Angular faz, para as escritas funcionarem na tela.</summary>
    private const string CsrfInterceptor = """
        (request) => {
          const match = document.cookie.match(/(?:^|; )XSRF-TOKEN=([^;]*)/);
          if (match) {
            request.headers['X-XSRF-TOKEN'] = decodeURIComponent(match[1]);
          }
          return request;
        }
        """;

    public static void UseLacoosSwaggerUi(this WebApplication app) =>
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/openapi/v1.json", "Laçoos API");
            options.RoutePrefix = "swagger";
            options.DocumentTitle = "Laçoos API";
            options.UseRequestInterceptor(CsrfInterceptor);
        });
}
