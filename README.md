# Laçoos API

Backend do Laçoos, sistema para casais e organizadores planejarem um casamento. É um monolito modular em ASP.NET Core (.NET 10), com Entity Framework Core e PostgreSQL.

## Como executar

Pré-requisito: Docker com Compose.

```bash
cp .env.example .env
docker compose up --build
```

| Serviço | Endereço |
|---|---|
| API | http://localhost:8080 |
| Health check | http://localhost:8080/health |
| Swagger UI (só em Development) | http://localhost:8080/swagger |
| OpenAPI (só em Development) | http://localhost:8080/openapi/v1.json |
| E-mails de desenvolvimento (Mailpit) | http://localhost:8025 |

Em desenvolvimento, as migrations de todos os módulos são aplicadas ao iniciar e os dados de exemplo são carregados. Todos os usuários abaixo usam a senha de `SEED_DEMO_PASSWORD`:

| Usuário | Papel |
|---|---|
| `milena@lacoos.local` | owner |
| `organizadora@lacoos.local` | organizer |
| `visitante@lacoos.local` | viewer |

### Sem Docker para a API

Suba só o banco e o Mailpit e rode a API na máquina:

```bash
docker compose up db mailpit -d
export ConnectionStrings__Postgres="Host=localhost;Database=lacoos;Username=lacoos;Password=<sua senha do .env>"
dotnet run --project src/Host/Lacoos.Api
```

## Testes

```bash
dotnet test --solution LacoosApi.sln
```

- `tests/Lacoos.ArchitectureTests`: falham quando um módulo acessa o interior de outro.
- `tests/Lacoos.UnitTests`: regras puras de parcelas, status e quantidades de presentes.
- `tests/Lacoos.IntegrationTests`: API completa contra PostgreSQL real, via Testcontainers. Precisa do Docker ligado.

## Estrutura

```
src/Host/Lacoos.Api                     host único: registra os módulos e inicia a API
src/BuildingBlocks/Lacoos.SharedKernel  erros, relógio, isolamento por casamento, base do DbContext
src/Modules/<Modulo>/Lacoos.Modules.<Modulo>            domínio, casos de uso, persistência e endpoints (internal)
src/Modules/<Modulo>/Lacoos.Modules.<Modulo>.Contracts  interfaces e DTOs públicos
tests/
```

Regras das fronteiras:

- Um módulo só referencia o projeto `.Contracts` de outro módulo. A classe `<Modulo>Module` é o único tipo público.
- Cada módulo com dados tem seu DbContext, seu schema no PostgreSQL e suas migrations.
- Entre módulos só há IDs: sem navegação do EF Core e sem chave estrangeira entre schemas.
- O Dashboard não tem tabelas; compõe os dados pelos contratos.

## Migrations

Cada módulo tem as suas. Para criar uma nova, troque `Budget` pelo módulo desejado:

```bash
dotnet tool restore
dotnet ef migrations add NomeDaMigration \
  --project src/Modules/Budget/Lacoos.Modules.Budget \
  --startup-project src/Host/Lacoos.Api \
  --context BudgetDbContext \
  --output-dir Persistence/Migrations
```

Módulos com migrations: `Identity`, `Weddings`, `Checklist`, `Budget`, `Guests`, `Suppliers`, `Payments`, `Gifts`. O comando não precisa de banco ligado.

### Em produção

As migrations não rodam ao iniciar: `Database:MigrateOnStartup` é `false` fora de Development. Rode o passo explícito antes de publicar a nova versão:

```bash
docker compose --profile migrate run --rm migrate
```

Em produção, defina também `ASPNETCORE_ENVIRONMENT=Production`, `SEED_ENABLED=false` e o SMTP do provedor escolhido. `Auth:SecureCookies` já vem `true`, o que exige HTTPS.

## Integração com o frontend

Rotas de negócio ficam em `/api/weddings/{weddingId}/...`. Depois do login, `GET /api/weddings` lista os casamentos do usuário. A documentação completa dos endpoints está no Swagger UI, que também permite testá-los: faça o login em `POST /api/auth/login` e as demais chamadas usam a sessão e o token CSRF automaticamente.

**Autenticação.** O login grava dois cookies:

- `lacoos_session`: HttpOnly, invisível ao JavaScript. Toda requisição precisa enviá-lo (`withCredentials: true` se a API estiver em outra origem).
- `XSRF-TOKEN`: legível. Requisições de escrita devem devolver o valor no cabeçalho `X-XSRF-TOKEN`.

O `withXsrfConfiguration` do Angular usa esses nomes por padrão, mas só envia o cabeçalho em URLs relativas. Por isso, chame a API por um caminho relativo (como `/api`, com proxy) ou adicione um interceptor para URLs absolutas.

Frontend e API precisam ficar no mesmo domínio-base, porque os cookies usam `SameSite=Lax`. Rotas privadas não podem ser pré-renderizadas no SSR; use `RenderMode.Client` nelas.

**Erros.** Sempre em `application/problem+json`, com a mensagem em português em `detail`.

| Status | Significado |
|---|---|
| 400 | Campos inválidos, detalhados em `errors` |
| 401 | Não autenticado |
| 403 | Papel insuficiente ou token CSRF ausente |
| 404 | Não encontrado, inclusive casamento do qual a pessoa não é membro |
| 409 | Registro em uso ou duplicado |
| 422 | Regra de negócio violada |

**Links dos e-mails.** A API envia links para `{FRONTEND_BASE_URL}/redefinir-senha?token=...` e `{FRONTEND_BASE_URL}/convite?token=...`. O frontend precisa dessas duas rotas.

## Configuração

Tudo vem de variáveis de ambiente, com `.env.example` como referência. Arquivos enviados ficam no volume `lacoos-uploads` (PDF, JPG ou PNG, até 10 MB); para usar S3, basta outra implementação de `IFileStorage`.
