using EmailSender.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EmailSender.IntegrationTests
{
    /// <summary>
    /// Sobe a Api de verdade (Kestrel, para valer o limite de 1 MB) apontando para um banco novo no container.
    /// SMTP e RabbitMQ são configurados para endereços inalcançáveis: nenhum dos dois está no ar.
    /// </summary>
    public sealed class ApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _servidor;
        private readonly string _database = $"EmailSenderApi_{Guid.NewGuid():N}";

        public ApiFactory(string connectionStringDoServidor)
        {
            _servidor = connectionStringDoServidor;
            UseKestrel();
        }

        public string ConnectionString =>
            new SqlConnectionStringBuilder(_servidor) { InitialCatalog = _database }.ConnectionString;

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:DefaultConnection", ConnectionString);
            builder.UseSetting("Smtp:Host", "127.0.0.1");
            builder.UseSetting("Smtp:Porta", "1");
            builder.UseSetting("Smtp:RemetenteEndereco", "teste@localhost");
            builder.UseSetting("RabbitMq:Host", "127.0.0.1");
            builder.UseSetting("RabbitMq:Port", "1");
        }

        /// <summary>Sobe o host e cria o schema pelas migrations.</summary>
        public async Task MigrarAsync()
        {
            using var scope = Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
        }

        public async Task ApagarBancoAsync()
        {
            await using var context = new AppDbContext(
                new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(ConnectionString).Options);
            await context.Database.EnsureDeletedAsync();
        }

        public async Task<(int Lotes, int Emails)> ContarRegistrosAsync()
        {
            await using var context = new AppDbContext(
                new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(ConnectionString).Options);
            return (await context.Lotes.CountAsync(), await context.Emails.CountAsync());
        }
    }
}
