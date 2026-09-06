using System.Net;

namespace NotificationService.Services
{
    public static class EmailTemplateHelper
    {
        public static string BuildDocumentNotificationHtml(string title, string message, string? documentNumber, string? actionUrl)
        {
            var cleanTitle = WebUtility.HtmlEncode(title);
            var cleanMessage = WebUtility.HtmlEncode(message);
            var cleanDocNum = WebUtility.HtmlEncode(documentNumber ?? "Đang cập nhật");
            var cleanUrl = !string.IsNullOrWhiteSpace(actionUrl) ? actionUrl : "http://localhost:3000";

            return $@"
<!DOCTYPE html>
<html lang=""vi"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{cleanTitle}</title>
</head>
<body style=""margin: 0; padding: 0; background-color: #f4f6f9; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #333333;"">
    <table border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""table-layout: fixed;"">
        <tr>
            <td align=""center"" style=""padding: 30px 15px;"">
                <table border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""max-width: 600px; background-color: #ffffff; border-radius: 8px; overflow: hidden; box-shadow: 0 4px 12px rgba(0,0,0,0.08);"">
                    <!-- Header -->
                    <tr>
                        <td style=""background: linear-gradient(135deg, #1e40af 0%, #3b82f6 100%); padding: 25px 30px; text-align: left;"">
                            <h1 style=""margin: 0; font-size: 20px; font-weight: 700; color: #ffffff; letter-spacing: -0.3px;"">
                                📋 HỆ THỐNG VĂN THƯ & CÔNG VĂN SỐ
                            </h1>
                            <p style=""margin: 6px 0 0 0; font-size: 13px; color: #dbeafe;"">Hệ thống thông báo tự động (Notification Service)</p>
                        </td>
                    </tr>
                    <!-- Content -->
                    <tr>
                        <td style=""padding: 30px;"">
                            <h2 style=""margin: 0 0 16px 0; font-size: 18px; color: #1e293b; font-weight: 600;"">
                                {cleanTitle}
                            </h2>
                            <p style=""margin: 0 0 20px 0; font-size: 15px; line-height: 1.6; color: #475569;"">
                                {cleanMessage}
                            </p>
                            
                            <!-- Card Info -->
                            <table border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""background-color: #f8fafc; border: 1px solid #e2e8f0; border-radius: 6px; margin-bottom: 25px;"">
                                <tr>
                                    <td style=""padding: 15px 20px;"">
                                        <p style=""margin: 0 0 8px 0; font-size: 13px; color: #64748b;"">
                                            <strong>Số ký hiệu:</strong> <span style=""color: #0f172a; font-weight: 600;"">{cleanDocNum}</span>
                                        </p>
                                        <p style=""margin: 0; font-size: 13px; color: #64748b;"">
                                            <strong>Thời gian:</strong> <span style=""color: #0f172a;"">{System.DateTime.UtcNow:dd/MM/yyyy HH:mm} (UTC)</span>
                                        </p>
                                    </td>
                                </tr>
                            </table>

                            <!-- CTA Button -->
                            <table border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%"">
                                <tr>
                                    <td align=""center"" style=""padding: 10px 0 20px 0;"">
                                        <a href=""{cleanUrl}"" target=""_blank"" style=""display: inline-block; background-color: #2563eb; color: #ffffff; font-size: 15px; font-weight: 600; text-decoration: none; padding: 12px 30px; border-radius: 6px; box-shadow: 0 2px 4px rgba(37,99,235,0.3);"">
                                            Xem Chi Tiết Văn Bản →
                                        </a>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                    <!-- Footer -->
                    <tr>
                        <td style=""background-color: #f8fafc; border-top: 1px solid #e2e8f0; padding: 20px 30px; text-align: center; font-size: 12px; color: #94a3b8; line-height: 1.5;"">
                            <p style=""margin: 0;"">Email này được gửi tự động từ Hệ thống Quản trị Văn thư Doanh nghiệp.</p>
                            <p style=""margin: 4px 0 0 0;"">Vui lòng không trả lời trực tiếp email này.</p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
        }
    }
}
