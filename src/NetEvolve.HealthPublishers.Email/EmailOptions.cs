namespace NetEvolve.HealthPublishers.Email;

using System;
using System.Collections.Generic;
using MailKit.Security;
using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Represents configuration options for the Email health check publisher.
/// </summary>
public sealed record EmailOptions
{
    /// <summary>
    /// Gets or sets the hostname or IP address of the SMTP server.
    /// </summary>
    /// <remarks>
    /// Required.
    /// </remarks>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the port of the SMTP server.
    /// </summary>
    /// <remarks>
    /// Required. Must be a valid TCP port number between <c>1</c> and <c>65535</c>.
    /// </remarks>
    public int Port { get; set; }

    /// <summary>
    /// Gets or sets the mode used to secure the connection to the SMTP server.
    /// </summary>
    /// <remarks>
    /// Optional. Defaults to <see cref="SecureSocketOptions.Auto"/>, which lets MailKit decide the best option
    /// based on the configured <see cref="Port"/>.
    /// </remarks>
    public SecureSocketOptions SecureSocketOptions { get; set; } = SecureSocketOptions.Auto;

    /// <summary>
    /// Gets or sets the username used to authenticate against the SMTP server.
    /// </summary>
    /// <remarks>
    /// Optional. When set, <see cref="Password"/> must be set as well. When not set, the publisher connects to
    /// the SMTP server without authentication.
    /// </remarks>
    public string? Username { get; set; }

    /// <summary>
    /// Gets or sets the password used to authenticate against the SMTP server.
    /// </summary>
    /// <remarks>
    /// Optional. When set, <see cref="Username"/> must be set as well.
    /// </remarks>
    public string? Password { get; set; }

    /// <summary>
    /// Gets or sets the email address the health report is sent from.
    /// </summary>
    /// <remarks>
    /// Required. Must be a valid email address.
    /// </remarks>
    public string From { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the email addresses the health report is sent to.
    /// </summary>
    /// <remarks>
    /// Required. At least one valid email address must be configured.
    /// </remarks>
#pragma warning disable CA2227 // Collection properties should be read only - settable to support both code and configuration binding.
    public IList<string> To { get; set; } = [];
#pragma warning restore CA2227

    /// <summary>
    /// Gets or sets a free-form identifier for the system publishing the health report.
    /// </summary>
    /// <remarks>
    /// Required. Sent alongside <see cref="System.Environment.MachineName"/> as part of the published email,
    /// useful to distinguish reports coming from the same machine across multiple applications or instances.
    /// </remarks>
    public string SystemIdentifier { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the minimum duration an improved <see cref="HealthStatus"/> must be sustained before a
    /// recovery email is sent.
    /// </summary>
    /// <remarks>
    /// Optional. Defaults to <c>5</c> minutes. Worsening statuses are always sent immediately and are not
    /// affected by this option. An enforced minimum of <c>5</c> minutes applies.
    /// </remarks>
    public TimeSpan RecoveryConfirmationDelay { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the time zone the timestamp included in the email body is converted to.
    /// </summary>
    /// <remarks>
    /// Optional. Defaults to <c>Europe/Berlin</c>. Must be a valid identifier resolvable via
    /// <see cref="TimeZoneInfo.FindSystemTimeZoneById(string)"/>.
    /// </remarks>
    public string TimeZoneId { get; set; } = "Europe/Berlin";
}
