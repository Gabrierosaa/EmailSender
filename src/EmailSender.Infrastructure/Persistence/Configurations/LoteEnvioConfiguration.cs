using EmailSender.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmailSender.Infrastructure.Persistence.Configurations
{
    internal sealed class LoteEnvioConfiguration : IEntityTypeConfiguration<LoteEnvio>
    {
        public void Configure(EntityTypeBuilder<LoteEnvio> builder)
        {
            builder.ToTable("LotesEnvio");

            builder.HasKey(l => l.Id);
            builder.Property(l => l.Id).ValueGeneratedNever();

            builder.Property(l => l.IdempotencyKey).HasMaxLength(100);
            builder.Property(l => l.RequestHash).HasMaxLength(64);

            builder.Property(l => l.Assunto)
                .IsRequired()
                .HasMaxLength(LoteEnvio.AssuntoMaxLength);

            builder.Property(l => l.RemetenteNome)
                .IsRequired()
                .HasMaxLength(LoteEnvio.RemetenteNomeMaxLength);

            builder.Property(l => l.ReplyTo).HasMaxLength(320);
            builder.Property(l => l.CorpoHtml).HasColumnType("nvarchar(max)");
            builder.Property(l => l.CorpoTexto).HasColumnType("nvarchar(max)");
            builder.Property(l => l.CriadoEm).IsRequired();

            builder.Ignore(l => l.TotalDestinatarios);

            builder.HasMany(l => l.Emails)
                .WithOne()
                .HasForeignKey(e => e.LoteId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(l => l.Emails)
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.HasIndex(l => l.IdempotencyKey)
                .IsUnique()
                .HasDatabaseName("UQ_Lote_IdempotencyKey")
                .HasFilter("[IdempotencyKey] IS NOT NULL");
        }
    }
}
