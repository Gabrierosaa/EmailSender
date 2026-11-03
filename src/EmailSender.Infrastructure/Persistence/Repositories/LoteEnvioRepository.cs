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

        public async Task<ResumoLote?> ObterResumoAsync(Guid id, CancellationToken ct)
        {
            var lote = await _context.Lotes
                .AsNoTracking()
                .Where(l => l.Id == id)
                .Select(l => new { l.Id, l.CriadoEm })
                .FirstOrDefaultAsync(ct);

            if (lote is null)
                return null;

            var contagens = await _context.Emails
                .AsNoTracking()
                .Where(e => e.LoteId == id)
                .GroupBy(e => e.Status)
                .Select(g => new { g.Key, Total = g.Count() })
                .ToListAsync(ct);

            return new ResumoLote(lote.Id, lote.CriadoEm, contagens.ToDictionary(c => c.Key, c => c.Total));
        }

        public Task<LoteEnvio?> ObterPorIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct)
        {
            return _context.Lotes
                .Include(l => l.Emails)
                .FirstOrDefaultAsync(l => l.IdempotencyKey == idempotencyKey, ct);
        }
    }
}
