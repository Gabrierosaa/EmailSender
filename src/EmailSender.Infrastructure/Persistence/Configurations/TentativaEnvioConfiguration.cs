using EmailSender.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmailSender.Infrastructure.Persistence.Configurations
{
    internal sealed class TentativaEnvioConfiguration : IEntityTypeConfiguration<TentativaEnvio>
    {
        public void Configure(EntityTypeBuilder<TentativaEnvio> builder)
        {
            builder.ToTable("TentativasEnvio");

            builder.HasKey(t => t.Id);
            builder.Property(t => t.Id).ValueGeneratedNever();

            builder.Property(t => t.EmailId).IsRequired();
            builder.Property(t => t.Numero).IsRequired();
            builder.Property(t => t.IniciadaEm).IsRequired();
            builder.Property(t => t.Sucesso).IsRequired();

            builder.Property(t => t.CodigoErro)
                .IsRequired(false)
                .HasMaxLength(100);

            builder.HasIndex(t => new { t.EmailId, t.Numero }).IsUnique();
        }
    }
}
