namespace NetEvolve.HealthPublishers.Tests.Integration.Email;

using System;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// A minimal client for the parts of the Mailpit HTTP API (<c>/api/v1</c>) needed by the integration tests, used
/// to verify what the Email publisher actually sent over SMTP.
/// </summary>
internal sealed class MailpitApiClient : IDisposable
{
    private readonly HttpClient _client;

    public MailpitApiClient(Uri baseAddress) => _client = new HttpClient { BaseAddress = baseAddress };

    public void Dispose() => _client.Dispose();

    public async Task<int> CountMessagesAsync(string query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var summary = await FindMessagesAsync(query, cancellationToken).ConfigureAwait(false);
        return summary.Messages.Length;
    }

    public async Task<MailpitMessage> FindSingleMessageAsync(string query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var summary = await FindMessagesAsync(query, cancellationToken).ConfigureAwait(false);

        if (summary.Messages.Length != 1)
        {
            throw new InvalidOperationException(
                $"Expected exactly one message matching query '{query}', found {summary.Messages.Length}."
            );
        }

        var id = summary.Messages[0].ID;

        using var messageResponse = await _client
            .GetAsync(new Uri($"api/v1/message/{id}", UriKind.Relative), cancellationToken)
            .ConfigureAwait(false);
        _ = messageResponse.EnsureSuccessStatusCode();

        await using var messageStream = await messageResponse
            .Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        var message = await JsonSerializer
            .DeserializeAsync<MailpitMessage>(messageStream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return message ?? throw new InvalidOperationException("The Mailpit message could not be deserialized.");
    }

    private async Task<MailpitMessagesSummary> FindMessagesAsync(string query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var uri = new Uri($"api/v1/search?query={Uri.EscapeDataString(query)}", UriKind.Relative);

        using var summaryResponse = await _client.GetAsync(uri, cancellationToken).ConfigureAwait(false);
        _ = summaryResponse.EnsureSuccessStatusCode();

        await using var summaryStream = await summaryResponse
            .Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        var summary = await JsonSerializer
            .DeserializeAsync<MailpitMessagesSummary>(summaryStream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return summary ?? new MailpitMessagesSummary();
    }
}

// The following DTOs are only instantiated via System.Text.Json deserialization, which the CA1812 analyzer
// cannot see through.
#pragma warning disable CA1812

internal sealed class MailpitMessagesSummary
{
    [JsonPropertyName("messages")]
    public MailpitMessageSummary[] Messages { get; set; } = [];
}

internal sealed class MailpitMessageSummary
{
    [JsonPropertyName("ID")]
    public string ID { get; set; } = string.Empty;
}

internal sealed class MailpitMessage
{
    [JsonPropertyName("Subject")]
    public string Subject { get; set; } = string.Empty;

    [JsonPropertyName("From")]
    public MailpitAddress? From { get; set; }

    [JsonPropertyName("To")]
    public MailpitAddress[] To { get; set; } = [];

    [JsonPropertyName("Text")]
    public string Text { get; set; } = string.Empty;
}

internal sealed class MailpitAddress
{
    [JsonPropertyName("Name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("Address")]
    public string Address { get; set; } = string.Empty;
}

#pragma warning restore CA1812
