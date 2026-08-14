using EmailWorkerService.Models;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace EmailWorkerService
{
    public class EmailWorker : BackgroundService
    {
        private readonly ILogger<EmailWorker> _logger;
        private readonly EmailSettings _emailSettings;
        private readonly IHttpClientFactory _httpClientFactory;

        // 1. Tiêm (Inject) IHttpClientFactory vào constructor
        public EmailWorker(ILogger<EmailWorker> logger, IOptions<EmailSettings> emailSettings, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _emailSettings = emailSettings.Value;
            _httpClientFactory = httpClientFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Tiến trình quét Email khởi động lúc: {time}", DateTimeOffset.Now);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Chuyển sang gọi hàm Async vì HttpClient yêu cầu dùng await
                    await ReadUnreadEmailsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi kết nối IMAP hoặc quá trình đọc thư thất bại.");
                }

                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }

        private async Task ReadUnreadEmailsAsync()
        {
            using var client = new ImapClient();

            client.Connect(_emailSettings.ImapServer, _emailSettings.ImapPort, true);
            client.Authenticate(_emailSettings.EmailAddress, _emailSettings.AppPassword);

            var inbox = client.Inbox;
            if (inbox == null)
            {
                _logger.LogError("Máy chủ IMAP không hỗ trợ hoặc không tìm thấy thư mục Inbox.");
                return; // Dừng tiến trình nếu không có hộp thư
            }

            await inbox.OpenAsync(FolderAccess.ReadWrite);

            var unreadUids = inbox.Search(SearchQuery.NotSeen);

            if (unreadUids.Count > 0)
            {
                _logger.LogInformation($"[!] Cảnh báo: Phát hiện {unreadUids.Count} thư mới chưa đọc.");
            }

            foreach (var uid in unreadUids)
            {
                var message = inbox.GetMessage(uid);
                _logger.LogInformation($"---> Đang đọc thư: [{message.Subject}] - Gửi từ: {message.From}");

                foreach (var attachment in message.Attachments)
                {
                    if (attachment is MimePart part && !string.IsNullOrEmpty(part.FileName)
                        && part.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation($"     + Phát hiện công văn PDF: {part.FileName}");

                        // 2. Chuyển đổi dữ liệu file từ email sang MemoryStream
                        using var memoryStream = new MemoryStream();
                        part.Content.DecodeTo(memoryStream);
                        memoryStream.Position = 0; // Reset con trỏ stream về đầu để chuẩn bị đọc

                        // 3. Chuẩn bị gói dữ liệu gửi đi (Multipart/form-data)
                        var httpClient = _httpClientFactory.CreateClient();
                        using var formContent = new MultipartFormDataContent();

                        var streamContent = new StreamContent(memoryStream);
                        streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");

                        // Chú ý: Chữ "Files" ở đây phải khớp đúng với tên tham số trong API /api/Files/upload
                        formContent.Add(streamContent, "Files", part.FileName);

                        // 4. Bắn API sang files-service (Lưu ý đổi lại port nếu files-service chạy port khác 5004)
                        string filesServiceUrl = "http://localhost:5004/api/Files/upload";
                        _logger.LogInformation($"     => Đang gửi file sang {filesServiceUrl}...");

                        var response = await httpClient.PostAsync(filesServiceUrl, formContent);

                        if (response.IsSuccessStatusCode)
                        {
                            string responseBody = await response.Content.ReadAsStringAsync();
                            _logger.LogInformation($"     [Thành công] Phản hồi từ Files-Service: {responseBody}");
                        }
                        else
                        {
                            _logger.LogError($"     [Thất bại] Status Code: {response.StatusCode}. Vui lòng kiểm tra lại files-service.");
                        }
                    }
                }

                // 5. Đánh dấu email đã xử lý xong thành ĐÃ ĐỌC
                inbox.AddFlags(uid, MessageFlags.Seen, true);
            }

            client.Disconnect(true);
        }
    }
}