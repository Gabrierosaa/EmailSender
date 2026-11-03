# EmailSenderApi

API para envio de e-mails em lote, de forma assíncrona e confiável. Recebe uma lista de destinatários, grava tudo no banco e envia **um e-mail individual por pessoa** em segundo plano, com retentativas e rastreio de status.

> **Status:** em desenvolvimento (fase de planejamento/implementação inicial). Projeto pessoal de aprendizado e portfólio.

**Stack:** .NET (C#, ASP.NET Core) · RabbitMQ · SQL Server · Docker

---

## Sumário

- [Problema e objetivo](#problema-e-objetivo)
- [Escopo da versão 1](#escopo-da-versão-1)
- [Arquitetura](#arquitetura)
- [Fluxo de mensageria](#fluxo-de-mensageria)
- [Modelo de dados](#modelo-de-dados)
- [Endpoints](#endpoints)
- [Estrutura do projeto](#estrutura-do-projeto)
- [Segurança](#segurança)
- [Observabilidade](#observabilidade)
- [Testes](#testes)
- [Ambientes e execução](#ambientes-e-execução)
- [Configuração SMTP](#configuração-smtp)
- [Roadmap](#roadmap)
- [Riscos e decisões em aberto](#riscos-e-decisões-em-aberto)

---

## Problema e objetivo

Enviar o mesmo conteúdo para muitas pessoas dentro da requisição HTTP é frágil: a chamada demora, uma falha no meio deixa o lote pela metade e ninguém sabe quem recebeu. Além disso, cada destinatário deve receber um e-mail individual, sem ver os demais.

**Objetivos**

- **Técnico:** aceitar lotes de até **1.000 destinatários**, responder rápido e processar em segundo plano.
- **Confiabilidade:** não perder e-mails aceitos, tentar de novo em falhas temporárias e isolar o que falhou de vez.
- **Transparência:** consultar o status do lote e de cada e-mail.
- **Aprendizado:** praticar RabbitMQ, mensageria, Clean Architecture e DDD tático.

## Escopo da versão 1

**Dentro do escopo**

- API REST para criar lotes e consultar status (por lote e por e-mail).
- Worker que consome a fila do RabbitMQ e envia via SMTP.
- Persistência em SQL Server com histórico de tentativas.
- Templates simples com variáveis por destinatário (`{{nome}}`), enviados na própria requisição.
- Retry (3 tentativas), dead letter queue e idempotência.
- Dois ambientes: dev (local, com SMTP de teste) e prod (provedor real).

**Fora do escopo**

- Anexos e CRUD de templates.
- Rastreio de abertura/clique e o estado "Entregue" (exige webhooks do provedor).
- Agendamento, multi-tenant, descadastro automático e tratamento de bounces.
- Interface web (um painel em Angular é uma evolução natural).

## Arquitetura

Estilo **API + fila + worker**. A API valida, grava no SQL Server e publica uma mensagem por e-mail no RabbitMQ; ela **nunca** fala com o provedor de e-mail. Um worker separado consome as mensagens e faz o envio. Assim a resposta ao cliente é rápida e uma falha no provedor não derruba a API.

```
Cliente ──HTTPS + API Key──▶ API ──grava──▶ SQL Server
                              │
                              └──publica──▶ RabbitMQ ──▶ Worker ──SMTP/TLS──▶ Provedor ──▶ Destinatário
                                                           │
                                                           └──atualiza status──▶ SQL Server
```

**Sequência do envio em lote**

1. `POST /api/v1/lotes`
2. A API autentica e valida.
3. Grava o lote e os e-mails como `Pendente`.
4. Publica 1 mensagem por e-mail (com publisher confirms).
5. Marca os e-mails como `Enfileirado`.
6. Responde `202 Accepted` com o `loteId`.
7. O worker recebe a mensagem (`emailId`).
8. Carrega o e-mail; se já estiver `Enviado`, ignora.
9. Envia o e-mail.
10. Grava `Enviado` + tentativa.
11. Dá ack na mensagem.

**Principais decisões**

| # | Decisão | Motivo |
|---|---------|--------|
| D01 | API + fila + worker com RabbitMQ | Desacopla receber de enviar, permite retry e escala o worker de forma independente |
| D02 | Uma mensagem por e-mail, não por lote | Retry individual e isolamento: um endereço ruim não atrapalha os outros 999 |
| D03 | Mensagem enxuta (só identificadores) | O banco é a fonte da verdade; dados pessoais não circulam pela fila |
| D04 | Clean Architecture enxuta + DDD tático | Domínio pequeno, mas com regras de estado que merecem encapsulamento |
| D05 | `RabbitMQ.Client` direto, sem MassTransit/Wolverine | Aprender exchanges, filas, ack, retry e dead letter |
| D06 | Retry com filas de espera (TTL + dead-lettering) | Só recursos nativos do RabbitMQ, sobrevive a reinicializações |
| D07 | Reconciliação de `Pendente` em vez de Transactional Outbox | Cobre o mesmo risco com bem menos código |
| D08 | SQL Server + EF Core; conteúdo guardado no lote | Assunto e corpo se repetem em até 1.000 e-mails |
| D09 | Casos de uso como classes simples + `IEmailSender` (MailKit) | Menos "mágica"; Mailpit em dev e SES em prod só trocando configuração |
| D10 | API Key no cabeçalho, uma por ambiente | Há um único cliente consumidor |
| D11 | `IEmailSender.EnviarAsync`: SMTP 5xx devolve `ResultadoEnvio(false, "SMTP_<código>")`; falha transitória lança `FalhaTemporariaEnvioException` | Falha permanente é um resultado esperado e vira `Falhou` sem retry; falha transitória é uma exceção que o handler captura (sem depender de MailKit) para acionar o retry. Cancelamento (`OperationCanceledException`) propaga |

**Tecnologias**

| Tecnologia | Uso |
|------------|-----|
| .NET (LTS) / ASP.NET Core | API e worker |
| RabbitMQ (com management) | Mensageria |
| SQL Server + EF Core | Persistência |
| MailKit | Envio SMTP |
| Mailpit | SMTP de teste em dev |
| Amazon SES | Envio real em prod |
| FluentValidation | Validação |
| Serilog | Logs estruturados |
| OpenTelemetry | Métricas e traces |
| xUnit, NSubstitute, Testcontainers | Testes |
| k6 | Teste de carga |
| Docker / Docker Compose | Ambientes |

## Fluxo de mensageria

Um exchange `direct` roteia as mensagens para a fila principal. Falhas temporárias passam por filas de espera (retry) que devolvem a mensagem à fila principal após um TTL. Tentativas esgotadas vão para a dead letter queue.

| Recurso | Tipo | Função |
|---------|------|--------|
| `email.exchange` | Exchange direct, durável | Ponto único de entrada e retorno das mensagens |
| `email.send` | Fila durável | Fila principal consumida pelo worker |
| `email.retry.1` | Fila durável, TTL 15 s | Espera após a 1ª falha; ao expirar devolve ao `email.send` |
| `email.retry.2` | Fila durável, TTL 60 s | Espera após a 2ª falha |
| `email.dlq` | Fila durável | Destino final do que esgotou as tentativas ou chegou inválido |

Os TTLs ficam em configuração (valores sugeridos).

**Formato da mensagem** (JSON, persistente):

```json
{
  "messageId": "7b1e0c52-...",
  "emailId": "c94a31d8-...",
  "loteId": "3f2b9c1e-...",
  "correlationId": "3f2b9c1e-...",
  "tentativa": 1,
  "publicadoEm": "2026-10-02T14:30:00Z"
}
```

**Política de retentativa** (3 tentativas no total, configurável)

| Tentativa | Se falhar (transitória) | Espera | Estado do e-mail |
|-----------|-------------------------|--------|------------------|
| 1ª | Publica em `email.retry.1` | 15 s | Enfileirado (Tentativas = 1) |
| 2ª | Publica em `email.retry.2` | 60 s | Enfileirado (Tentativas = 2) |
| 3ª | Publica em `email.dlq` | — | Falhou (Tentativas = 3) |

**Classificação das falhas**

| Tipo | Exemplos | Ação |
|------|----------|------|
| Temporária | Timeout, conexão recusada, SMTP 4xx, rate limit do provedor | Retry conforme a tabela acima |
| Permanente | SMTP 5xx: caixa inexistente, endereço rejeitado | Marca `Falhou` na hora, registra a tentativa e dá ack |
| Mensagem inválida | JSON malformado, `emailId` inexistente | Vai direto para `email.dlq`, com log de erro |
| Banco indisponível | Não foi possível ler/gravar o status | Tratada como temporária |

**Idempotência (3 camadas)**

- **API:** cabeçalho opcional `Idempotency-Key` com índice único. Mesma chave + mesmo conteúdo devolve o lote original; conteúdo diferente retorna `409`.
- **Worker:** se o e-mail já está `Enviado`, só dá ack e ignora.
- **Banco:** coluna `RowVersion` (concorrência otimista).

> **Limitação conhecida:** se o worker cair depois de o provedor aceitar o e-mail e antes de gravar `Enviado`, a mensagem será reprocessada e o destinatário poderá receber duas cópias. A mitigação é usar um `Message-ID` SMTP determinístico (derivado do `emailId`).

**Publicação confiável e reconciliação:** a API publica com publisher confirms e só depois marca como `Enfileirado`. Se a publicação falhar, o e-mail permanece `Pendente`; um serviço de reconciliação no worker republica periodicamente os `Pendente` mais antigos que um limite (sugestão: 2 minutos).

**Consumo:** ack manual (só após gravar o resultado), prefetch configurável (sugestão: 10) e controle de taxa para respeitar o limite do provedor.

## Modelo de dados

Três tabelas: `LoteEnvio` (1) → (N) `Email` (1) → (N) `TentativaEnvio`.

| Tabela | Campos principais |
|--------|-------------------|
| `LoteEnvio` | `Id`, `IdempotencyKey?`, `RequestHash?`, `Assunto`, `RemetenteNome`, `ReplyTo?`, `CorpoHtml`, `CorpoTexto?`, `TotalDestinatarios`, `CriadoEm` |
| `Email` | `Id`, `LoteId`, `Destinatario`, `NomeDestinatario?`, `VariaveisJson?`, `Status`, `Tentativas`, `UltimoErro?`, `CriadoEm`, `EnviadoEm?`, `RowVersion` |
| `TentativaEnvio` | `Id`, `EmailId`, `Numero`, `IniciadaEm`, `Sucesso`, `CodigoErro?`, `Mensagem?` |

**Índices e regras:** `IX_Email_LoteId_Status`, `IX_Email_Status_CriadoEm` (reconciliação), `UQ_Email_LoteId_Destinatario` (sem duplicados no lote) e `UQ_Lote_IdempotencyKey` (filtrado, só quando não nulo).

**Estados de um e-mail**

| De | Para | Gatilho |
|----|------|---------|
| (novo) | Pendente | Lote criado e e-mails gravados |
| Pendente | Enfileirado | Broker confirmou a publicação (ou a reconciliação republicou) |
| Enfileirado | Enfileirado | Falha temporária: incrementa `Tentativas` e aguarda a fila de retry |
| Enfileirado | Enviado | Provedor SMTP aceitou a mensagem |
| Enfileirado | Falhou | Falha permanente ou 3ª tentativa sem sucesso |
| Falhou | Enfileirado | Reenfileiramento manual (opcional) |

"Enviado" significa que o provedor **aceitou** a mensagem; não garante chegada à caixa de entrada. O status do **lote** é calculado: *Processando* (há `Pendente`/`Enfileirado`), *Concluído* (todos `Enviado`) ou *Concluído com falhas*.

**Modelagem no domínio:** `Email` (entidade com comportamento, sem setters públicos de `Status`), `EnderecoEmail` (value object imutável) e `LoteEnvio` (valida o limite de 1.000 e duplicidades e cria os e-mails).

## Endpoints

Rotas relativas a `/api/v1` (exceto health checks). Todos os endpoints de negócio exigem o cabeçalho `X-Api-Key` e retornam erros no formato `ProblemDetails` (RFC 9457).

| Método | Rota | Descrição | Respostas |
|--------|------|-----------|-----------|
| `POST` | `/lotes` | Cria um lote e enfileira os e-mails (aceita `Idempotency-Key`) | 202, 400, 401, 409, 413, 429, 503 |
| `GET` | `/lotes/{loteId}` | Resumo do lote com contagem por estado | 200, 401, 404, 429 |
| `GET` | `/lotes/{loteId}/emails` | Lista paginada (`status`, `pagina`, `tamanho` máx. 100) | 200, 400, 401, 404, 429 |
| `GET` | `/emails/{id}/status` | Status de um e-mail, data da última tentativa e código do último erro | 200, 404 |
| `POST` | `/emails/{emailId}/reenfileirar` | Reenfileira um e-mail em `Falhou` (opcional) | 202, 401, 404, 409 |
| `GET` | `/health/live` · `/health/ready` | Liveness e readiness (banco e RabbitMQ), sem autenticação | 200, 503 |

**Exemplo — criar lote**

```http
POST /api/v1/lotes
X-Api-Key: <sua-chave>
Idempotency-Key: <opcional>
Content-Type: application/json

{
  "remetenteNome": "Gabriel Rosa",
  "replyTo": "contato@example.com",
  "assunto": "Olá, {{nome}}!",
  "corpoHtml": "<p>Oi {{nome}}, seu código é {{codigo}}.</p>",
  "corpoTexto": "Oi {{nome}}, seu código é {{codigo}}.",
  "destinatarios": [
    { "email": "ana@example.com", "nome": "Ana", "variaveis": { "codigo": "A123" } },
    { "email": "bruno@example.com", "nome": "Bruno", "variaveis": { "codigo": "B456" } }
  ]
}
```

Resposta `202 Accepted`:

```json
{ "loteId": "3f2b9c1e-...", "totalDestinatarios": 2, "status": "Processando" }
```

Erro de validação `400 Bad Request` (ProblemDetails):

```json
{
  "title": "Erro de validação",
  "status": 400,
  "errors": { "destinatarios[1].email": ["Endereço inválido."] }
}
```

## Estrutura do projeto

```
EmailSender/
├── src/
│   ├── EmailSender.Domain/          entidades, value objects, enums, regras
│   ├── EmailSender.Application/     casos de uso, DTOs, validadores, portas
│   ├── EmailSender.Infrastructure/  EF Core, RabbitMQ, MailKit, templates
│   ├── EmailSender.Api/             endpoints, auth, rate limit, health
│   └── EmailSender.Worker/          consumidor, reconciliação
├── tests/
│   ├── EmailSender.Domain.Tests/
│   ├── EmailSender.Application.Tests/
│   └── EmailSender.IntegrationTests/
├── docker/                          Dockerfiles e arquivos de compose
├── docs/                            documentação e diagramas
└── README.md
```

As dependências apontam para dentro e o **Domain não depende de nada**:

| Projeto | Depende de |
|---------|------------|
| Domain | — |
| Application | Domain |
| Infrastructure | Application, Domain |
| Api | Application, Infrastructure (composition root) |
| Worker | Application, Infrastructure (composition root) |

## Segurança

- **API Key** no cabeçalho `X-Api-Key`: segredo aleatório (mínimo 32 bytes), fora do código, comparado em tempo constante; idealmente só o hash é armazenado. Uma chave por ambiente na v1.
- **Validação de entrada:** formato dos e-mails, limite de 1.000 destinatários, tamanhos máximos e rejeição de quebras de linha em assunto, nome e Reply-To (injeção de cabeçalhos).
- **Rate limit** por API Key (geral e mais baixo para criação de lotes), configurável.
- **Remetente fixo e verificado** por ambiente, com SPF, DKIM e DMARC; evita uso para falsificar identidades.
- **Segredos:** variáveis de ambiente/Docker secrets em prod, user-secrets em dev; `.env` fora do Git.
- **Transporte:** HTTPS na borda (proxy reverso) e STARTTLS/TLS no SMTP.
- **Dados pessoais:** endereços mascarados nos logs (`a***@dominio.com`), nada de dados pessoais na fila e retenção planejada (sugestão: 90 dias, evolução). Sem tratamento especial de LGPD na v1.
- **Conteúdo HTML** vem do cliente autenticado e é enviado como está; o risco principal é abuso, não XSS — por isso o tamanho é limitado.

## Observabilidade

- **Logs:** Serilog em JSON no console. Campos fixos: `loteId`, `emailId`, `tentativa`, `correlationId` (a API aceita e devolve `X-Correlation-Id`). Nunca registra corpo de e-mail, API Key ou credenciais.
- **Métricas** (OpenTelemetry): `emails_enviados_total`, `emails_falhos_total`, `envio_duracao_segundos`, `lotes_criados_total` e tamanho das filas `email.send`/`email.dlq`.
- **Health checks:** `/health/live` (processo responde) e `/health/ready` (SQL Server e RabbitMQ). O worker não expõe HTTP na v1.

**Tratamento de falhas**

| Cenário | Comportamento |
|---------|---------------|
| Provedor SMTP fora do ar | Retry em 15 s e 60 s; se persistir, DLQ e `Falhou` |
| Endereço inexistente (SMTP 5xx) | `Falhou` imediatamente, sem retry |
| RabbitMQ indisponível na criação do lote | Lote gravado, e-mails ficam `Pendente` e a reconciliação republica depois |
| Worker reiniciado no meio do envio | Mensagem sem ack volta à fila (possível duplicidade rara) |
| SQL Server indisponível | API responde `503`; o worker trata como falha temporária |
| Mensagem malformada | Vai direto para `email.dlq` com log de erro |

## Testes

| Nível | O que testar | Ferramentas |
|-------|--------------|-------------|
| Unitário | Transições de estado do `Email`, validação de `EnderecoEmail`, limites do `LoteEnvio`, templates, backoff e classificação de erros SMTP | xUnit, NSubstitute |
| Aplicação | Casos de uso com repositórios/portas falsos (sucesso, falha temporária, permanente, já `Enviado`) | xUnit, NSubstitute |
| Integração | SQL Server e RabbitMQ reais, fluxo completo API → fila → worker → Mailpit, retry e DLQ | xUnit, Testcontainers, WebApplicationFactory |
| Carga | Lotes de 1.000 destinatários: tempo de aceitação e até todos ficarem `Enviado` | k6 |

Para rodar os testes:

```bash
dotnet test
```

## Ambientes e execução

Dois ambientes via Docker Compose. A mesma imagem serve aos dois; muda só a configuração.

**Dev (local)**

| Serviço | Portas |
|---------|--------|
| API | 8080 (Swagger habilitado) |
| Worker | — (pode ter várias réplicas) |
| SQL Server | 1433 (reserve ao menos 2 GB de memória) |
| RabbitMQ | 5672 e 15672 (painel de management) |
| Mailpit | 1025 (SMTP) e 8025 (web) |

```bash
docker compose up
```

> Os arquivos de Compose/Dockerfiles ainda serão adicionados na pasta `docker/` (Fase 0 do roadmap).

**Dev × Prod**

| Aspecto | Dev | Prod |
|---------|-----|------|
| Provedor SMTP | Mailpit | Amazon SES (ou outro) com domínio verificado |
| From | Endereço de teste | Endereço do domínio verificado |
| Segredos | user-secrets e `.env` local fora do Git | Variáveis de ambiente ou Docker secrets |
| Banco | Dados descartáveis | Volume persistente e backup agendado |
| HTTPS | Não necessário | Proxy reverso com certificado (Caddy/Nginx + Let's Encrypt) |
| Portas | Todas expostas | Somente 80 e 443 |
| Logs | Legíveis no console | JSON, com rotação |

**Sugestões para produção:** VPS com Docker Compose (2 GB+ de RAM), GitHub Actions para build/test/imagens, imagens com versão fixa (nunca `latest`) e backup do volume do SQL Server.

## Configuração SMTP

O envio usa a seção `Smtp`, lida por API e Worker (ambos chamam `AddInfrastructure`). Ela é validada na inicialização: sem `Host`, `Porta` válida e `RemetenteEndereco`, a aplicação não sobe.

| Chave | Obrigatória | Descrição |
|-------|-------------|-----------|
| `Smtp:Host` | Sim | Servidor SMTP |
| `Smtp:Porta` | Sim | 1 a 65535 |
| `Smtp:RemetenteEndereco` | Sim | Endereço do `From` (o nome vem do lote) |
| `Smtp:Usuario` | Não | Se vazio, não autentica |
| `Smtp:Senha` | Não | Segredo: nunca no Git |
| `Smtp:Seguranca` | Não | `SecureSocketOptions` do MailKit; padrão `StartTls` (`None`, `SslOnConnect`, `StartTlsWhenAvailable`, `Auto`) |
| `Smtp:TimeoutSegundos` | Não | 1 a 300; padrão 30 |

**Dev com Mailpit** (`appsettings.Development.json`, só valores não sensíveis; mensagens em http://localhost:8025):

```json
"Smtp": {
  "Host": "localhost",
  "Porta": 1025,
  "RemetenteEndereco": "teste@localhost",
  "Seguranca": "None"
}
```

**Credenciais reais em dev** (user-secrets; os dois projetos já têm `UserSecretsId`):

```bash
dotnet user-secrets set "Smtp:Usuario" "seu-usuario" --project src/EmailSender.Worker
dotnet user-secrets set "Smtp:Senha" "sua-senha" --project src/EmailSender.Worker
```

**Prod:** variáveis de ambiente, como `Smtp__Host`, `Smtp__Porta` e `Smtp__Senha`.

## Roadmap

| Fase | Objetivo | Pronto quando |
|------|----------|---------------|
| 0 | Preparação (5 projetos, Git, Compose) | `docker compose up` sobe tudo |
| 1 | Domínio | Testes do Domain passando |
| 2 | Persistência | Lote e e-mails gravados e lidos do SQL Server |
| 3 | API de lotes | Lote criado e mensagens visíveis na fila |
| 4 | Worker e envio | E-mails aparecem no Mailpit e ficam `Enviado` |
| 5 | Resiliência | Falhas simuladas seguem o fluxo de retry/DLQ |
| 6 | Consulta de status | Status refletem o que o worker fez |
| 7 | Segurança | Requisições sem chave retornam 401 |
| 8 | Observabilidade | Dá para rastrear um e-mail pelos logs |
| 9 | Testes de integração e carga | Pipeline de CI verde e resultados registrados |
| 10 | Produção | E-mail real enviado pelo ambiente de prod |

**Evoluções futuras:** anexos e CRUD de templates · estado "Entregue" via webhooks · Transactional Outbox · descadastro e retenção automática · tabela de API Keys, JWT e multi-tenant · painel em Angular.

## Riscos e decisões em aberto

**Riscos**

| ID | Risco | Mitigação |
|----|-------|-----------|
| R01 | Conta nova do provedor com restrições | Verificar regras cedo e usar Mailpit até liberar |
| R02 | Reputação do domínio ruim | SPF/DKIM/DMARC e só enviar a quem aceita |
| R03 | Duplicidade de envio em falha no meio do processo | Idempotência em camadas e `Message-ID` determinístico (risco aceito) |
| R04 | Mensagens perdidas entre banco e fila | Publisher confirms e reconciliação |
| R05 | Inserção de 1.000 e-mails lenta | Inserção em lote e índices mínimos; medir no teste de carga |
| R06 | SQL Server consome muita memória na VPS | Dimensionar a VPS e limitar memória do contêiner |
| R07 | DLQ acumulando sem ninguém olhar | Alerta e rotina de reprocessamento (evolução) |
| R08 | Escopo crescer além do necessário | Seguir as fases e manter evoluções fora da v1 |

**Decisões em aberto:** provedor definitivo de prod (A01), domínio próprio para o From (A02), TTLs do retry em prod (A03), Controllers ou Minimal APIs (A04), tipo das filas — clássicas ou quorum (A05) e política de retenção de dados (A06).

---

**Autor:** Gabriel Rosa
