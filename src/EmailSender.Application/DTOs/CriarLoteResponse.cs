namespace EmailSender.Application.DTOs
{
    public sealed record CriarLoteResponse(Guid LoteId, int TotalDestinatarios, string Status);
}
