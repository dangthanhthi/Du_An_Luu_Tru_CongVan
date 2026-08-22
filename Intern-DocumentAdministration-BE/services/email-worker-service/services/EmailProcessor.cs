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
        private readonly IAiOcrServiceClient _aiOcrServiceClient;

        public EmailProcessor(
            ILogger<EmailProcessor> logger,
            IConfiguration configuration,
            IFilesServiceClient filesServiceClient,
            IDocumentServiceClient documentServiceClient,
            IAiOcrServiceClient aiOcrServiceClient)
        {
            _logger = logger;
            _configuration = configuration;
            _filesServiceClient = filesServiceClient;
            _documentServiceClient = documentServiceClient;
            _aiOcrServiceClient = aiOcrServiceClient;
        }

        public async Task ProcessIncomingEmailsAsync()
        {
            _logger.LogInformation("--- BẮT ĐẦU KIỂM TRA HỘP THƯ IMAP GMAIL ---");

            var host = _configuration["Email:Host"] 
                    ?? _configuration["Email__Host"] 
                    ?? _configuration["ImapSettings:Host"] 
                    ?? "imap.gmail.com";
            var port = int.Parse(_configuration["Email:Port"] 
                    ?? _configuration["Email__Port"] 
                    ?? _configuration["ImapSettings:Port"] 
                    ?? "993");
            var useSsl = bool.Parse(_configuration["Email:UseSSL"] 
                    ?? _configuration["ImapSettings:UseSSL"] 
                    ?? "true");
            var email = _configuration["Email:Username"] 
                     ?? _configuration["Email__Username"] 
                     ?? _configuration["ImapSettings:Email"] 
                     ?? string.Empty;
            var password = _configuration["Email:Password"] 
                        ?? _configuration["Email__Password"] 
                        ?? _configuration["ImapSettings:Password"] 
                        ?? string.Empty;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                _logger.LogWarning("Chưa cấu hình tài khoản Email/IMAP (Email:Username, Email:Password). Bỏ qua lượt quét.");
                return;
            }

            using var client = new ImapClient();
            try
            {
                await client.ConnectAsync(host, port, useSsl);
                await client.AuthenticateAsync(email, password);

                var inbox = client.Inbox;
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
                    if (message == null) continue;

                    var senderEmail = message.From.Mailboxes.FirstOrDefault()?.Address ?? message.From.ToString();
                    _logger.LogInformation($"Đang xử lý email: [{message.Subject}] - Người gửi: {senderEmail}");

                    var pdfAttachments = message.Attachments
                        .OfType<MimePart>()
                        .Where(a => a.ContentType?.MimeType?.Equals("application/pdf", StringComparison.OrdinalIgnoreCase) == true
                                 || (!string.IsNullOrEmpty(a.FileName) && a.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)))
                        .ToList();

                    if (pdfAttachments.Any())
                    {
                        foreach (var attachment in pdfAttachments)
                        {
                            var safeFileName = attachment.FileName ?? $"Ban_Scan_{Guid.NewGuid().ToString().Substring(0, 6)}.pdf";
                            _logger.LogInformation($"  + Phát hiện tài liệu PDF: {safeFileName}");

                            if (attachment.Content == null)
                            {
                                _logger.LogWarning($"File {safeFileName} không có nội dung, bỏ qua.");
                                continue;
                            }

                            using var memoryStream = new MemoryStream();
                            await attachment.Content.DecodeToAsync(memoryStream);
                            memoryStream.Position = 0;

                            var fileBytes = memoryStream.ToArray();

                            // 1. Lưu file vào FilesService
                            var fileIdString = await _filesServiceClient.UploadFileAsync(safeFileName, fileBytes);
                            if (string.IsNullOrEmpty(fileIdString) || !Guid.TryParse(fileIdString, out var fileIdGuid))
                            {
                                _logger.LogError($"Thất bại khi upload file {safeFileName} sang FilesService.");
                                continue;
                            }

                            _logger.LogInformation($"  + Đã upload file lên FilesService, FileId: {fileIdGuid}");

                            // 2. Gọi AI OCR Service phân tích nội dung, tìm Partner và Số hiệu công văn
                            var ocrResult = await _aiOcrServiceClient.AnalyzeDocumentAsync(fileIdGuid, senderEmail);
                            
                            var partnerId = ocrResult?.MatchedPartnerId;
                            var refNumber = ocrResult?.ExtractedReferenceNumber;
                            var subject = ocrResult?.ExtractedSubject;

                            // Ưu tiên trích yếu AI bóc tách từ văn bản -> Tiêu đề email -> Mặc định
                            var title = !string.IsNullOrWhiteSpace(subject)
                                ? subject.Trim()
                                : (!string.IsNullOrWhiteSpace(message.Subject) 
                                    ? message.Subject.Trim() 
                                    : $"[Fax] Công văn đến từ {senderEmail} ({message.Date.DateTime:dd/MM/yyyy})");

                            if (partnerId.HasValue)
                            {
                                _logger.LogInformation($"  + AI OCR nhận diện thành công Đối tác: {partnerId.Value} (Phương thức: {ocrResult?.MatchMethod}, Độ tin cậy: {ocrResult?.Confidence:P0})");
                            }
                            if (!string.IsNullOrWhiteSpace(refNumber))
                            {
                                _logger.LogInformation($"  + AI OCR bóc tách Số ký hiệu đối tác (Reference No.): {refNumber}");
                            }

                            // 3. Đăng ký công văn đến vào DocumentService
                            var docId = await _documentServiceClient.RegisterIncomingDocumentAsync(
                                title: title,
                                referenceNumber: refNumber,
                                partnerId: partnerId,
                                fileId: fileIdGuid,
                                receivedAt: message.Date.UtcDateTime);

                            if (!string.IsNullOrEmpty(docId))
                            {
                                _logger.LogInformation($"  [Thành công] Đã tạo công văn đến ID: {docId}");
                            }
                        }
                    }

                    // Đánh dấu đã đọc
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