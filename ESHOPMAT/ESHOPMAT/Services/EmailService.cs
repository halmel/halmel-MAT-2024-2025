using SendGrid;
using SendGrid.Helpers.Mail;
using System.Threading.Tasks;
using ESHOPMAT.Models;

public class EmailService
{
    private readonly string _sendGridApiKey;

    public EmailService()
    {
        _sendGridApiKey = "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";
    }

    public async Task SendEmailAsync(string toEmail, string subject, string htmlContent, string plainTextContent)
    {
        var client = new SendGridClient(_sendGridApiKey);
        var from = new EmailAddress("your-email@example.com", "Your Name");
        var to = new EmailAddress(toEmail);
        var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent, htmlContent);
        var response = await client.SendEmailAsync(msg);
    }

    public async Task SendEmailAsync(string toEmail, EmailTemplate template, EmailValues Values)
    {
        string processedHtml = template.ProcessTemplate(Values);
        await SendEmailAsync(toEmail, template.Subject, processedHtml, template.PlainTextContent);
    }
}
