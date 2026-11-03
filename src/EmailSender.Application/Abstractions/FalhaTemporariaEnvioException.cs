namespace EmailSender.Application.Abstractions
{
    public sealed class FalhaTemporariaEnvioException : Exception
    {
        public FalhaTemporariaEnvioException(string message, Exception? inner = null)
            : base(message, inner) { }
    }
}
