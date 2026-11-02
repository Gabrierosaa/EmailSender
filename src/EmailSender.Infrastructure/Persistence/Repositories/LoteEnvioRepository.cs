using EmailSender.Domain.Entities;
using EmailSender.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EmailSender.Infrastructure.Persistence.Repositories
{
    public sealed class LoteEnvioRepository : ILoteEnvioRepository
    {
        private readonly AppDbContext _context;

        public LoteEnvioRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task AdicionarAsync(LoteEnvio lote, CancellationToken ct)
        {
            await _context.Lotes.AddAsync(lote, ct);
        }

        public Task<LoteEnvio?> ObterPorIdAsync(Guid id, CancellationToken ct)
        {
            return _context.Lotes
                .Include(l => l.Emails)
                .FirstOrDefaultAsync(l => l.Id == id, ct);
        }

        public Task<LoteEnvio?> ObterPorIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct)
        {
            return _context.Lotes
                .Include(l => l.Emails)
                .FirstOrDefaultAsync(l => l.IdempotencyKey == idempotencyKey, ct);
        }
    }
}
