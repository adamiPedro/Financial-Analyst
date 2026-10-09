using System.Net;
using System.Net.Http.Headers;

namespace FinancialIntelligence.IntegrationTests.Cvm;

/// <summary>
/// Stands in for dados.cvm.gov.br at the bottom of the HttpClient, below the
/// resilience handler, so retries and timeouts run exactly as configured.
/// </summary>
internal sealed class FakeCvm : HttpMessageHandler
{
    private readonly Queue<Func<HttpResponseMessage>> _replies = new();

    public List<SentRequest> Requests { get; } = [];

    public void Reply(Func<HttpResponseMessage> reply) => _replies.Enqueue(reply);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var ifNoneMatch = request.Headers.TryGetValues("If-None-Match", out var values)
            ? string.Join(",", values)
            : null;
        Requests.Add(new SentRequest(request.RequestUri, ifNoneMatch));

        return Task.FromResult(_replies.Dequeue()());
    }

    public static HttpResponseMessage Ok(byte[] body, string eTag)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) };
        response.Headers.ETag = EntityTagHeaderValue.Parse(eTag);
        return response;
    }

    public static HttpResponseMessage Status(HttpStatusCode status) => new(status);
}

internal sealed record SentRequest(Uri? Url, string? IfNoneMatch);
