using EmailSender.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EmailSender.IntegrationTests
{
    public sealed class ApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _servidor;
        private readonly string _database = $"EmailSenderApi_{Guid.NewGuid():N}";

        private readonly Action<IServiceCollection>? _configurarServicos;

        public ApiFactory(string connectionStringDoServidor, Action<IServiceCollection>? configurarServicos = null)
        {
            _servidor = connectionStringDoServidor;
            _configurarServicos = configurarServicos;
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

            if (_configurarServicos is not null)
                builder.ConfigureTestServices(_configurarServicos);
        }

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
