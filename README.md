# EmailSender

API em .NET 10 para registrar e-mails que serão enviados. Nesta etapa (US-07) a API **só registra**: o e-mail é gravado
com status `Pendente` e o envio por SMTP/fila acontece em etapas posteriores.

## Estrutura

| Projeto | Responsabilidade |
|---|---|
| `EmailSender.Domain` | Entidades (`LoteEnvio`, `Email`), value object `EnderecoEmail`, interfaces de repositório |
| `EmailSender.Application` | Casos de uso (`RegistrarEmailHandler`), DTOs e validação (FluentValidation) |
| `EmailSender.Infrastructure` | EF Core, mapeamentos, migrations e repositórios (SQL Server) |
| `EmailSender.Api` | Endpoints HTTP, ProblemDetails e OpenAPI/Swagger |
| `EmailSender.Worker` | Processamento em segundo plano (ainda não usado por esta US) |

## Como rodar

Pré-requisitos: .NET 10 SDK e um SQL Server acessível (por exemplo em container Docker, porta 1433).

1. Configure a connection string sem colocá-la no repositório:

   ```bash
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost,1433;Database=EmailSender;User Id=sa;Password=<SENHA>;TrustServerCertificate=True" --project src/EmailSender.Api
   ```

2. Aplique as migrations:

   ```bash
   dotnet ef database update --project src/EmailSender.Infrastructure --startup-project src/EmailSender.Api
   ```

3. Suba a API:

   ```bash
   dotnet run --project src/EmailSender.Api --launch-profile http
   ```

Em Development, a documentação fica em `http://localhost:5024/swagger` (documento OpenAPI em `/openapi/v1.json`).
Também há um arquivo [`EmailSender.Api.http`](src/EmailSender.Api/EmailSender.Api.http) com requisições prontas.

## POST /api/v1/emails

Registra um e-mail para envio. Cria um lote unitário com 1 e-mail no status `Pendente`.

### Requisição

```http
POST /api/v1/emails
Content-Type: application/json

{
  "destinatario": "fulano@exemplo.com",
  "nomeDestinatario": "Fulano de Tal",
  "assunto": "Bem-vindo, {{nome}}",
  "remetenteNome": "Equipe EmailSender",
  "replyTo": "suporte@exemplo.com",
  "corpoTexto": "Olá, {{nome}}! Sua conta foi criada.",
  "corpoHtml": "<p>Olá, <b>{{nome}}</b>! Sua conta foi criada.</p>",
  "variaveis": { "nome": "Fulano" }
}
```

| Campo | Obrigatório | Regras |
|---|---|---|
| `destinatario` | sim | e-mail válido, até 254 caracteres, sem quebra de linha |
| `assunto` | sim | até 300 caracteres, sem quebra de linha |
| `remetenteNome` | sim | até 200 caracteres, sem quebra de linha |
| `corpoTexto` / `corpoHtml` | pelo menos um | texto livre |
| `nomeDestinatario` | não | até 200 caracteres, sem quebra de linha |
| `replyTo` | não | e-mail válido, sem quebra de linha |
| `variaveis` | não | pares chave/valor em texto |

O corpo da requisição tem limite de **1 MB**.

### Resposta `201 Created`

```http
HTTP/1.1 201 Created
Location: /api/v1/emails/3f8e2c1a-5b7d-4e9a-8c6f-1a2b3c4d5e6f
Content-Type: application/json

{ "id": "3f8e2c1a-5b7d-4e9a-8c6f-1a2b3c4d5e6f" }
```

### Resposta `400 Bad Request`

Erros de validação vêm em `application/problem+json`, agrupados por campo e com mensagens em português:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Um ou mais campos são inválidos.",
  "status": 400,
  "errors": {
    "destinatario": [ "O destinatário informado não é um e-mail válido." ],
    "corpo": [ "Informe o corpo do e-mail em HTML ou em texto." ]
  }
}
```

### Resposta `413 Payload Too Large`

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.14",
  "title": "O corpo da requisição é grande demais.",
  "status": 413,
  "detail": "O limite é de 1 MB."
}
```

Nos casos 400 e 413 nada é gravado no banco.

> O cabeçalho `Location` aponta para `GET /api/v1/emails/{id}`, que ainda não existe (consulta de status é outra história).

## Testes

```bash
dotnet test tests/EmailSender.Domain.Tests
dotnet test tests/EmailSender.Application.Tests
dotnet test tests/EmailSender.IntegrationTests
```

- **Domain** e **Application**: testes unitários, sem dependências externas.
- **IntegrationTests**: os testes de repositório e de endpoint usam [Testcontainers](https://dotnet.testcontainers.org/) e sobem um
  SQL Server em container sozinhos (**precisam do Docker em execução**).
