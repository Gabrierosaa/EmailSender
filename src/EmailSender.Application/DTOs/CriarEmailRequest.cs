namespace EmailSender.Application.DTOs
{
    public sealed record CriarEmailRequest(
        string Destinatario,
        string Assunto,
        string RemetenteNome,
        string? NomeDestinatario = null,
        string? ReplyTo = null,
        string? CorpoHtml = null,
        string? CorpoTexto = null,
        IReadOnlyDictionary<string, string>? Variaveis = null);
}
