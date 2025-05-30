namespace MinimalApi.Services;

public class EmailNotificationService
{
    // Attributes
    private readonly string _smtpHost;
    private readonly int _smtpPort;
    private readonly string _senderAddress;
    private readonly string _senderPassword;
    private readonly string _senderDisplayName;
    private readonly string _dummyRecipientAddress;

    public EmailNotificationService(IConfiguration config)
    {
        _smtpHost = config["MAIL_SMTP_HOST"] ?? throw new ArgumentNullException("MAIL_SMTP_HOST");
        _smtpPort = int.TryParse(config["MAIL_SMTP_PORT"], out int smtpPort) ? smtpPort : 587;
        _senderAddress = config["MAIL_SENDER_EMAIL_ADDRESS"] ?? throw new ArgumentNullException("MAIL_SENDER_EMAIL_ADDRESS");
        _senderPassword = config["MAIL_SENDER_EMAIL_PASSWORD"] ?? throw new ArgumentNullException("MAIL_SENDER_EMAIL_PASSWORD");
        _senderDisplayName = config["MAIL_SENDER_DISPLAY_NAME"] ?? throw new ArgumentNullException("MAIL_SENDER_DISPLAY_NAME");
        _dummyRecipientAddress = config["MAIL_DUMMY_RECIPIENT_ADDRESS"] ?? throw new ArgumentNullException("MAIL_DUMMY_RECIPIENT_ADDRESS");
    }

    public async Task<(bool, string)> SendEmailAsync(string recipientEmail, string subject, string body)
    {
        if (string.IsNullOrEmpty(recipientEmail))
        {
            return (false, "Recipient email is empty");
        }

        try
        {
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
            mailMessage.To.Add(string.IsNullOrEmpty(_dummyRecipientAddress) ? recipientEmail : _dummyRecipientAddress);

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