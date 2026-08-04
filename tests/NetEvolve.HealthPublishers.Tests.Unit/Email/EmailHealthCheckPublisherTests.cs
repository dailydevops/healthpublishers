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
    private static readonly DateTimeOffset TestStart = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    [Test]
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
    public async Task PublishAsync_WhenFreshPublisherReceivesHealthyReport_DoesNotSend()
    {
        // Arrange
        var mock = ISmtpSender.Mock();
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var publisher = new EmailHealthCheckPublisher(TestName, mock, optionsMonitor, TimeProvider.System);
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal), TimeSpan.Zero);

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        mock.SendAsync(Any(), Any(), Any()).WasCalled(Times.Never);
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
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal),
            HealthStatus.Degraded,
            TimeSpan.Zero
        );

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
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal),
            HealthStatus.Unhealthy,
            TimeSpan.Zero
        );

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
    public async Task PublishAsync_WhenTimeZoneIdNotConfigured_IncludesTimestampInBerlinTimeZoneByDefault()
    {
        // Arrange
        var mock = ISmtpSender.Mock();
        MimeMessage? captured = null;
        _ = mock.SendAsync(Any(), Any(), Any())
            .Callback((EmailOptions _, MimeMessage message, CancellationToken _) => captured = message);
        var optionsMonitor = CreateOptionsMonitor(options => { });
        var timeProvider = new FakeTimeProvider(TestStart);
        var publisher = new EmailHealthCheckPublisher(TestName, mock, optionsMonitor, timeProvider);
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal),
            HealthStatus.Degraded,
            TimeSpan.Zero
        );

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert - TestStart is 2026-01-02T03:04:05Z; Europe/Berlin is CET (UTC+1) in January, no DST.
        var body = GetTextBody(captured!);
        _ = await Assert.That(body).Contains("Timestamp: 2026-01-02 04:04:05 +01:00 (Europe/Berlin)");
    }

    [Test]
    public async Task PublishAsync_WhenTimeZoneIdConfigured_ConvertsTimestampToThatZone()
    {
        // Arrange
        var mock = ISmtpSender.Mock();
        MimeMessage? captured = null;
        _ = mock.SendAsync(Any(), Any(), Any())
            .Callback((EmailOptions _, MimeMessage message, CancellationToken _) => captured = message);
        var optionsMonitor = CreateOptionsMonitor(options => options.TimeZoneId = "America/New_York");
        var timeProvider = new FakeTimeProvider(TestStart);
        var publisher = new EmailHealthCheckPublisher(TestName, mock, optionsMonitor, timeProvider);
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal),
            HealthStatus.Degraded,
            TimeSpan.Zero
        );

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert - TestStart is 2026-01-02T03:04:05Z; America/New_York is EST (UTC-5) in January, no DST.
        var body = GetTextBody(captured!);
        _ = await Assert.That(body).Contains("Timestamp: 2026-01-01 22:04:05 -05:00 (America/New_York)");
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
        var timeProvider = new FakeTimeProvider(TestStart);
        var publisher = new EmailHealthCheckPublisher(TestName, mock, optionsMonitor, timeProvider);
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal),
            HealthStatus.Degraded,
            TimeSpan.Zero
        );

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
        var optionsMonitor = CreateOptionsMonitor(options =>
            options.RecoveryConfirmationDelay = TimeSpan.FromMinutes(5)
        );
        var timeProvider = new FakeTimeProvider(TestStart);
        var publisher = new EmailHealthCheckPublisher(TestName, mock, optionsMonitor, timeProvider);
        var baselineReport = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal),
            HealthStatus.Unhealthy,
            TimeSpan.Zero
        );
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal),
            TimeSpan.FromMilliseconds(42)
        );

        // Act
        await publisher.PublishAsync(baselineReport, CancellationToken.None); // moves baseline off Healthy
        await publisher.PublishAsync(report, CancellationToken.None); // starts the recovery-confirmation timer
        timeProvider.Advance(TimeSpan.FromMinutes(5));
        await publisher.PublishAsync(report, CancellationToken.None); // pending window elapsed, now sends

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
        var optionsMonitor = CreateOptionsMonitor(options =>
            options.RecoveryConfirmationDelay = TimeSpan.FromMinutes(5)
        );
        var timeProvider = new FakeTimeProvider(TestStart);
        var publisher = new EmailHealthCheckPublisher(TestName, mock, optionsMonitor, timeProvider);
        var baselineReport = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal),
            HealthStatus.Unhealthy,
            TimeSpan.Zero
        );
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal)
            {
                ["self"] = new HealthReportEntry(HealthStatus.Healthy, null, TimeSpan.FromMilliseconds(3), null, null),
            },
            TimeSpan.FromMilliseconds(3)
        );

        // Act
        await publisher.PublishAsync(baselineReport, CancellationToken.None); // moves baseline off Healthy
        await publisher.PublishAsync(report, CancellationToken.None); // starts the recovery-confirmation timer
        timeProvider.Advance(TimeSpan.FromMinutes(5));
        await publisher.PublishAsync(report, CancellationToken.None); // pending window elapsed, now sends

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
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal),
            HealthStatus.Unhealthy,
            TimeSpan.Zero
        );

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
        var report = new HealthReport(
            new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal),
            HealthStatus.Unhealthy,
            TimeSpan.Zero
        );

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

    [Test]
    public async Task PublishAsync_WhenStatusWorsensFromHealthyToDegraded_SendsImmediately()
    {
        // Arrange
        var mock = ISmtpSender.Mock();
        var optionsMonitor = CreateOptionsMonitor(options =>
            options.RecoveryConfirmationDelay = TimeSpan.FromMinutes(5)
        );
        var timeProvider = new FakeTimeProvider(TestStart);
        var publisher = new EmailHealthCheckPublisher(TestName, mock, optionsMonitor, timeProvider);
        var report = CreateReport(HealthStatus.Degraded);

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        mock.SendAsync(Any(), Any(), Any()).WasCalled(Times.Once);
    }

    [Test]
    public async Task PublishAsync_WhenStatusWorsensFromHealthyToUnhealthy_SendsImmediately()
    {
        // Arrange
        var mock = ISmtpSender.Mock();
        var optionsMonitor = CreateOptionsMonitor(options =>
            options.RecoveryConfirmationDelay = TimeSpan.FromMinutes(5)
        );
        var timeProvider = new FakeTimeProvider(TestStart);
        var publisher = new EmailHealthCheckPublisher(TestName, mock, optionsMonitor, timeProvider);
        var report = CreateReport(HealthStatus.Unhealthy);

        // Act
        await publisher.PublishAsync(report, CancellationToken.None);

        // Assert
        mock.SendAsync(Any(), Any(), Any()).WasCalled(Times.Once);
    }

    [Test]
    public async Task PublishAsync_WhenStatusWorsensAgainFromDegradedToUnhealthy_SendsImmediately()
    {
        // Arrange
        var mock = ISmtpSender.Mock();
        var optionsMonitor = CreateOptionsMonitor(options =>
            options.RecoveryConfirmationDelay = TimeSpan.FromMinutes(5)
        );
        var timeProvider = new FakeTimeProvider(TestStart);
        var publisher = new EmailHealthCheckPublisher(TestName, mock, optionsMonitor, timeProvider);

        // Act
        await publisher.PublishAsync(CreateReport(HealthStatus.Degraded), CancellationToken.None);
        await publisher.PublishAsync(CreateReport(HealthStatus.Unhealthy), CancellationToken.None);

        // Assert
        mock.SendAsync(Any(), Any(), Any()).WasCalled(Times.Exactly(2));
    }

    [Test]
    public async Task PublishAsync_WhenImprovementNotYetSustained_DoesNotSend()
    {
        // Arrange
        var mock = ISmtpSender.Mock();
        var optionsMonitor = CreateOptionsMonitor(options =>
            options.RecoveryConfirmationDelay = TimeSpan.FromMinutes(5)
        );
        var timeProvider = new FakeTimeProvider(TestStart);
        var publisher = new EmailHealthCheckPublisher(TestName, mock, optionsMonitor, timeProvider);

        // Act
        await publisher.PublishAsync(CreateReport(HealthStatus.Unhealthy), CancellationToken.None); // worsening, sends
        timeProvider.Advance(TimeSpan.FromMinutes(4));
        await publisher.PublishAsync(CreateReport(HealthStatus.Healthy), CancellationToken.None); // improvement, not yet due

        // Assert
        mock.SendAsync(Any(), Any(), Any()).WasCalled(Times.Once);
    }

    [Test]
    public async Task PublishAsync_WhenImprovementSustainedForExactlyTheDelay_Sends()
    {
        // Arrange
        var mock = ISmtpSender.Mock();
        var optionsMonitor = CreateOptionsMonitor(options =>
            options.RecoveryConfirmationDelay = TimeSpan.FromMinutes(5)
        );
        var timeProvider = new FakeTimeProvider(TestStart);
        var publisher = new EmailHealthCheckPublisher(TestName, mock, optionsMonitor, timeProvider);

        // Act
        await publisher.PublishAsync(CreateReport(HealthStatus.Unhealthy), CancellationToken.None); // worsening, sends
        await publisher.PublishAsync(CreateReport(HealthStatus.Healthy), CancellationToken.None); // improvement, starts timer
        timeProvider.Advance(TimeSpan.FromMinutes(5));
        await publisher.PublishAsync(CreateReport(HealthStatus.Healthy), CancellationToken.None); // pending window elapsed, sends

        // Assert
        mock.SendAsync(Any(), Any(), Any()).WasCalled(Times.Exactly(2));
    }

    [Test]
    public async Task PublishAsync_WhenRegressingBackToLastNotifiedStatus_CancelsPendingAndRestartsTimer()
    {
        // Arrange
        var mock = ISmtpSender.Mock();
        var optionsMonitor = CreateOptionsMonitor(options =>
            options.RecoveryConfirmationDelay = TimeSpan.FromMinutes(5)
        );
        var timeProvider = new FakeTimeProvider(TestStart);
        var publisher = new EmailHealthCheckPublisher(TestName, mock, optionsMonitor, timeProvider);

        // Act
        await publisher.PublishAsync(CreateReport(HealthStatus.Unhealthy), CancellationToken.None); // worsening, sends
        await publisher.PublishAsync(CreateReport(HealthStatus.Degraded), CancellationToken.None); // improvement, starts timer
        await publisher.PublishAsync(CreateReport(HealthStatus.Unhealthy), CancellationToken.None); // matches last-notified, cancels pending
        timeProvider.Advance(TimeSpan.FromMinutes(10)); // well past the delay
        await publisher.PublishAsync(CreateReport(HealthStatus.Degraded), CancellationToken.None); // fresh pending timer starting now, not due yet

        // Assert
        mock.SendAsync(Any(), Any(), Any()).WasCalled(Times.Once);
    }

    [Test]
    public async Task PublishAsync_WhenFluctuatingBetweenImprovedStatuses_DoesNotResetPendingTimer()
    {
        // Arrange
        var mock = ISmtpSender.Mock();
        var optionsMonitor = CreateOptionsMonitor(options =>
            options.RecoveryConfirmationDelay = TimeSpan.FromMinutes(5)
        );
        var timeProvider = new FakeTimeProvider(TestStart);
        var publisher = new EmailHealthCheckPublisher(TestName, mock, optionsMonitor, timeProvider);

        // Act
        await publisher.PublishAsync(CreateReport(HealthStatus.Unhealthy), CancellationToken.None); // worsening, sends
        await publisher.PublishAsync(CreateReport(HealthStatus.Degraded), CancellationToken.None); // improvement, timer starts at t0
        timeProvider.Advance(TimeSpan.FromMinutes(1));
        await publisher.PublishAsync(CreateReport(HealthStatus.Healthy), CancellationToken.None); // still an improvement, timer origin unchanged
        timeProvider.Advance(TimeSpan.FromMinutes(4)); // total elapsed from t0 is now exactly the delay
        await publisher.PublishAsync(CreateReport(HealthStatus.Healthy), CancellationToken.None); // sends, proving the timer origin stayed at t0

        // Assert
        mock.SendAsync(Any(), Any(), Any()).WasCalled(Times.Exactly(2));
    }

    [Test]
    public async Task PublishAsync_WhenStatusMatchesLastNotifiedStatus_DoesNotSendAgain()
    {
        // Arrange
        var mock = ISmtpSender.Mock();
        var optionsMonitor = CreateOptionsMonitor(options =>
            options.RecoveryConfirmationDelay = TimeSpan.FromMinutes(5)
        );
        var timeProvider = new FakeTimeProvider(TestStart);
        var publisher = new EmailHealthCheckPublisher(TestName, mock, optionsMonitor, timeProvider);

        // Act
        await publisher.PublishAsync(CreateReport(HealthStatus.Degraded), CancellationToken.None); // worsening, sends
        await publisher.PublishAsync(CreateReport(HealthStatus.Degraded), CancellationToken.None); // matches last-notified, no-op

        // Assert
        mock.SendAsync(Any(), Any(), Any()).WasCalled(Times.Once);
    }

    private static HealthReport CreateReport(HealthStatus status) =>
        new(new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal), status, TimeSpan.Zero);

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
