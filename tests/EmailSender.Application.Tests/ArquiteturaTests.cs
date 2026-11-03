using EmailSender.Application.Abstractions;

namespace EmailSender.Application.Tests
{
    public class ArquiteturaTests
    {
        [Fact]
        public void Application_NaoDeveReferenciarEntityFrameworkCoreNemInfrastructure()
        {
            var referencias = typeof(IEmailSender).Assembly.GetReferencedAssemblies();

            Assert.DoesNotContain(referencias, a => a.Name!.StartsWith("Microsoft.EntityFrameworkCore"));
            Assert.DoesNotContain(referencias, a => a.Name == "EmailSender.Infrastructure");
        }

        [Fact]
        public void IEmailSender_DeveSerInterfacePublica()
        {
            Assert.True(typeof(IEmailSender).IsInterface && typeof(IEmailSender).IsPublic);
        }
    }
}
