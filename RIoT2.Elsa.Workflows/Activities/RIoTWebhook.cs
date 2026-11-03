using System.Net.Http.Headers;
using System.Text;
using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Activities.Flowchart.Attributes;
using Elsa.Workflows.Attributes;
using Elsa.Workflows.Models;
using Elsa.Workflows.UIHints;
using RIoT2.Core.Security;

namespace RIoT2.Elsa.Server.RIoT.Activities
{
    /// <summary>
    /// Calls a webhook: continues on <c>Done</c> for a 2xx answer, and on <c>Failed</c> for any other status, a timeout,
    /// a network error or a URL the outbound policy refuses (design 7.5, purpose <c>webhook</c>), with the reason in
    /// <c>Error</c>. Elsa's own "HTTP Request" activity remains for anything more elaborate.
    /// </summary>
    [Activity(
        Namespace = "RIoT2",
        Category = "RIoT",
        Description = "Calls a webhook (HTTP) and continues on Done or Failed",
        DisplayName = "RIoT Webhook",
        Kind = ActivityKind.Task)]
    [FlowNode(DoneOutcome, FailedOutcome)]
    public class RIoTWebhook : Activity
    {
        public const string DoneOutcome = "Done";
        public const string FailedOutcome = "Failed";
        public const string HttpClientName = "riot2-webhook";
        /// <summary>The outbound policy purpose of webhook calls.</summary>
        public const string OutboundPurpose = "webhook";
        public const int MaxTimeoutSeconds = 60;
        /// <summary>The longest response body kept in <c>ResponseBody</c>, in characters.</summary>
        public const int MaxResponseLength = 64 * 1024;

        [Input(Description = "The URL, http or https")]
        public Input<string> Url { get; set; } = new("");

        [Input(Description = "The HTTP method", UIHint = InputUIHints.DropDown, Options = new[] { "POST", "PUT", "PATCH", "GET", "DELETE" }, DefaultValue = "POST")]
        public Input<string> Method { get; set; } = new("POST");

        [Input(Description = "The request body; not sent with GET and DELETE")]
        public Input<string?> Body { get; set; } = default!;

        [Input(Description = "The content type of the body", DefaultValue = "application/json")]
        public Input<string> ContentType { get; set; } = new("application/json");

        [Input(Description = "Seconds to wait for the answer, 1 to 60", DefaultValue = 10)]
        public Input<int> TimeoutSeconds { get; set; } = new(10);

        [Output(Description = "The HTTP status code, or 0 without an answer")]
        public Output<int> StatusCode { get; set; } = default!;

        [Output(Description = "The response body (at most 64 KiB)")]
        public Output<string?> ResponseBody { get; set; } = default!;

        [Output(Description = "Why the call failed, if it did")]
        public Output<string?> Error { get; set; } = default!;

        protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
        {
            var url = (Url.GetOrDefault(context) ?? "").Trim();
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                throw new InvalidOperationException($"RIoT Webhook needs an http or https URL, not '{url}'.");
            var timeout = TimeoutSeconds.GetOrDefault(context);
            if (timeout is < 1 or > MaxTimeoutSeconds)
                throw new InvalidOperationException($"The timeout must be 1 to {MaxTimeoutSeconds} seconds, not {timeout}.");
            var method = new HttpMethod((Method.GetOrDefault(context) ?? "POST").Trim().ToUpperInvariant());

            using var request = new HttpRequestMessage(method, uri);
            var body = Body?.GetOrDefault(context);
            if (body != null && method != HttpMethod.Get && method != HttpMethod.Delete)
            {
                var contentType = ContentType.GetOrDefault(context);
                if (!MediaTypeHeaderValue.TryParse(string.IsNullOrWhiteSpace(contentType) ? "application/json" : contentType.Trim(), out var mediaType))
                    throw new InvalidOperationException($"RIoT Webhook: '{contentType}' is not a content type.");
                // The body is always sent as UTF-8.
                mediaType.CharSet = "utf-8";
                request.Content = new StringContent(body, Encoding.UTF8);
                request.Content.Headers.ContentType = mediaType;
            }

            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
            timeoutSource.CancelAfter(TimeSpan.FromSeconds(timeout));
            var client = context.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName);
            string? error = null;
            var status = 0;
            try
            {
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutSource.Token);
                status = (int)response.StatusCode;
                ResponseBody.Set(context, await ReadAsync(response, timeoutSource.Token));
                if (!response.IsSuccessStatusCode)
                    error = $"HTTP {status} {response.ReasonPhrase}".TrimEnd();
            }
            catch (OperationCanceledException) when (!context.CancellationToken.IsCancellationRequested)
            {
                error = $"No answer within {timeout} s";
            }
            catch (OutboundUrlRejectedException rejected)
            {
                error = rejected.Message;
            }
            catch (HttpRequestException failure)
            {
                error = failure.Message;
            }
            catch (IOException failure)
            {
                // The connection dropped while the body was read.
                error = failure.Message;
            }
            StatusCode.Set(context, status);
            Error.Set(context, error);
            if (error != null)
                context.GetRequiredService<ILogger<RIoTWebhook>>().LogWarning("RIoT Webhook {Method} {Host} failed: {Error}", method, uri.Host, error);
            await context.CompleteActivityWithOutcomesAsync(error == null ? DoneOutcome : FailedOutcome);
        }

        private static async Task<string> ReadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var buffer = new char[MaxResponseLength];
            var read = 0;
            int count;
            while (read < buffer.Length && (count = await reader.ReadAsync(buffer.AsMemory(read), cancellationToken)) > 0)
                read += count;
            return new string(buffer, 0, read);
        }
    }
}
