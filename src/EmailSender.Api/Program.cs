using EmailSender.Api.ErrorHandling;
using EmailSender.Api.OpenApi;
using EmailSender.Application;
using EmailSender.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = Limites.CorpoRequisicaoBytes);

builder.Services
    .AddControllers(options =>
    {
        options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
    });

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

builder.Services.AddOpenApi(options => options.AddOperationTransformer<ExemplosCriarEmailTransformer>());
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "EmailSender API v1"));
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();

public partial class Program;
