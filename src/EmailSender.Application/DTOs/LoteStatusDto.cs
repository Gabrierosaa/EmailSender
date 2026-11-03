namespace EmailSender.Application.DTOs
{
    public sealed record LoteStatusDto(
        Guid Id,
        int TotalDestinatarios,
        int Pendentes,
        int Enviados,
        int Falhos,
        string Status,
        DateTimeOffset CriadoEm);
}
