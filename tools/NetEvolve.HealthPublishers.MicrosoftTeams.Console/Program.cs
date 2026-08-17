using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Time.Testing;
using NetEvolve.HealthPublishers.MicrosoftTeams;

// Set via: dotnet user-secrets set "MicrosoftTeams:WebhookUrl" "https://example.webhook.office.com/webhookb2/..."
var configuration = new ConfigurationBuilder().AddUserSecrets<Program>().Build();

var webhookUrl = configuration["MicrosoftTeams:WebhookUrl"];
if (string.IsNullOrWhiteSpace(webhookUrl))
{
    Console.WriteLine(
        "No WebhookUrl configured. Set it via: dotnet user-secrets set \"MicrosoftTeams:WebhookUrl\" \"<your webhook URL>\""
    );
    return;
}

// FakeTimeProvider allows fast-forwarding time manually instead of waiting 5 real minutes.
var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);

var services = new ServiceCollection();
services
    .AddHealthChecks()
    .AddMicrosoftTeamsPublisher(options =>
    {
        options.WebhookUrl = new Uri(webhookUrl);
        options.SystemIdentifier = "manual-test";
        options.RecoveryConfirmationDelay = TimeSpan.FromMinutes(5L);
    });
services.AddSingleton<TimeProvider>(timeProvider);

var provider = services.BuildServiceProvider();
var publisher = provider.GetRequiredService<IHealthCheckPublisher>();

while (true)
{
    Console.WriteLine();
    Console.WriteLine("Choose status: [1] Healthy  [2] Degraded  [3] Unhealthy  [t] fast-forward +5min  [q] Quit");
    var input = Console.ReadLine();

    if (input == "q")
    {
        break;
    }

    if (input == "t")
    {
        timeProvider.Advance(TimeSpan.FromMinutes(5L));
        Console.WriteLine($"Time fast-forwarded to {timeProvider.GetUtcNow():O}");
        continue;
    }

    HealthStatus? status = input switch
    {
        "1" => HealthStatus.Healthy,
        "2" => HealthStatus.Degraded,
        "3" => HealthStatus.Unhealthy,
        _ => null,
    };

    if (status is null)
    {
        Console.WriteLine("Invalid input.");
        continue;
    }

    var report = new HealthReport(
        new Dictionary<string, HealthReportEntry>(StringComparer.Ordinal)
        {
            ["self"] = new HealthReportEntry(status.Value, "manual test", TimeSpan.FromMilliseconds(42L), null, null),
        },
        TimeSpan.FromMilliseconds(42L)
    );

    await publisher.PublishAsync(report, CancellationToken.None).ConfigureAwait(false);
    Console.WriteLine(
        $"PublishAsync({status}) called - the card only posts immediately on a worsening status; an improvement waits for RecoveryConfirmationDelay (simulate it with 't')."
    );
}
