using EmailSender.Application.DTOs;
using EmailSender.Application.Emails.Commands.ProcessarEmailsPendentes;
using EmailSender.Application.Emails.Commands.RegistrarEmail;
using EmailSender.Application.Emails.Queries.ConsultarStatus;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EmailSender.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddSingleton<IValidator<CriarEmailRequest>, CriarEmailRequestValidator>();
            services.AddScoped<RegistrarEmailHandler>();
            services.AddScoped<ProcessarEmailHandler>();
            services.AddSingleton<IProcessadorEmailsPendentes, ProcessarEmailsPendentesHandler>();
            services.TryAddSingleton(TimeProvider.System);
            services.AddScoped<ConsultarStatusHandler>();

            return services;
        }
    }
}
