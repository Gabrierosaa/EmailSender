namespace EmailSender.Domain.Exceptions
{
    public class ConflitoConcorrenciaException : Exception
    {
        public ConflitoConcorrenciaException(string message, Exception? innerException = null)
            : base(message, innerException)
        {
        }
    }
}
