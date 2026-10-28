namespace EmailSender.Domain.Repositories
{
    public interface IUnitOfWork
    {
        Task<int> CommitAsync(CancellationToken ct);
    }
}
