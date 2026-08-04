using System.Security.Cryptography;
using System.Text;

// Verifies the HMAC Slack attaches to every request it sends.
// https://api.slack.com/authentication/verifying-requests-from-slack
static class SlackSignature
{
  // Slack's own guidance: treat anything older than five minutes as a replay.
  static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(5);

  public static bool IsValid(
    IHeaderDictionary headers,
    string body,
    string signingSecret,
    DateTimeOffset now)
  {
    var timestamp = headers["X-Slack-Request-Timestamp"].ToString();
    var signature = headers["X-Slack-Signature"].ToString();

    if (string.IsNullOrEmpty(timestamp) || string.IsNullOrEmpty(signature))
    {
      return false;
    }

    if (!long.TryParse(timestamp, out var unixSeconds))
    {
      return false;
    }

    // Duration() so a timestamp from the future is rejected too, not just a stale one.
    var age = (now - DateTimeOffset.FromUnixTimeSeconds(unixSeconds)).Duration();

    if (age > MaxAge)
    {
      return false;
    }

    return CryptographicOperations.FixedTimeEquals(
      Encoding.UTF8.GetBytes(Compute(signingSecret, timestamp, body)),
      Encoding.UTF8.GetBytes(signature)
    );
  }

  static string Compute(string signingSecret, string timestamp, string body)
  {
    var mac = HMACSHA256.HashData(
      key: Encoding.UTF8.GetBytes(signingSecret),
      source: Encoding.UTF8.GetBytes($"v0:{timestamp}:{body}")
    );

    return $"v0={Convert.ToHexString(mac).ToLowerInvariant()}";
  }
}
