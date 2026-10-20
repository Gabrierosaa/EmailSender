# EmailSender - ambientes Docker

## O que existe

### DEV
- API ASP.NET Core
- Worker .NET
- SQL Server Developer
- RabbitMQ com Management
- Mailpit
- Portas abertas para depuração

### PROD
- API ASP.NET Core
- Worker .NET
- SQL Server Express com volume persistente
- RabbitMQ
- Amazon SES externo via SMTP/TLS
- Caddy como reverse proxy HTTPS
- Somente 80/443 expostas no host
- Logs do Docker com rotação

> MailKit não é um container: ele deve ser instalado no projeto Infrastructure como pacote NuGet.
> SSMS também não é container do stack: use o SSMS no Windows para conectar ao SQL Server.

## 1. DEV

Na raiz do projeto:

```powershell
Copy-Item docker/.env.dev.example docker/.env.dev

docker compose `
  --env-file docker/.env.dev `
  -f docker/compose.dev.yml `
  up -d --build
```

Acessos:
- API: http://localhost:8080
- RabbitMQ Management: http://localhost:15672
- Mailpit: http://localhost:8025
- SQL Server/SSMS: localhost,1433

Parar:

```powershell
docker compose --env-file docker/.env.dev -f docker/compose.dev.yml down
```

Parar e apagar dados locais:

```powershell
docker compose --env-file docker/.env.dev -f docker/compose.dev.yml down -v
```

## 2. PROD

No servidor:

```bash
cp docker/.env.prod.example docker/.env.prod
```

Edite `docker/.env.prod` e troque TODOS os `CHANGE_ME`, domínio, região e remetente.

Depois:

```bash
docker compose \
  --env-file docker/.env.prod \
  -f docker/compose.prod.yml \
  up -d --build
```

A API não publica a porta 8080 no host. O Caddy recebe 80/443 e encaminha internamente para `api:8080`.

## Banco

A connection string usa:
- DEV: `EmailSender_Dev`
- PROD: `EmailSender_Prod`

Depois de criar suas migrations, rode a migration correspondente. Exemplo local:

```powershell
dotnet ef database update `
  --project src/EmailSender.Infrastructure `
  --startup-project src/EmailSender.Api
```

Se o seu `DbContext` ainda não estiver preparado para criar o banco pela migration, crie o banco manualmente no SSMS antes de subir a aplicação.

## Atenção aos nomes das configurações

Os nomes:
- `ConnectionStrings__DefaultConnection`
- `RabbitMq__*`
- `Smtp__*`
- `Security__ApiKey`
- `Worker__*`

precisam bater com os nomes usados em `appsettings` / `IOptions<T>` no seu código. Se suas classes de configuração tiverem outros nomes, ajuste as chaves no Compose.
