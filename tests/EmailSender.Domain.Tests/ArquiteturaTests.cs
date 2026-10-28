using EmailSender.Domain.Repositories;

namespace EmailSender.Domain.Tests
{
    public class ArquiteturaTests
    {
        [Fact]
        public void Domain_NaoDeveReferenciarEntityFrameworkCore()
        {
            var referencias = typeof(IEmailRepository).Assembly.GetReferencedAssemblies();

            Assert.DoesNotContain(referencias, a => a.Name!.StartsWith("Microsoft.EntityFrameworkCore"));
        }

        [Fact]
        public void Interfaces_DevemSerPublicas()
        {
            Assert.True(typeof(IEmailRepository).IsInterface && typeof(IEmailRepository).IsPublic);
            Assert.True(typeof(ILoteEnvioRepository).IsInterface && typeof(ILoteEnvioRepository).IsPublic);
            Assert.True(typeof(IUnitOfWork).IsInterface && typeof(IUnitOfWork).IsPublic);
        }
    }
}
