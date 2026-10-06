using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
namespace NotificationService.Services;
public sealed class ConfiguredDeliveryEmail(IConfiguration config):IDeliveryEmail
{
    public async Task<string> SendAsync(DurableMessage message,Guid id,CancellationToken ct)
    {
        if(!config.GetValue<bool>("Smtp:DeliveryEnabled")||string.IsNullOrWhiteSpace(config["Smtp:Host"])||string.IsNullOrWhiteSpace(config["Smtp:Username"])||string.IsNullOrWhiteSpace(config["Smtp:Password"]))return "PendingConfiguration";
        var started=false;
        try {
            var mail=new MimeMessage{Subject=message.Subject,MessageId=id+"@das.local"};mail.From.Add(MailboxAddress.Parse(config["Smtp:Username"]!));mail.To.Add(MailboxAddress.Parse(message.RecipientEmail!));
            foreach(var address in message.Cc??[])mail.Cc.Add(MailboxAddress.Parse(address));
            mail.Body=new TextPart("plain"){Text=message.Body};
            using var smtp=new SmtpClient();smtp.Timeout=60000;
            await smtp.ConnectAsync(config["Smtp:Host"]!,config.GetValue("Smtp:Port",587),config.GetValue<bool>("Smtp:UseSSL")?SecureSocketOptions.SslOnConnect:SecureSocketOptions.StartTls,ct);
            await smtp.AuthenticateAsync(config["Smtp:Username"]!,config["Smtp:Password"]!,ct);
            started=true;await smtp.SendAsync(mail,ct);return "Sent";
        }
        catch(Exception e) when(e is not OutOfMemoryException){return started?"UnknownOutcome":"Retryable";}
    }
}
