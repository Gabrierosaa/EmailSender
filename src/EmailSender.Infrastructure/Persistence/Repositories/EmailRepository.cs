using EmailSender.Domain.Entities;
using EmailSender.Domain.Enum;
using EmailSender.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EmailSender.Infrastructure.Persistence.Repositories
{
    public sealed class EmailRepository : IEmailRepository
    {
        private readonly AppDbContext _context;

        public EmailRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task AdicionarAsync(Email email, CancellationToken ct)
        {
            await _context.Emails.AddAsync(email, ct);
        }

        public Task<Email?> ObterPorIdAsync(Guid id, CancellationToken ct)
        {
            return _context.Emails
                .Include(e => e.Tentativas)
                .FirstOrDefaultAsync(e => e.Id == id, ct);
        }

        public async Task<IReadOnlyList<Email>> ObterPorStatusAsync(EmailStatusEnum status, int limite, CancellationToken ct)
        {
            return await _context.Emails
                .Include(e => e.Tentativas)
                .Where(e => e.Status == status)
                .OrderBy(e => EF.Property<DateTimeOffset>(e, "CriadoEm"))
                .ThenBy(e => e.Id)
                .Take(limite)
                .ToListAsync(ct);
        }
    }
}
