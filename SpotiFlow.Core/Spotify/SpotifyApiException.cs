using System.Net;

namespace SpotiFlow.Spotify;

public class SpotifyApiException : Exception
{
    public HttpStatusCode StatusCode { get; }

    public TimeSpan? RetryAfter { get; }

    public SpotifyApiException(HttpStatusCode statusCode, string message, TimeSpan? retryAfter = null)
        : base(message)
    {
        StatusCode = statusCode;
        RetryAfter = retryAfter;
    }
}
