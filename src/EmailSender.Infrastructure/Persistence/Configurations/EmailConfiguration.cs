using EmailSender.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmailSender.Infrastructure.Persistence.Configurations
{
    internal sealed class EmailConfiguration : IEntityTypeConfiguration<Email>
    {
        public void Configure(EntityTypeBuilder<Email> builder)
        {
            builder.ToTable("Emails");

            builder.HasKey(e => e.Id);
            builder.Property(e => e.Id).ValueGeneratedNever();

            builder.Property(e => e.LoteId).IsRequired();

            builder.Property(e => e.Destinatario)
                .IsRequired()
                .HasMaxLength(320);

            builder.Property(e => e.NomeDestinatario)
                .HasMaxLength(200);

            builder.Property(e => e.VariavelJson)
                .HasColumnType("nvarchar(max)");

            builder.Property(e => e.Status)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(20);

            // Quando o e-mail reagendado volta a ser elegível para envio (retry); nulo = imediatamente.
            builder.Property(e => e.ProximaTentativaEm);

            // Token de concorrência: se dois workers tentarem mudar o mesmo email (ex.: Pendente -> Enfileirado),
            // o segundo SaveChanges lança DbUpdateConcurrencyException e não deve enviar.
            builder.Property<byte[]>("RowVersion").IsRowVersion();

            // Data de criação (shadow) usada para ordenar a fila de envio em FIFO.
            builder.Property<DateTimeOffset>("CriadoEm").HasDefaultValueSql("SYSDATETIMEOFFSET()");

            builder.HasMany(e => e.Tentativas)
                .WithOne()
                .HasForeignKey(t => t.EmailId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(e => e.Tentativas)
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            // Consulta de status do lote (contagem por estado) e listagem filtrada.
            builder.HasIndex(e => new { e.LoteId, e.Status })
                .HasDatabaseName("IX_Email_LoteId_Status");

            // Reconciliação: achar e-mails Pendentes antigos e ordenar a fila em FIFO.
            builder.HasIndex("Status", "CriadoEm")
                .HasDatabaseName("IX_Email_Status_CriadoEm");

            // Impede o mesmo endereço duas vezes no mesmo lote.
            builder.HasIndex(e => new { e.LoteId, e.Destinatario })
                .IsUnique()
                .HasDatabaseName("UQ_Email_LoteId_Destinatario");
        }
    }
}
