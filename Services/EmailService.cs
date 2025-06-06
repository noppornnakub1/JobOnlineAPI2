using Microsoft.Extensions.Options;
using JobOnlineAPI.Models;
using MailKit.Net.Smtp;
using MimeKit;

namespace JobOnlineAPI.Services
{
    public class EmailService(IOptions<EmailSettings> emailSettings) : IEmailService
    {
        private readonly EmailSettings _emailSettings = emailSettings.Value;

        public async Task SendEmailAsync(string to, string subject, string body, bool isHtml)
        {

        var emailMessage = new MimeMessage();
            emailMessage.From.Add(new MailboxAddress(_emailSettings.SenderName, _emailSettings.FromEmail));
            emailMessage.To.Add(new MailboxAddress("", to));
            emailMessage.Subject = subject;

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = isHtml ? body : null,
                TextBody = !isHtml ? body : null
            };

            emailMessage.Body = bodyBuilder.ToMessageBody();
         try
         {

             using var client = new SmtpClient();
             await client.ConnectAsync(_emailSettings.SmtpServer, _emailSettings.SmtpPort, _emailSettings.UseSSL);
             if (!string.IsNullOrEmpty(_emailSettings.SmtpUser) && !string.IsNullOrEmpty(_emailSettings.SmtpPass))
             {
                 await client.AuthenticateAsync(_emailSettings.SmtpUser, _emailSettings.SmtpPass);
             }
             await client.SendAsync(emailMessage);
             await client.DisconnectAsync(true);
         }
         catch (Exception ex)
         {
             Console.WriteLine($"Error sending email: {ex.Message}");
             throw;
         }

        }
    }
}