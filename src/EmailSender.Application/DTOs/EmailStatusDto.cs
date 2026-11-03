namespace EmailSender.Application.DTOs
{
    public sealed record EmailStatusDto(
        Guid Id,
        string Status,
        DateTimeOffset? UltimaTentativaEm,
        string? UltimoErro);
}
