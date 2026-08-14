using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EmailWorkerService.Services.Integration;

namespace EmailWorkerService.Services
{
    public class EmailProcessor : IEmailProcessor
    {
        private readonly ILogger<EmailProcessor> _logger;
        private readonly IConfiguration _configuration;
        private readonly IFilesServiceClient _filesServiceClient;
        private readonly IDocumentServiceClient _documentServiceClient;

        public EmailProcessor(
            ILogger<EmailProcessor> logger,
            IConfiguration configuration,
            IFilesServiceClient filesServiceClient,
            IDocumentServiceClient documentServiceClient)
        {
            _logger = logger;
            _configuration = configuration;
            _filesServiceClient = filesServiceClient;
            _documentServiceClient = documentServiceClient;
        }

        public async Task ProcessIncomingEmailsAsync()
        {
            _logger.LogInformation("--- BẮT ĐẦU KIỂM TRA HỘP THƯ IMAP ---");

            // Fix cảnh báo null bằng cách cung cấp giá trị mặc định rỗng
            var host = _configuration["ImapSettings:Host"] ?? string.Empty;
            var port = int.Parse(_configuration["ImapSettings:Port"] ?? "993");
            var useSsl = bool.Parse(_configuration["ImapSettings:UseSSL"] ?? "true");
            var email = _configuration["ImapSettings:Email"] ?? string.Empty;
            var password = _configuration["ImapSettings:Password"] ?? string.Empty;

            using var client = new ImapClient();
            try
            {
                await client.ConnectAsync(host, port, useSsl);
                await client.AuthenticateAsync(email, password);

                var inbox = client.Inbox;

                // Chốt chặn 1: Đảm bảo server có hỗ trợ thư mục Inbox
                if (inbox == null)
                {
                    _logger.LogError("Máy chủ IMAP không hỗ trợ hoặc không tìm thấy thư mục Inbox.");
                    return;
                }

                await inbox.OpenAsync(FolderAccess.ReadWrite);

                var unreadEmails = await inbox.SearchAsync(SearchQuery.NotSeen);
                _logger.LogInformation($"Tìm thấy {unreadEmails.Count} email chưa đọc.");

                foreach (var uid in unreadEmails)
                {
                    var message = await inbox.GetMessageAsync(uid);

                    // Chốt chặn 2: Đảm bảo message không bị rỗng khi tải về
                    if (message == null)
                    {
                        continue;
                    }

                    _logger.LogInformation($"Đang xử lý email: {message.Subject}");

                    var pdfAttachments = message.Attachments
                        .OfType<MimePart>()
                        .Where(a => a.ContentType?.MimeType?.Equals("application/pdf", StringComparison.OrdinalIgnoreCase) == true)
                        .ToList();

                    if (pdfAttachments.Any())
                    {
                        foreach (var attachment in pdfAttachments)
                        {
                            // Fix cảnh báo file name null
                            var safeFileName = attachment.FileName ?? $"Bản_Fax_Khong_Ten_{Guid.NewGuid().ToString().Substring(0, 6)}.pdf";

                            _logger.LogInformation($"Tìm thấy file Fax PDF: {safeFileName}");

                            // Kiểm tra an toàn trước khi giải mã file
                            if (attachment.Content == null)
                            {
                                _logger.LogWarning($"File {safeFileName} không có nội dung, bỏ qua.");
                                continue;
                            }

                            using var memoryStream = new MemoryStream();
                            await attachment.Content.DecodeToAsync(memoryStream);
                            memoryStream.Position = 0;

                            var fileBytes = memoryStream.ToArray();

                            var fileId = await _filesServiceClient.UploadFileAsync(safeFileName, fileBytes);

                            if (!string.IsNullOrEmpty(fileId))
                            {
                                _logger.LogInformation($"File đã được lưu trữ an toàn với ID: {fileId}");

                                var title = $"[Fax] Công văn đến từ hộp thư ({message.Date.DateTime:dd/MM/yyyy})";

                                var docId = await _documentServiceClient.RegisterIncomingDocumentAsync(
                                    title: title,
                                    fileId: fileId,
                                    receivedAt: message.Date.UtcDateTime);

                                if (!string.IsNullOrEmpty(docId))
                                {
                                    _logger.LogInformation($"Đã xử lý trọn vẹn bản fax, tạo mã số công văn thành công!");
                                }
                            }
                            else
                            {
                                _logger.LogError($"Thất bại khi upload file {safeFileName} sang FilesService.");
                            }
                        }
                    }

                    await inbox.AddFlagsAsync(uid, MessageFlags.Seen, true);
                }

                await client.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xảy ra trong quá trình đọc email IMAP.");
            }

            _logger.LogInformation("--- HOÀN TẤT KIỂM TRA HỘP THƯ ---");
        }
    }
}