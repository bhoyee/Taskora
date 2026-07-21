using System.Net;
using System.Text;
using TodoApp.Application.Abstractions;

namespace TodoApp.Application.Notifications;

public sealed record EmailDetail(string Label, string Value);

public static class TaskoraEmailTemplate
{
    public static NotificationEmailMessage Build(
        IReadOnlyCollection<string> recipients,
        string subject,
        string eyebrow,
        string title,
        string greeting,
        string intro,
        IReadOnlyCollection<EmailDetail> details,
        string actionText,
        string? actionUrl = null,
        string? secondaryNote = null)
    {
        var plain = BuildPlainText(
            greeting,
            intro,
            details,
            actionText,
            actionUrl,
            secondaryNote);
        var html = BuildHtml(
            eyebrow,
            title,
            greeting,
            intro,
            details,
            actionText,
            actionUrl,
            secondaryNote);
        return new NotificationEmailMessage(
            recipients,
            subject,
            plain,
            html);
    }

    private static string BuildPlainText(
        string greeting,
        string intro,
        IReadOnlyCollection<EmailDetail> details,
        string actionText,
        string? actionUrl,
        string? secondaryNote)
    {
        var body = new StringBuilder();
        body.AppendLine(greeting);
        body.AppendLine();
        body.AppendLine(intro);
        body.AppendLine();
        foreach (var detail in details)
        {
            body.AppendLine($"{detail.Label}: {detail.Value}");
        }

        body.AppendLine();
        body.AppendLine(actionText);
        if (!string.IsNullOrWhiteSpace(actionUrl))
        {
            body.AppendLine(actionUrl);
        }

        if (!string.IsNullOrWhiteSpace(secondaryNote))
        {
            body.AppendLine();
            body.AppendLine(secondaryNote);
        }

        body.AppendLine();
        body.AppendLine("Taskora");
        body.AppendLine("Organized workspaces, smarter delivery.");
        return body.ToString();
    }

    private static string BuildHtml(
        string eyebrow,
        string title,
        string greeting,
        string intro,
        IReadOnlyCollection<EmailDetail> details,
        string actionText,
        string? actionUrl,
        string? secondaryNote)
    {
        var detailRows = string.Join(
            string.Empty,
            details.Select(detail => $"""
            <tr>
              <td style="padding:10px 0;color:#667281;font-size:13px;border-bottom:1px solid #edf0f2;">{Encode(detail.Label)}</td>
              <td style="padding:10px 0;color:#26313d;font-size:13px;font-weight:700;text-align:right;border-bottom:1px solid #edf0f2;">{Encode(detail.Value)}</td>
            </tr>
            """));
        var action = string.IsNullOrWhiteSpace(actionUrl)
            ? $"""<p style="margin:0;color:#26313d;font-size:14px;line-height:1.6;">{Encode(actionText)}</p>"""
            : $"""
              <a href="{Encode(actionUrl)}" style="display:inline-block;background:#14a38b;color:#ffffff;text-decoration:none;font-weight:800;font-size:14px;padding:12px 18px;border-radius:6px;">{Encode(actionText)}</a>
              """;
        var note = string.IsNullOrWhiteSpace(secondaryNote)
            ? string.Empty
            : $"""<p style="margin:18px 0 0;color:#667281;font-size:13px;line-height:1.6;">{Encode(secondaryNote)}</p>""";

        return $"""
        <!doctype html>
        <html lang="en">
        <body style="margin:0;background:#f3f6f8;font-family:Arial,Helvetica,sans-serif;color:#26313d;">
          <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#f3f6f8;padding:28px 12px;">
            <tr>
              <td align="center">
                <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:640px;background:#ffffff;border:1px solid #dfe5ea;border-radius:10px;overflow:hidden;box-shadow:0 14px 34px rgba(24,32,43,.10);">
                  <tr>
                    <td style="background:#101820;padding:22px 26px;">
                      <div style="font-size:22px;font-weight:900;color:#ffffff;letter-spacing:.2px;">Taskora</div>
                      <div style="margin-top:5px;color:#80d7cb;font-size:12px;font-weight:800;text-transform:uppercase;letter-spacing:.7px;">{Encode(eyebrow)}</div>
                    </td>
                  </tr>
                  <tr>
                    <td style="padding:28px 26px 10px;">
                      <h1 style="margin:0;color:#17212b;font-size:24px;line-height:1.25;">{Encode(title)}</h1>
                      <p style="margin:18px 0 0;color:#26313d;font-size:15px;line-height:1.65;">{Encode(greeting)}</p>
                      <p style="margin:10px 0 0;color:#667281;font-size:14px;line-height:1.65;">{Encode(intro)}</p>
                    </td>
                  </tr>
                  <tr>
                    <td style="padding:10px 26px 24px;">
                      <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#fbfcfd;border:1px solid #e2e8ee;border-radius:8px;padding:4px 16px;">
                        {detailRows}
                      </table>
                    </td>
                  </tr>
                  <tr>
                    <td style="padding:0 26px 30px;">
                      {action}
                      {note}
                    </td>
                  </tr>
                  <tr>
                    <td style="background:#f8fafb;border-top:1px solid #e2e8ee;padding:18px 26px;color:#73808d;font-size:12px;line-height:1.6;">
                      <strong style="color:#26313d;">Taskora</strong><br>
                      Organized workspaces, smarter delivery.
                    </td>
                  </tr>
                </table>
              </td>
            </tr>
          </table>
        </body>
        </html>
        """;
    }

    private static string Encode(string value) =>
        WebUtility.HtmlEncode(value);
}
