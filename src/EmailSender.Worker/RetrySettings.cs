using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace EmailSender.Worker;

public sealed class RetrySettings
{
    public const string Secao = "Retry";

    [Range(1, 10)] public int MaxTentativas { get; set; } = 3;
    [Required, MinLength(1)] public int[] IntervalosSegundos { get; set; } = [];
}

public sealed class RetrySettingsValidator : IValidateOptions<RetrySettings>
{
    public ValidateOptionsResult Validate(string? name, RetrySettings options)
    {
        var intervalos = options.IntervalosSegundos;
        if (intervalos is null || intervalos.Length == 0)
            return ValidateOptionsResult.Fail("Retry:IntervalosSegundos deve ter pelo menos um valor.");

        if (intervalos[0] < 1)
            return ValidateOptionsResult.Fail("Retry:IntervalosSegundos deve conter apenas valores positivos.");

        for (var i = 1; i < intervalos.Length; i++)
        {
            if (intervalos[i] <= intervalos[i - 1])
                return ValidateOptionsResult.Fail("Retry:IntervalosSegundos deve ser estritamente crescente.");
        }

        return ValidateOptionsResult.Success;
    }
}
