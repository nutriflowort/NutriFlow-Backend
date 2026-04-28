using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Nutriflow.Services
{
    public class EmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task EnviarRecuperacionPassword(string destino, string codigo, string resetLink)
        {
            var mensaje = new MimeMessage();

            mensaje.From.Add(new MailboxAddress("NutriFlow", _configuration["EmailSettings:Email"]));
            mensaje.To.Add(MailboxAddress.Parse(destino));
            mensaje.Subject = "Recuperar contraseña - NutriFlow";

            mensaje.Body = new TextPart("html")
            {
                Text = $@"
                    <h2>Recuperar contraseña - NutriFlow</h2>

                    <p>Recibimos una solicitud para cambiar tu contraseña.</p>

                    <p>Tu código de verificación es:</p>

                    <h1>{codigo}</h1>

                    <p>Entrá al siguiente link para cambiar tu contraseña:</p>

                    <a href='{resetLink}'>Cambiar contraseña</a>

                    <p>Este código vence en 1 hora.</p>

                    <p>Si no fuiste vos, ignorá este mensaje.</p>
                "
            };

            using var smtp = new SmtpClient();

            await smtp.ConnectAsync(
                _configuration["EmailSettings:Host"],
                int.Parse(_configuration["EmailSettings:Port"]!),
                SecureSocketOptions.StartTls
            );

            await smtp.AuthenticateAsync(
                _configuration["EmailSettings:Email"],
                _configuration["EmailSettings:Password"]
            );

            await smtp.SendAsync(mensaje);
            await smtp.DisconnectAsync(true);
        }
    }
}