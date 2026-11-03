using EmailSender.Application.Abstractions;
using EmailSender.Domain.Repositories;
using EmailSender.Infrastructure.Persistence;
using EmailSender.Infrastructure.Persistence.Repositories;
using EmailSender.Infrastructure.Services;
using EmailSender.Infrastructure.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EmailSender.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("A connection string 'DefaultConnection' não foi configurada.");

            services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));

            services.AddScoped<IEmailRepository, EmailRepository>();
            services.AddScoped<ILoteEnvioRepository, LoteEnvioRepository>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();

            services.AddOptions<SmtpSettings>()
                .Bind(configuration.GetSection(SmtpSettings.Secao))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddScoped<IEmailSender, SmtpEmailSender>();

            return services;
        }
    }
}
