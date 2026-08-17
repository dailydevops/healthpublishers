# NetEvolve.HealthPublishers.MicrosoftTeams.Console

Interactive dev tool to manually exercise `NetEvolve.HealthPublishers.MicrosoftTeams` against a real
Microsoft Teams incoming webhook, including its recovery-confirmation debounce behavior.

## Setup

```powershell
dotnet user-secrets set "MicrosoftTeams:WebhookUrl" "https://example.webhook.office.com/webhookb2/..."
```

## Run

```powershell
dotnet run
```

Choose `1`/`2`/`3` to publish a `Healthy`/`Degraded`/`Unhealthy` report, `t` to fast-forward the
(fake) clock by 5 minutes, `q` to quit.
