using EmailSender.Domain.Exceptions;
using EmailSender.Domain.ValueObjects;

namespace EmailSender.Domain.Tests
{
    public class EnderecoEmailTests
    {
        [Theory]
        [InlineData("a@teste.com")]
        [InlineData("nome.sobrenome+tag@sub.teste.com.br")]
        public void Criar_Valido_DeveGuardarValor(string valor)
        {
            var endereco = EnderecoEmail.Criar(valor);

            Assert.Equal(valor, endereco.Valor);
            Assert.Equal(valor, endereco.ToString());
        }

        [Fact]
        public void Criar_ComEspacosNasPontas_DeveAparar()
        {
            var endereco = EnderecoEmail.Criar("  a@teste.com  ");

            Assert.Equal("a@teste.com", endereco.Valor);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Criar_Vazio_DeveLancarDomainException(string? valor)
        {
            Assert.Throws<DomainException>(() => EnderecoEmail.Criar(valor));
        }

        [Theory]
        [InlineData("sem-arroba")]
        [InlineData("@teste.com")]
        [InlineData("a@")]
        [InlineData("a b@teste.com")]
        [InlineData("a@@teste.com")]
        [InlineData("Fulano <a@teste.com>")]
        public void Criar_FormatoInvalido_DeveLancarDomainException(string valor)
        {
            Assert.Throws<DomainException>(() => EnderecoEmail.Criar(valor));
        }

        [Fact]
        public void Criar_AcimaDoLimite_DeveLancarDomainException()
        {
            var valor = new string('a', EnderecoEmail.MaxLength) + "@teste.com";

            Assert.Throws<DomainException>(() => EnderecoEmail.Criar(valor));
        }

        [Fact]
        public void Igualdade_MesmoValor_DeveSerIgual()
        {
            Assert.Equal(EnderecoEmail.Criar("a@teste.com"), EnderecoEmail.Criar(" a@teste.com "));
            Assert.NotEqual(EnderecoEmail.Criar("a@teste.com"), EnderecoEmail.Criar("b@teste.com"));
        }
    }
}
