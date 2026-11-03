using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace EmailSender.Api.OpenApi
{
    internal sealed class ExemplosCriarEmailTransformer : IOpenApiOperationTransformer
    {
        private const string Requisicao = """
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
            """;

        private const string Criado = """
            { "id": "3f8e2c1a-5b7d-4e9a-8c6f-1a2b3c4d5e6f" }
            """;

        private const string Invalido = """
            {
              "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
              "title": "Um ou mais campos são inválidos.",
              "status": 400,
              "errors": {
                "destinatario": [ "O destinatário informado não é um e-mail válido." ],
                "assunto": [ "O assunto não pode conter quebra de linha." ],
                "corpo": [ "Informe o corpo do e-mail em HTML ou em texto." ]
              },
              "traceId": "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-00"
            }
            """;

        private const string GrandeDemais = """
            {
              "type": "https://tools.ietf.org/html/rfc9110#section-15.5.14",
              "title": "O corpo da requisição é grande demais.",
              "status": 413,
              "detail": "O limite é de 1 MB.",
              "traceId": "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-00"
            }
            """;

        public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken ct)
        {
            if (context.Description.HttpMethod != "POST" || context.Description.RelativePath != "api/v1/emails")
                return Task.CompletedTask;

            operation.Summary = "Registra um e-mail para envio";
            operation.Description =
                "Cria um lote unitário com 1 e-mail no status Pendente. Esta chamada só registra: " +
                "o envio por SMTP acontece depois e não é feito aqui. O cabeçalho Location aponta para o recurso criado.";

            DefinirExemplo(operation.RequestBody?.Content, Requisicao);

            if (operation.Responses is null)
                return Task.CompletedTask;

            DefinirExemplo(operation.Responses.GetValueOrDefault("201")?.Content, Criado);
            DefinirExemplo(operation.Responses.GetValueOrDefault("400")?.Content, Invalido);
            DefinirExemplo(operation.Responses.GetValueOrDefault("413")?.Content, GrandeDemais);

            return Task.CompletedTask;
        }

        private static void DefinirExemplo(IDictionary<string, OpenApiMediaType>? conteudo, string json)
        {
            if (conteudo is null)
                return;

            foreach (var tipo in conteudo.Values)
                tipo.Example = JsonNode.Parse(json);
        }
    }
}
