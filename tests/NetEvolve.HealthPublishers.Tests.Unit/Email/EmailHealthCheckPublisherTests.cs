namespace NetEvolve.HealthPublishers.Tests.Unit.Email;

using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using MailKit.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using MimeKit;
using NetEvolve.Extensions.TUnit;
using NetEvolve.HealthPublishers.Email;
using TUnit.Mocks;

[TestGroup(nameof(Email))]
public sealed class EmailHealthCheckPublisherTests
{
    private const string TestName = "Test";

    [Test]
    [Arguments(HealthStatus.Healthy)]
    [Arguments(HealthStatus.Degraded)]
    [Arguments(HealthStatus.Unhealthy)]
    public async Task PublishAsync_WhenReportHasStatus_SendsMessageWithStatusInSubject(HealthStatus status)
    {
        // Arrange
        var mock = ISmtpSender.Mock();
        MimeMessage? captured = null;
        _ = mock.SendAsync(Any(), Any(), Any())
            .Callback((EmailOptions _, MimeMessage message, CancellationToken _) => captured = message);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var publisher = new EmailHealthCheckPublisher(TestName, mock, optionsMonitor, TimeProvider.System);
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal)
            {
                ["self"] = new HealthReportEntry(status, null, TimeSpan.FromMilliseconds(5), null, null),
            },
            TimeSpan.FromMilliseconds(42)
        );

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        using (Assert.Multiple())
        {
            mock.SendAsync(Any(), Any(), Any()).WasCalled(Times.Once);
            _ = await Assert.That(captured).IsNotNull();
            _ = await Assert.That(captured!.Subject).Contains(status.ToString());
        }
    }

    [Test]
    public async Task PublishAsync_WhenCalled_SetsFromAndToAddresses()
    {
        // Arrange
        var mock = ISmtpSender.Mock();
        MimeMessage? captured = null;
        _ = mock.SendAsync(Any(), Any(), Any())
            .Callback((EmailOptions _, MimeMessage message, CancellationToken _) => captured = message);
        var optionsMonitor = CreateOptionsMonitor(options =>
        {
            options.From = "sender@example.com";
            options.To = ["recipient-a@example.com", "recipient-b@example.com"];
        });
        var publisher = new EmailHealthCheckPublisher(TestName, mock, optionsMonitor, TimeProvider.System);
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal), TimeSpan.Zero);

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var message = captured!;
        using (Assert.Multiple())
        {
            _ = await Assert.That(message.From.Mailboxes).Contains(mailbox => mailbox.Address == "sender@example.com");
            _ = await Assert
                .That(message.To.Mailboxes)
                .Contains(mailbox => mailbox.Address == "recipient-a@example.com");
            _ = await Assert
                .That(message.To.Mailboxes)
                .Contains(mailbox => mailbox.Address == "recipient-b@example.com");
        }
    }

    [Test]
    public async Task PublishAsync_WhenSystemIdentifierProvided_IncludesMachineNameAndSystemIdentifierInBody()
    {
        // Arrange
        var mock = ISmtpSender.Mock();
        MimeMessage? captured = null;
        _ = mock.SendAsync(Any(), Any(), Any())
            .Callback((EmailOptions _, MimeMessage message, CancellationToken _) => captured = message);
        var optionsMonitor = CreateOptionsMonitor(options => options.SystemIdentifier = "checkout-service");
        var publisher = new EmailHealthCheckPublisher(TestName, mock, optionsMonitor, TimeProvider.System);
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal), TimeSpan.Zero);

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var body = GetTextBody(captured!);
        using (Assert.Multiple())
        {
            _ = await Assert.That(body).Contains($"Machine: {Environment.MachineName}");
            _ = await Assert.That(body).Contains("System: checkout-service");
        }
    }

    [Test]
    public async Task PublishAsync_WhenCalled_UsesTimeProviderForDate()
    {
        // Arrange
        var mock = ISmtpSender.Mock();
        MimeMessage? captured = null;
        _ = mock.SendAsync(Any(), Any(), Any())
            .Callback((EmailOptions _, MimeMessage message, CancellationToken _) => captured = message);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));
        var publisher = new EmailHealthCheckPublisher(TestName, mock, optionsMonitor, timeProvider);
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal), TimeSpan.Zero);

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        _ = await Assert.That(captured!.Date).IsEqualTo(timeProvider.GetUtcNow());
    }

    [Test]
    public async Task PublishAsync_WhenReportHasNoEntries_OmitsEntriesSection()
    {
        // Arrange
        var mock = ISmtpSender.Mock();
        MimeMessage? captured = null;
        _ = mock.SendAsync(Any(), Any(), Any())
            .Callback((EmailOptions _, MimeMessage message, CancellationToken _) => captured = message);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var publisher = new EmailHealthCheckPublisher(TestName, mock, optionsMonitor, TimeProvider.System);
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal),
            TimeSpan.FromMilliseconds(42)
        );

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var body = GetTextBody(captured!);
        using (Assert.Multiple())
        {
            _ = await Assert.That(body).Contains("Health check report Healthy in 42ms");
            _ = await Assert.That(body).DoesNotContain("Entries:");
        }
    }

    [Test]
    public async Task PublishAsync_WhenReportHasEntries_IncludesEntryDetailsInBody()
    {
        // Arrange
        var mock = ISmtpSender.Mock();
        MimeMessage? captured = null;
        _ = mock.SendAsync(Any(), Any(), Any())
            .Callback((EmailOptions _, MimeMessage message, CancellationToken _) => captured = message);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var publisher = new EmailHealthCheckPublisher(TestName, mock, optionsMonitor, TimeProvider.System);
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal)
            {
                ["database"] = new HealthReportEntry(
                    HealthStatus.Degraded,
                    "slow response",
                    TimeSpan.FromMilliseconds(120),
                    null,
                    null
                ),
            },
            TimeSpan.FromMilliseconds(120)
        );

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var body = GetTextBody(captured!);
        using (Assert.Multiple())
        {
            _ = await Assert.That(body).Contains("Entries:");
            _ = await Assert.That(body).Contains("- database: Degraded (120ms) - slow response");
        }
    }

    [Test]
    public async Task PublishAsync_WhenEntryHasNoDescription_OmitsDescriptionSuffix()
    {
        // Arrange
        var mock = ISmtpSender.Mock();
        MimeMessage? captured = null;
        _ = mock.SendAsync(Any(), Any(), Any())
            .Callback((EmailOptions _, MimeMessage message, CancellationToken _) => captured = message);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var publisher = new EmailHealthCheckPublisher(TestName, mock, optionsMonitor, TimeProvider.System);
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal)
            {
                ["self"] = new HealthReportEntry(HealthStatus.Healthy, null, TimeSpan.FromMilliseconds(3), null, null),
            },
            TimeSpan.FromMilliseconds(3)
        );

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        var body = GetTextBody(captured!);
        _ = await Assert.That(body).Contains("- self: Healthy (3ms)" + Environment.NewLine);
    }

    [Test]
    public async Task PublishAsync_WhenSmtpConnectionFails_PropagatesException()
    {
        // Arrange
        var mock = ISmtpSender.Mock();
        _ = mock.SendAsync(Any(), Any(), Any()).Throws<SocketException>();
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var publisher = new EmailHealthCheckPublisher(TestName, mock, optionsMonitor, TimeProvider.System);
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal), TimeSpan.Zero);

        // Act
        SocketException? caught = null;
        try
        {
            await publisher.PublishAsync(report, CancellationToken.None);
        }
        catch (SocketException ex)
        {
            caught = ex;
        }

        // Assert
        _ = await Assert.That(caught).IsNotNull();
    }

    [Test]
    public async Task PublishAsync_WhenSmtpAuthenticationFails_PropagatesException()
    {
        // Arrange
        var mock = ISmtpSender.Mock();
        _ = mock.SendAsync(Any(), Any(), Any()).Throws(new AuthenticationException("bad credentials"));
        var optionsMonitor = CreateOptionsMonitor(options =>
        {
            options.Username = "smtp-user";
            options.Password = "wrong-password";
        });
        var publisher = new EmailHealthCheckPublisher(TestName, mock, optionsMonitor, TimeProvider.System);
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal), TimeSpan.Zero);

        // Act
        AuthenticationException? caught = null;
        try
        {
            await publisher.PublishAsync(report, CancellationToken.None);
        }
        catch (AuthenticationException ex)
        {
            caught = ex;
        }

        // Assert
        _ = await Assert.That(caught).IsNotNull();
    }

    private static string GetTextBody(MimeMessage message) => ((TextPart)message.Body!).Text ?? string.Empty;

    private static IOptionsMonitor<EmailOptions> CreateOptionsMonitor(Action<EmailOptions> configure)
    {
        var services = new ServiceCollection();
        _ = services.Configure<EmailOptions>(
            TestName,
            options =>
            {
                options.Host = "smtp.example.com";
                options.Port = 587;
                options.From = "health-checks@example.com";
                options.To = ["ops-team@example.com"];
                options.SystemIdentifier = "test-system";
                configure(options);
            }
        );
        return services.BuildServiceProvider().GetRequiredService<IOptionsMonitor<EmailOptions>>();
    }
}
