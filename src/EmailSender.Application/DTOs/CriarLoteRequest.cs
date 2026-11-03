namespace EmailSender.Application.DTOs
{
    public sealed record DestinatarioLoteRequest(
        string Email,
        string? Nome = null,
        IReadOnlyDictionary<string, string>? Variaveis = null);

    public sealed record CriarLoteRequest(
        string Assunto,
        string RemetenteNome,
        string? ReplyTo = null,
        string? CorpoHtml = null,
        string? CorpoTexto = null,
        IReadOnlyList<DestinatarioLoteRequest>? Destinatarios = null);
}
