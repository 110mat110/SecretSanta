using System.Net;
using System.Text;

namespace SecretSanta.ApiService.Services;

public class EmailOptions
{
    /// <summary>Brevo API key (Brevo → SMTP &amp; API → API Keys). Without it emails are only logged.</summary>
    public string? BrevoApiKey { get; set; }
    /// <summary>Sender address – must be verified in Brevo (Senders &amp; IP → Senders).</summary>
    public string? SenderEmail { get; set; }
    public string SenderName { get; set; } = "Secret Santa";
    /// <summary>Public URL of the web app, used for links in emails, e.g. https://secretsanta-web.onrender.com</summary>
    public string? SiteUrl { get; set; }
}

public record GiftAssignment(string ForName, string WishText);

public record AssignmentEmail(string ToEmail, string ToName, Guid GroupId, string GroupName, IReadOnlyList<GiftAssignment> Gifts);

/// <summary>Sends transactional emails through the Brevo HTTP API (SMTP ports are blocked on Render's free plan).</summary>
public class EmailService(HttpClient httpClient, Microsoft.Extensions.Options.IOptions<EmailOptions> options, ILogger<EmailService> logger)
{
    private readonly EmailOptions options = options.Value;

    /// <summary>Sends one personalised email per participant. Failures are logged and don't stop the others.</summary>
    public async Task SendAssignmentsAsync(IEnumerable<AssignmentEmail> emails, CancellationToken cancellationToken = default)
    {
        foreach (var email in emails)
        {
            try
            {
                await SendAssignmentAsync(email, cancellationToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogError(e, "Sending assignment email for group {GroupId} to {Email} failed", email.GroupId, email.ToEmail);
            }
        }
    }

    private async Task SendAssignmentAsync(AssignmentEmail email, CancellationToken cancellationToken)
    {
        var groupUrl = $"{(options.SiteUrl ?? "").TrimEnd('/')}/{email.GroupId}";
        var subject = $"🎅 Secret Santa: {email.GroupName} – komu kupuješ darček";

        if (string.IsNullOrWhiteSpace(options.BrevoApiKey) || string.IsNullOrWhiteSpace(options.SenderEmail))
        {
            logger.LogWarning("Email is not configured (Email:BrevoApiKey, Email:SenderEmail) – not sending to {Email}. Gifts: {Gifts}",
                email.ToEmail, string.Join("; ", email.Gifts.Select(g => $"{g.ForName}: {g.WishText}")));
            return;
        }

        var payload = new
        {
            sender = new { name = options.SenderName, email = options.SenderEmail },
            to = new[] { new { email = email.ToEmail, name = email.ToName } },
            subject,
            htmlContent = BuildHtml(email, groupUrl),
            textContent = BuildText(email, groupUrl),
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email")
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Add("api-key", options.BrevoApiKey);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"Brevo returned {(int)response.StatusCode}: {body}");
        }

        logger.LogInformation("Assignment email for group {GroupId} sent to {Email}", email.GroupId, email.ToEmail);
    }

    private static string BuildText(AssignmentEmail email, string groupUrl)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Ahoj {email.ToName},");
        sb.AppendLine();
        sb.AppendLine($"skupina „{email.GroupName}“ bola uzavretá a Santa už rozhodol!");
        sb.AppendLine("Darčeky, ktoré máš kúpiť:");
        foreach (var gift in email.Gifts)
        {
            sb.AppendLine($"  🎁 {gift.ForName}: {gift.WishText}");
        }
        sb.AppendLine();
        sb.AppendLine($"Skupina: {groupUrl}");
        sb.AppendLine();
        sb.AppendLine("Veselé Vianoce! 🎄");
        return sb.ToString();
    }

    private static string BuildHtml(AssignmentEmail email, string groupUrl)
    {
        static string E(string? s) => WebUtility.HtmlEncode(s ?? "");

        var items = new StringBuilder();
        foreach (var gift in email.Gifts)
        {
            items.Append($"""
                <tr><td style="padding:10px 14px;border-bottom:1px solid #f0e6d8;">
                  🎁 <strong>{E(gift.ForName)}</strong><br/>
                  <span style="color:#444;">{E(gift.WishText)}</span>
                </td></tr>
                """);
        }

        return $"""
            <!doctype html>
            <html><body style="margin:0;padding:24px;background:#f7f3ee;font-family:Arial,Helvetica,sans-serif;color:#222;">
              <table role="presentation" width="100%" style="max-width:560px;margin:0 auto;background:#fff;border-radius:12px;overflow:hidden;">
                <tr><td style="background:#b3202a;color:#fff;padding:20px 24px;font-size:20px;font-weight:bold;">🎅 Secret Santa</td></tr>
                <tr><td style="padding:24px;">
                  <p style="margin:0 0 12px;">Ahoj {E(email.ToName)},</p>
                  <p style="margin:0 0 16px;">skupina <strong>{E(email.GroupName)}</strong> bola uzavretá a Santa už rozhodol, kto komu daruje darčeky!</p>
                  <p style="margin:0 0 8px;font-weight:bold;">Darčeky, ktoré máš kúpiť:</p>
                  <table role="presentation" width="100%" style="border-collapse:collapse;background:#fffaf3;border-radius:8px;">{items}</table>
                  <p style="margin:24px 0;text-align:center;">
                    <a href="{E(groupUrl)}" style="display:inline-block;background:#1f6b3a;color:#fff;text-decoration:none;padding:12px 22px;border-radius:8px;font-weight:bold;">Otvoriť skupinu</a>
                  </p>
                  <p style="margin:0;color:#777;font-size:13px;">Veselé Vianoce! 🎄</p>
                </td></tr>
              </table>
            </body></html>
            """;
    }
}
