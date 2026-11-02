using EmailSender.Domain.Repositories;

namespace EmailSender.Infrastructure.Persistence
{
    public sealed class UnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext _context;

        public UnitOfWork(AppDbContext context)
        {
            _context = context;
        }

        public Task<int> CommitAsync(CancellationToken ct)
        {
            return _context.SaveChangesAsync(ct);
        }
    }
}
