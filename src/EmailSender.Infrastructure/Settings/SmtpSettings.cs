using System.ComponentModel.DataAnnotations;
using MailKit.Security;

namespace EmailSender.Infrastructure.Settings
{
    public sealed class SmtpSettings
    {
        public const string Secao = "Smtp";

        [Required] public string Host { get; set; } = "";
        [Range(1, 65535)] public int Porta { get; set; }
        public string? Usuario { get; set; }
        public string? Senha { get; set; }
        [Required, EmailAddress] public string RemetenteEndereco { get; set; } = "";
        public SecureSocketOptions Seguranca { get; set; } = SecureSocketOptions.StartTls;
        [Range(1, 300)] public int TimeoutSegundos { get; set; } = 30;
    }
}
