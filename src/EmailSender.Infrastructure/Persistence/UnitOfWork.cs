using EmailSender.Domain.Exceptions;
using EmailSender.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EmailSender.Infrastructure.Persistence
{
    public sealed class UnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext _context;

        public UnitOfWork(AppDbContext context)
        {
            _context = context;
        }

        public async Task<int> CommitAsync(CancellationToken ct)
        {
            try
            {
                return await _context.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                throw new ConflitoConcorrenciaException(
                    "O registro foi alterado por outro processo.", ex);
            }
        }
    }
}
