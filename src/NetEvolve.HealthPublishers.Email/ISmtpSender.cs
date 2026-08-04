namespace NetEvolve.HealthPublishers.Email;

using System.Threading;
using System.Threading.Tasks;
using MimeKit;

/// <summary>
/// Abstracts sending a <see cref="MimeMessage"/> over SMTP, so the publisher itself does not depend directly on a
/// concrete SMTP client implementation.
/// </summary>
internal interface ISmtpSender
{
    /// <summary>
    /// Sends the <paramref name="message"/> to the SMTP server described by <paramref name="options"/>.
    /// </summary>
    /// <param name="options">The SMTP connection settings to use.</param>
    /// <param name="message">The message to send.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/>.</param>
    Task SendAsync(EmailOptions options, MimeMessage message, CancellationToken cancellationToken);
}
