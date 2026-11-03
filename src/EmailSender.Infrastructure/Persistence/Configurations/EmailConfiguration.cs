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

            builder.Property(e => e.ProximaTentativaEm);

            builder.Property<byte[]>("RowVersion").IsRowVersion();

            builder.Property<DateTimeOffset>("CriadoEm").HasDefaultValueSql("SYSDATETIMEOFFSET()");

            builder.HasMany(e => e.Tentativas)
                .WithOne()
                .HasForeignKey(t => t.EmailId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(e => e.Tentativas)
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.HasIndex(e => new { e.LoteId, e.Status })
                .HasDatabaseName("IX_Email_LoteId_Status");

            builder.HasIndex("Status", "CriadoEm")
                .HasDatabaseName("IX_Email_Status_CriadoEm");

            builder.HasIndex(e => new { e.LoteId, e.Destinatario })
                .IsUnique()
                .HasDatabaseName("UQ_Email_LoteId_Destinatario");
        }
    }
}
