namespace NetEvolve.HealthPublishers.Email;

using System.Threading;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MimeKit;

/// <summary>
/// Default <see cref="ISmtpSender"/> implementation, using the MailKit <see cref="SmtpClient"/> to connect, and
/// optionally authenticate against, an SMTP server.
/// </summary>
internal sealed class MailKitSmtpSender : ISmtpSender
{
    public async Task SendAsync(EmailOptions options, MimeMessage message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var client = new SmtpClient();

        await client
            .ConnectAsync(options.Host, options.Port, options.SecureSocketOptions, cancellationToken)
            .ConfigureAwait(false);

        if (!string.IsNullOrEmpty(options.Username))
        {
            // EmailOptionsConfigure guarantees Username and Password are either both set or both unset.
            await client
                .AuthenticateAsync(options.Username, options.Password!, cancellationToken)
                .ConfigureAwait(false);
        }

        _ = await client.SendAsync(message, cancellationToken).ConfigureAwait(false);

        await client.DisconnectAsync(true, cancellationToken).ConfigureAwait(false);
    }
}
