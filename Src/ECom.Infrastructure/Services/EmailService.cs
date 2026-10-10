using Ecom.Core.DTO.sharing;
using Ecom.Core.Services;

using MailKit.Security;

using Microsoft.Extensions.Configuration;

using MimeKit;

namespace ECom.Infrastructure.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration configuration;
        public EmailService(IConfiguration configuration)
        {
            this.configuration = configuration;
        }
        //STMP
        public async Task SendEmail(EmailDto emailDto)
        {
            string FromAdress = configuration["EmailSetting:From"];
            MimeMessage message = new();
            message.From.Add(new MailboxAddress("My Ecom", FromAdress));
            message.Subject = emailDto.Subject;
            message.To.Add(new MailboxAddress(emailDto.To, emailDto.To));
            message.Body = new TextPart(MimeKit.Text.TextFormat.Html)
            {
                Text = emailDto.Content
            };
            int port = int.Parse(configuration["EmailSetting:Port"]);
            string host = configuration["EmailSetting:smtp"];
            using (var smtp = new MailKit.Net.Smtp.SmtpClient())
            {
                try
                {
                    smtp.CheckCertificateRevocation = false;
                    await smtp.ConnectAsync(host, port, SecureSocketOptions.SslOnConnect);
                    string userName = configuration["EmailSetting:username"];
                    string password = configuration["EmailSetting:password"];
                    await smtp.AuthenticateAsync(userName, password);
                    await smtp.SendAsync(message);
                }
                catch
                {
                    throw;
                }
                finally
                {
                    smtp.Disconnect(true);
                    smtp.Dispose();
                }
            }
        }

        public string returnEmailFrom()
        {
            return configuration["EmailSetting:From"];
        }
    }
}
