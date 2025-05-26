namespace MinimalApi.Services;

public class EmailNotificationService
{
    // Attributes
    private readonly string _smtpHost;
    private readonly int _smtpPort;
    private readonly string _senderAddress;
    private readonly string _senderPassword;
    private readonly string _senderDisplayName;

    public EmailNotificationService(IConfiguration config)
    {
        _smtpHost = config["GMAIL_SMTP_HOST"] ?? throw new ArgumentNullException("GMAIL_SMTP_HOST");
        _smtpPort = int.TryParse(config["GMAIL_SMTP_PORT"], out int smtpPort) ? smtpPort : 587;
        _senderAddress = config["GMAIL_SENDER_EMAIL_ADDRESS"] ?? throw new ArgumentNullException("GMAIL_SENDER_EMAIL_ADDRESS");
        _senderPassword = config["GMAIL_SENDER_EMAIL_PASSWORD"] ?? throw new ArgumentNullException("GMAIL_SENDER_EMAIL_PASSWORD");
        _senderDisplayName = config["GMAIL_SENDER_DISPLAY_NAME"] ?? throw new ArgumentNullException("GMAIL_SENDER_DISPLAY_NAME");
    }

    public async Task<(bool, string)> SendEmailAsync(string recipientEmail, string subject, string body)
    {
        if (string.IsNullOrEmpty(recipientEmail))
        {
            return (false, "Recipient email is empty");
        }

        try
        {
            string dummyRecipient = "vincenteous.william@global.ntt";

            // Set SMTP Client
            using var smtpClient = new SmtpClient(_smtpHost, _smtpPort)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(_senderAddress, _senderPassword)
            };

            // Set Mail Message
            using var mailMessage = new MailMessage
            {
                From = new MailAddress(_senderAddress, _senderDisplayName),
                Subject = subject,
                Body = body,
                IsBodyHtml = false
            };

            // Add Recipient
            mailMessage.To.Add(dummyRecipient);

            // Send the email
            await smtpClient.SendMailAsync(mailMessage);
            return (true, "Email sent successfully");
        }
        catch (Exception ex)
        {
            return (false, $"Failed to send email: {ex.Message}");
        }
    }
}