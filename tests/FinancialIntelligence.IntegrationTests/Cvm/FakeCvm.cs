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
    private readonly Dictionary<string, Func<HttpResponseMessage>> _files = new(StringComparer.Ordinal);

    public List<SentRequest> Requests { get; } = [];

    public void Reply(Func<HttpResponseMessage> reply) => _replies.Enqueue(reply);

    /// <summary>Answers every request for this file name, ahead of the queue.</summary>
    public void Serve(string fileName, Func<HttpResponseMessage> reply) => _files[fileName] = reply;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var ifNoneMatch = request.Headers.TryGetValues("If-None-Match", out var values)
            ? string.Join(",", values)
            : null;
        Requests.Add(new SentRequest(request.RequestUri, ifNoneMatch));

        var fileName = Path.GetFileName(request.RequestUri!.AbsolutePath);
        var reply = _files.TryGetValue(fileName, out var served) ? served : _replies.Dequeue();
        return Task.FromResult(reply());
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
