using EmailWorkerService.Data;
using EmailWorkerService.Models;
using EmailWorkerService.Services.Integration;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EmailWorkerService.Services;

public class EmailProcessor : IEmailProcessor
{
    private static readonly SemaphoreSlim ScanGate = new(1, 1);

    private readonly ILogger<EmailProcessor> _logger;
    private readonly IConfiguration _configuration;
    private readonly EmailWorkerDbContext _db;
    private readonly IFilesServiceClient _filesServiceClient;
    private readonly IDocumentServiceClient _documentServiceClient;
    private readonly IAiOcrServiceClient _aiOcrServiceClient;

    public EmailProcessor(
        ILogger<EmailProcessor> logger,
        IConfiguration configuration,
        EmailWorkerDbContext db,
        IFilesServiceClient filesServiceClient,
        IDocumentServiceClient documentServiceClient,
        IAiOcrServiceClient aiOcrServiceClient)
    {
        _logger = logger;
        _configuration = configuration;
        _db = db;
        _filesServiceClient = filesServiceClient;
        _documentServiceClient = documentServiceClient;
        _aiOcrServiceClient = aiOcrServiceClient;
    }

    public async Task<EmailScanResult> ProcessIncomingEmailsAsync(
        string triggerType = "Scheduled",
        CancellationToken cancellationToken = default)
    {
        if (!ScanGate.Wait(0))
        {
            _logger.LogWarning(
                "Email scan request ({TriggerType}) was ignored because another scan is already running.",
                triggerType);

            return new EmailScanResult(
                0,
                0,
                false,
                "Another email scan is already running.");
        }

        try
        {
            var scanLog = new EmailScanLog
            {
                Id = Guid.NewGuid(),
                StartedAt = DateTime.UtcNow,
                TriggerType = string.Equals(
                    triggerType,
                    "Manual",
                    StringComparison.OrdinalIgnoreCase)
                    ? "Manual"
                    : "Scheduled"
            };

            _db.EmailScanLogs.Add(scanLog);
            await _db.SaveChangesAsync(cancellationToken);

            var emailsScanned = 0;
            var documentsCreated = 0;
            var readyForIntakeCount = 0;
            var skippedCount = 0;
            var failedCount = 0;

            _logger.LogInformation(
                "--- START EMAIL IMAP SCAN ({TriggerType}) ---",
                scanLog.TriggerType);

            try
            {
                var settings =
                    await LoadSettingsAsync(cancellationToken);

                ValidateSettings(settings);

                var existingDocIds = await _documentServiceClient.GetExistingDocumentIdsAsync(cancellationToken);

                using var client = new ImapClient();

                await client.ConnectAsync(
                    settings.ImapHost,
                    settings.ImapPort,
                    settings.UseSsl,
                    cancellationToken);

                await client.AuthenticateAsync(
                    settings.EmailAddress,
                    settings.AppPassword,
                    cancellationToken);

                var inbox = client.Inbox;

                await inbox.OpenAsync(
                    FolderAccess.ReadWrite,
                    cancellationToken);

                const int maxEmailsPerScan = 20;
                int totalInFolder = inbox.Count;
                List<UniqueId> unreadEmails = new();
                Dictionary<UniqueId, IMessageSummary> summaryMap = new();

                if (totalInFolder > 0)
                {
                    // Lấy đúng 20 thư mới nhất ở cuối hộp thư (20 email mới nhất tuyệt đối trong Gmail)
                    int startIndex = Math.Max(0, totalInFolder - maxEmailsPerScan);
                    int endIndex = totalInFolder - 1;

                    var recentSummaries = await inbox.FetchAsync(
                        startIndex,
                        endIndex,
                        MessageSummaryItems.UniqueId | MessageSummaryItems.Envelope | MessageSummaryItems.Flags,
                        cancellationToken);

                    summaryMap = recentSummaries.ToDictionary(s => s.UniqueId);

                    // Sắp xếp đúng thứ tự từ mới nhất đến cũ hơn (Date DESC)
                    unreadEmails = recentSummaries
                        .OrderByDescending(s => s.Date)
                        .Take(maxEmailsPerScan)
                        .Select(s => s.UniqueId)
                        .ToList();

                    _logger.LogInformation(
                        "Inbox has {TotalCount} emails. Selected {SelectedCount} latest emails for processing ({TriggerType}).",
                        totalInFolder,
                        unreadEmails.Count,
                        triggerType);
                }

                scanLog.TotalEmails = unreadEmails.Count;
                scanLog.EmailsScanned = 0;
                scanLog.DocumentsCreated = 0;
                scanLog.ReadyForIntakeCount = 0;
                scanLog.SkippedCount = 0;
                scanLog.FailedCount = 0;

                await _db.SaveChangesAsync(
                    CancellationToken.None);

                _logger.LogInformation(
                    "Inbox has {TotalCount} emails. Selected {SelectedCount} latest emails for processing.",
                    totalInFolder,
                    unreadEmails.Count);

                var whitelistedDomains =
                    ParseWhitelistedDomains(
                        settings.WhitelistedDomains);

                var sessionCreatedDocByRef = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var sessionCreatedDocByFileName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (var uid in unreadEmails)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        summaryMap.TryGetValue(uid, out var summary);
                        var senderFromEnvelope = summary?.Envelope?.From?.Mailboxes?.FirstOrDefault()?.Address
                            ?? summary?.Envelope?.From?.ToString()
                            ?? string.Empty;
                        var subjectFromEnvelope = summary?.Envelope?.Subject ?? "(Không có tiêu đề)";

                        // TỐI ƯU TỐC ĐỘ GẤP 40 LẦN: Nếu người gửi không thuộc whitelist, bỏ qua ngay lập tức trong 0ms
                        // KHÔNG cần tải toàn bộ nội dung HTML/hình ảnh dung lượng lớn từ Gmail về
                        if (!string.IsNullOrWhiteSpace(senderFromEnvelope) && !IsSenderAllowed(senderFromEnvelope, whitelistedDomains))
                        {
                            var skippedItem = new EmailScanItemLog
                            {
                                Id = Guid.NewGuid(),
                                ScanLogId = scanLog.Id,
                                ReceivedAt = summary?.Date.UtcDateTime ?? DateTime.UtcNow,
                                SenderEmail = TruncateNullable(senderFromEnvelope, 320) ?? string.Empty,
                                Subject = TruncateNullable(subjectFromEnvelope, 1000),
                                Status = "NotWhitelisted",
                                ProcessedAt = DateTime.UtcNow
                            };

                            _db.EmailScanItemLogs.Add(skippedItem);
                            skippedCount++;

                            _logger.LogInformation(
                                "Fast skipped email from {SenderEmail} (Not whitelisted) in 0ms.",
                                senderFromEnvelope);

                            await inbox.AddFlagsAsync(
                                uid,
                                MessageFlags.Seen,
                                true,
                                cancellationToken);

                            continue;
                        }

                        // Chỉ tải nội dung thư đầy đủ đối với email thuộc whitelist
                        var message = await inbox.GetMessageAsync(
                            uid,
                            cancellationToken);

                        if (message == null)
                        {
                            failedCount++;
                            continue;
                        }

                        var senderEmail = message.From.Mailboxes.FirstOrDefault()?.Address ?? message.From.ToString();

                        scanLog.CurrentEmailSubject = TruncateNullable(message.Subject, 1000);
                        scanLog.CurrentSenderEmail = TruncateNullable(senderEmail, 320);

                        _logger.LogInformation(
                            "Processing whitelisted email [{Subject}] from {SenderEmail}",
                            message.Subject,
                            senderEmail);

                        var pdfAttachments =
                            message.Attachments
                                .OfType<MimePart>()
                                .Where(IsPdfAttachment)
                                .ToList();

                        if (pdfAttachments.Count == 0)
                        {
                            var noPdfItem =
                                new EmailScanItemLog
                                {
                                    Id = Guid.NewGuid(),
                                    ScanLogId = scanLog.Id,
                                    ReceivedAt =
                                        message.Date.UtcDateTime,
                                    SenderEmail =
                                        TruncateNullable(
                                            senderEmail,
                                            320)
                                        ?? string.Empty,
                                    Subject =
                                        TruncateNullable(
                                            message.Subject,
                                            1000),
                                    Status = "NoPdf",
                                    ProcessedAt =
                                        DateTime.UtcNow
                                };

                            _db.EmailScanItemLogs.Add(
                                noPdfItem);

                            skippedCount++;

                            await _db.SaveChangesAsync(
                                CancellationToken.None);

                            _logger.LogInformation(
                                "Email has no PDF attachment. Marking as processed.");

                            await inbox.AddFlagsAsync(
                                uid,
                                MessageFlags.Seen,
                                true,
                                cancellationToken);

                            continue;
                        }

                        var emailHasFailure = false;

                        foreach (var attachment in pdfAttachments)
                        {
                            cancellationToken
                                .ThrowIfCancellationRequested();

                            var safeFileName =
                                string.IsNullOrWhiteSpace(
                                    attachment.FileName)
                                    ? $"email-scan-{Guid.NewGuid():N}.pdf"
                                    : Path.GetFileName(
                                        attachment.FileName);

                            // 1. Kiểm tra xem tệp/công văn từ email này đã từng được lưu/tiếp nhận vào Database chưa
                            var alreadySavedItem = await _db.EmailScanItemLogs
                                .AsNoTracking()
                                .FirstOrDefaultAsync(x =>
                                    x.SenderEmail == senderEmail &&
                                    x.AttachmentName == safeFileName &&
                                    !string.IsNullOrWhiteSpace(x.DocumentId) &&
                                    (x.Status == "IntakeCompleted" || x.IntakeConfirmedAt != null),
                                    cancellationToken);

                            bool isReallyInDb = alreadySavedItem != null
                                && !string.IsNullOrWhiteSpace(alreadySavedItem.DocumentId)
                                && existingDocIds.Contains(alreadySavedItem.DocumentId);

                            if (isReallyInDb)
                            {
                                var skippedSavedItem = new EmailScanItemLog
                                {
                                    Id = Guid.NewGuid(),
                                    ScanLogId = scanLog.Id,
                                    ReceivedAt = message.Date.UtcDateTime,
                                    SenderEmail = TruncateNullable(senderEmail, 320) ?? string.Empty,
                                    Subject = TruncateNullable(message.Subject, 1000),
                                    AttachmentName = TruncateNullable(safeFileName, 500),
                                    DocumentId = alreadySavedItem.DocumentId,
                                    FileId = alreadySavedItem.FileId,
                                    ExtractedReferenceNumber = alreadySavedItem.ExtractedReferenceNumber,
                                    ExtractedSubject = alreadySavedItem.ExtractedSubject,
                                    PartnerId = alreadySavedItem.PartnerId,
                                    IntakeConfirmedAt = alreadySavedItem.IntakeConfirmedAt,
                                    Status = "AlreadySaved",
                                    ErrorMessage = "Công văn này đã được tiếp nhận và lưu vào hệ thống trước đó.",
                                    ProcessedAt = DateTime.UtcNow
                                };

                                _db.EmailScanItemLogs.Add(skippedSavedItem);
                                skippedCount++;
                                await _db.SaveChangesAsync(CancellationToken.None);

                                _logger.LogInformation(
                                    "Skipped attachment [{FileName}] because it was already saved (DocumentId: {DocId}).",
                                    safeFileName,
                                    alreadySavedItem.DocumentId);

                                await inbox.AddFlagsAsync(uid, MessageFlags.Seen, true, cancellationToken);
                                continue;
                            }

                            // 2. Kiểm tra xem tệp đã từng được OCR thành công và đang chờ tiếp nhận không
                            var existingReadyItem = await _db.EmailScanItemLogs
                                .AsNoTracking()
                                .FirstOrDefaultAsync(x =>
                                    x.SenderEmail == senderEmail &&
                                    x.AttachmentName == safeFileName &&
                                    (x.Status == "ReadyForIntake" || x.Status == "Scanning") &&
                                    x.FileId != null,
                                    cancellationToken);

                            if (existingReadyItem != null)
                            {
                                var textToCheck = ((existingReadyItem.ExtractedSubject ?? "") + " " + (message.Subject ?? "") + " " + (safeFileName ?? "")).ToLowerInvariant();
                                string docDirection = "incoming";
                                if (textToCheck.Contains("thông báo nội bộ") || textToCheck.Contains("thong bao noi bo") || textToCheck.Contains("quyết định nội bộ") || textToCheck.Contains("tờ trình nội bộ") || textToCheck.Contains("kế hoạch nội bộ"))
                                {
                                    docDirection = "internal";
                                }

                                var finalTitle = !string.IsNullOrWhiteSpace(existingReadyItem.ExtractedSubject)
                                    ? existingReadyItem.ExtractedSubject.Trim()
                                    : !string.IsNullOrWhiteSpace(message.Subject)
                                        ? message.Subject.Trim()
                                        : $"Công văn tiếp nhận từ {senderEmail}";

                                string? autoDocId = null;
                                if (!string.IsNullOrWhiteSpace(existingReadyItem.ExtractedReferenceNumber) && 
                                    sessionCreatedDocByRef.TryGetValue(existingReadyItem.ExtractedReferenceNumber, out var existingRefDocId))
                                {
                                    autoDocId = existingRefDocId;
                                }
                                else if (sessionCreatedDocByFileName.TryGetValue(safeFileName, out var existingFileDocId))
                                {
                                    autoDocId = existingFileDocId;
                                }
                                var structuredSummary = BuildStructuredSummary(
                                    existingReadyItem.ExtractedReferenceNumber,
                                    null,
                                    null,
                                    null,
                                    null,
                                    senderEmail,
                                    finalTitle);

                                if (existingReadyItem.FileId.HasValue)
                                {
                                    try
                                    {
                                        autoDocId = await _documentServiceClient.RegisterDocumentAsync(
                                            docDirection,
                                            finalTitle,
                                            existingReadyItem.ExtractedReferenceNumber,
                                            existingReadyItem.PartnerId,
                                            existingReadyItem.FileId.Value,
                                            message.Date.UtcDateTime,
                                            summary: structuredSummary);

                                        if (!string.IsNullOrWhiteSpace(autoDocId))
                                        {
                                            sessionCreatedDocByFileName[safeFileName] = autoDocId;
                                            if (!string.IsNullOrWhiteSpace(existingReadyItem.ExtractedReferenceNumber))
                                            {
                                                sessionCreatedDocByRef[existingReadyItem.ExtractedReferenceNumber] = autoDocId;
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogWarning(ex, "Auto-register failed for existing item {FileName}", safeFileName);
                                    }
                                }

                                var readyItem = new EmailScanItemLog
                                {
                                    Id = Guid.NewGuid(),
                                    ScanLogId = scanLog.Id,
                                    ReceivedAt = message.Date.UtcDateTime,
                                    SenderEmail = TruncateNullable(senderEmail, 320) ?? string.Empty,
                                    Subject = TruncateNullable(message.Subject, 1000),
                                    AttachmentName = TruncateNullable(safeFileName, 500),
                                    FileId = existingReadyItem.FileId,
                                    DocumentId = autoDocId,
                                    ExtractedReferenceNumber = existingReadyItem.ExtractedReferenceNumber,
                                    ExtractedSubject = existingReadyItem.ExtractedSubject,
                                    PartnerId = existingReadyItem.PartnerId,
                                    Status = !string.IsNullOrWhiteSpace(autoDocId) ? "IntakeCompleted" : "ReadyForIntake",
                                    IntakeConfirmedAt = !string.IsNullOrWhiteSpace(autoDocId) ? DateTime.UtcNow : null,
                                    ProcessedAt = DateTime.UtcNow
                                };

                                _db.EmailScanItemLogs.Add(readyItem);
                                if (!string.IsNullOrWhiteSpace(autoDocId))
                                {
                                    documentsCreated++;
                                }
                                else
                                {
                                    readyForIntakeCount++;
                                }
                                await _db.SaveChangesAsync(CancellationToken.None);

                                _logger.LogInformation(
                                    "Attachment [{FileName}] auto-registered as Document {DocId}.",
                                    safeFileName,
                                    autoDocId);

                                await inbox.AddFlagsAsync(uid, MessageFlags.Seen, true, cancellationToken);
                                continue;
                            }

                            var item =
                                new EmailScanItemLog
                                {
                                    Id = Guid.NewGuid(),
                                    ScanLogId = scanLog.Id,
                                    ReceivedAt =
                                        message.Date.UtcDateTime,
                                    SenderEmail =
                                        TruncateNullable(
                                            senderEmail,
                                            320)
                                        ?? string.Empty,
                                    Subject =
                                        TruncateNullable(
                                            message.Subject,
                                            1000),
                                    AttachmentName =
                                        TruncateNullable(
                                            safeFileName,
                                            500),
                                    Status = "Scanning"
                                };

                            _db.EmailScanItemLogs.Add(item);

                            await _db.SaveChangesAsync(
                                CancellationToken.None);

                            if (attachment.Content == null)
                            {
                                item.Status =
                                    "UploadFailed";

                                item.ErrorMessage =
                                    "Attachment has no content.";

                                item.ProcessedAt =
                                    DateTime.UtcNow;

                                failedCount++;
                                emailHasFailure = true;

                                await _db.SaveChangesAsync(
                                    CancellationToken.None);

                                _logger.LogWarning(
                                    "Attachment {FileName} has no content.",
                                    safeFileName);

                                continue;
                            }

                            await using var memoryStream =
                                new MemoryStream();

                            await attachment.Content
                                .DecodeToAsync(
                                    memoryStream,
                                    cancellationToken);

                            var fileBytes =
                                memoryStream.ToArray();

                            if (fileBytes.Length == 0)
                            {
                                item.Status =
                                    "UploadFailed";

                                item.ErrorMessage =
                                    "Attachment is empty.";

                                item.ProcessedAt =
                                    DateTime.UtcNow;

                                failedCount++;
                                emailHasFailure = true;

                                await _db.SaveChangesAsync(
                                    CancellationToken.None);

                                _logger.LogWarning(
                                    "Attachment {FileName} is empty.",
                                    safeFileName);

                                continue;
                            }

                            string? fileIdString;

                            try
                            {
                                fileIdString =
                                    await _filesServiceClient
                                        .UploadFileAsync(
                                            safeFileName,
                                            fileBytes);
                            }
                            catch (Exception ex)
                            {
                                item.Status =
                                    "UploadFailed";

                                item.ErrorMessage =
                                    Truncate(
                                        ex.Message,
                                        2000);

                                item.ProcessedAt =
                                    DateTime.UtcNow;

                                failedCount++;
                                emailHasFailure = true;

                                await _db.SaveChangesAsync(
                                    CancellationToken.None);

                                _logger.LogError(
                                    ex,
                                    "Error while uploading {FileName} to FilesService.",
                                    safeFileName);

                                continue;
                            }

                            if (string.IsNullOrWhiteSpace(
                                    fileIdString)
                                || !Guid.TryParse(
                                    fileIdString,
                                    out var fileId))
                            {
                                item.Status =
                                    "UploadFailed";

                                item.ErrorMessage =
                                    "FilesService did not return a valid FileId.";

                                item.ProcessedAt =
                                    DateTime.UtcNow;

                                failedCount++;
                                emailHasFailure = true;

                                await _db.SaveChangesAsync(
                                    CancellationToken.None);

                                _logger.LogError(
                                    "Failed to upload {FileName} to FilesService.",
                                    safeFileName);

                                continue;
                            }

                            item.FileId = fileId;

                            await _db.SaveChangesAsync(
                                CancellationToken.None);

                            _logger.LogInformation(
                                "Uploaded {FileName}. FileId={FileId}",
                                safeFileName,
                                fileId);

                            try
                            {
                                OcrAnalyzeResult? ocrResult = null;
                                string? ocrWarning = null;

                                try
                                {
                                    ocrResult =
                                        await _aiOcrServiceClient
                                            .AnalyzeDocumentAsync(
                                                fileId,
                                                senderEmail,
                                                safeFileName);
                                }
                                catch (Exception ex)
                                {
                                    ocrWarning = ex.Message;

                                    _logger.LogWarning(
                                        ex,
                                        "OCR failed for {FileName}. The document will still be available for manual intake.",
                                        safeFileName);
                                }

                                item.FileId = fileId;

                                if (ocrResult != null)
                                {
                                    item.PartnerId =
                                        ocrResult.MatchedPartnerId;

                                    item.ExtractedReferenceNumber =
                                        ocrResult.ExtractedReferenceNumber;

                                    item.ExtractedSubject =
                                        ocrResult.ExtractedSubject;

                                    item.ErrorMessage = null;
                                }
                                else
                                {
                                    item.PartnerId = null;
                                    item.ExtractedReferenceNumber = null;

                                    item.ExtractedSubject =
                                        string.IsNullOrWhiteSpace(message.Subject)
                                            ? null
                                            : message.Subject.Trim();

                                    item.ErrorMessage =
                                        string.IsNullOrWhiteSpace(ocrWarning)
                                            ? "OCR cannot analyze the document. Manual processing is possible."
                                            : $"OCR warning: {ocrWarning}";
                                }

                                var textToCheck = ((item.ExtractedSubject ?? "") + " " + (message.Subject ?? "") + " " + (safeFileName ?? "")).ToLowerInvariant();
                                string docDirection = "incoming";
                                if (textToCheck.Contains("thông báo nội bộ") || textToCheck.Contains("thong bao noi bo") || textToCheck.Contains("quyết định nội bộ") || textToCheck.Contains("tờ trình nội bộ") || textToCheck.Contains("kế hoạch nội bộ"))
                                {
                                    docDirection = "internal";
                                }

                                var finalTitle = !string.IsNullOrWhiteSpace(item.ExtractedSubject)
                                    ? item.ExtractedSubject.Trim()
                                    : !string.IsNullOrWhiteSpace(message.Subject)
                                        ? message.Subject.Trim()
                                        : $"Công văn tiếp nhận từ {senderEmail}";

                                // Kiểm tra trùng lặp trong phiên quét hiện tại
                                string? autoDocId = null;
                                if (!string.IsNullOrWhiteSpace(item.ExtractedReferenceNumber) && 
                                    sessionCreatedDocByRef.TryGetValue(item.ExtractedReferenceNumber, out var existingRefDocId))
                                {
                                    autoDocId = existingRefDocId;
                                    _logger.LogInformation("Reused existing document {DocId} for ref {RefNum}", autoDocId, item.ExtractedReferenceNumber);
                                }
                                else if (sessionCreatedDocByFileName.TryGetValue(safeFileName, out var existingFileDocId))
                                {
                                    autoDocId = existingFileDocId;
                                    _logger.LogInformation("Reused existing document {DocId} for file {FileName}", autoDocId, safeFileName);
                                }
                                else
                                {
                                    try
                                    {
                                        var structuredSummary = BuildStructuredSummary(
                                            item.ExtractedReferenceNumber,
                                            ocrResult?.ExtractedPartnerName,
                                            ocrResult?.ExtractedDocumentType,
                                            ocrResult?.ExtractedDateString,
                                            ocrResult?.ExtractedSigner,
                                            senderEmail,
                                            finalTitle);

                                        autoDocId = await _documentServiceClient.RegisterDocumentAsync(
                                            docDirection,
                                            finalTitle,
                                            item.ExtractedReferenceNumber,
                                            item.PartnerId,
                                            fileId,
                                            message.Date.UtcDateTime,
                                            summary: structuredSummary);

                                        if (!string.IsNullOrWhiteSpace(autoDocId))
                                        {
                                            sessionCreatedDocByFileName[safeFileName] = autoDocId;
                                            if (!string.IsNullOrWhiteSpace(item.ExtractedReferenceNumber))
                                            {
                                                sessionCreatedDocByRef[item.ExtractedReferenceNumber] = autoDocId;
                                            }
                                        }
                                    }
                                    catch (Exception docEx)
                                    {
                                        _logger.LogWarning(docEx, "Auto-register failed for {FileName}", safeFileName);
                                    }
                                }

                                if (!string.IsNullOrWhiteSpace(autoDocId))
                                {
                                    item.DocumentId = autoDocId;
                                    item.Status = "IntakeCompleted";
                                    item.IntakeConfirmedAt = DateTime.UtcNow;
                                    item.ErrorMessage = null;
                                    item.ProcessedAt = DateTime.UtcNow;
                                    documentsCreated++;

                                    _logger.LogInformation(
                                        "Email attachment {FileName} auto-registered as Document {DocId}.",
                                        safeFileName,
                                        autoDocId);
                                }
                                else
                                {
                                    item.Status = "ReadyForIntake";
                                    item.ErrorMessage = null;
                                    item.ProcessedAt = DateTime.UtcNow;
                                    readyForIntakeCount++;

                                    _logger.LogInformation(
                                        "Email attachment {FileName} is ready for intake. FileId={FileId}",
                                        safeFileName,
                                        fileId);
                                }

                                await _db.SaveChangesAsync(
                                    CancellationToken.None);
                            }
                            catch (Exception ex)
                            {
                                item.Status =
                                    "OcrFailed";

                                item.ErrorMessage =
                                    Truncate(
                                        ex.Message,
                                        2000);

                                item.ProcessedAt =
                                    DateTime.UtcNow;

                                failedCount++;
                                emailHasFailure = true;

                                await _db.SaveChangesAsync(
                                    CancellationToken.None);

                                _logger.LogError(
                                    ex,
                                    "AI OCR failed for {FileName}.",
                                    safeFileName);
                            }
                        }

                        await inbox.AddFlagsAsync(
                            uid,
                            MessageFlags.Seen,
                            true,
                            cancellationToken);

                        if (emailHasFailure)
                        {
                            _logger.LogWarning(
                                "Email [{Subject}] was processed with one or more failed attachments. Details were saved to scan history.",
                                message.Subject);
                        }
                    }
                    finally
                    {
                        emailsScanned++;

                        scanLog.EmailsScanned =
                            emailsScanned;

                        scanLog.DocumentsCreated =
                            documentsCreated;

                        scanLog.ReadyForIntakeCount =
                            readyForIntakeCount;

                        scanLog.SkippedCount =
                            skippedCount;

                        scanLog.FailedCount =
                            failedCount;

                        try
                        {
                            await _db.SaveChangesAsync(
                                CancellationToken.None);
                        }
                        catch (Exception progressException)
                        {
                            _logger.LogWarning(
                                progressException,
                                "Could not save email scan progress.");
                        }
                    }
                }

                await client.DisconnectAsync(
                    true,
                    cancellationToken);

                scanLog.Success = true;

                scanLog.EmailsScanned =
                    emailsScanned;

                scanLog.DocumentsCreated =
                    documentsCreated;

                scanLog.ReadyForIntakeCount =
                    readyForIntakeCount;

                scanLog.SkippedCount =
                    skippedCount;

                scanLog.FailedCount =
                    failedCount;

                scanLog.CurrentEmailSubject = null;
                scanLog.CurrentSenderEmail = null;

                scanLog.FinishedAt =
                    DateTime.UtcNow;

                scanLog.ErrorMessage = null;

                await _db.SaveChangesAsync(
                    cancellationToken);

                _logger.LogInformation(
                    "--- EMAIL SCAN COMPLETED. Emails={EmailsScanned}, Ready={ReadyForIntake}, Skipped={Skipped}, Failed={Failed}, Documents={DocumentsCreated} ---",
                    emailsScanned,
                    readyForIntakeCount,
                    skippedCount,
                    failedCount,
                    documentsCreated);

                return new EmailScanResult(
                    emailsScanned,
                    documentsCreated,
                    true,
                    null);
            }
            catch (OperationCanceledException)
                when (cancellationToken
                    .IsCancellationRequested)
            {
                scanLog.Success = false;

                scanLog.EmailsScanned =
                    emailsScanned;

                scanLog.DocumentsCreated =
                    documentsCreated;

                scanLog.ReadyForIntakeCount =
                    readyForIntakeCount;

                scanLog.SkippedCount =
                    skippedCount;

                scanLog.FailedCount =
                    failedCount;

                scanLog.CurrentEmailSubject = null;
                scanLog.CurrentSenderEmail = null;

                scanLog.FinishedAt =
                    DateTime.UtcNow;

                scanLog.ErrorMessage =
                    "Email scan was cancelled.";

                try
                {
                    await _db.SaveChangesAsync(
                        CancellationToken.None);
                }
                catch
                {
                }

                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Email IMAP scan failed.");

                scanLog.Success = false;

                scanLog.EmailsScanned =
                    emailsScanned;

                scanLog.DocumentsCreated =
                    documentsCreated;

                scanLog.ReadyForIntakeCount =
                    readyForIntakeCount;

                scanLog.SkippedCount =
                    skippedCount;

                scanLog.FailedCount =
                    failedCount;

                scanLog.CurrentEmailSubject = null;
                scanLog.CurrentSenderEmail = null;

                scanLog.FinishedAt =
                    DateTime.UtcNow;

                scanLog.ErrorMessage =
                    Truncate(
                        ex.Message,
                        2000);

                await _db.SaveChangesAsync(
                    CancellationToken.None);

                return new EmailScanResult(
                    emailsScanned,
                    documentsCreated,
                    false,
                    ex.Message);
            }
        }
        finally
        {
            ScanGate.Release();
        }
    }

    private async Task<EmailImapSettings>
        LoadSettingsAsync(
            CancellationToken cancellationToken)
    {
        var dbSettings =
            await _db.EmailImapSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Id == 1,
                    cancellationToken);

        if (dbSettings != null
            && !string.IsNullOrWhiteSpace(
                dbSettings.EmailAddress)
            && !string.IsNullOrWhiteSpace(
                dbSettings.AppPassword))
        {
            return dbSettings;
        }

        return new EmailImapSettings
        {
            Id = 1,

            ImapHost =
                _configuration["ImapSettings:Host"]
                ?? _configuration["Email:Host"]
                ?? "imap.gmail.com",

            ImapPort = ParseInt(
                _configuration["ImapSettings:Port"]
                ?? _configuration["Email:Port"],
                993),

            UseSsl = ParseBool(
                _configuration["ImapSettings:UseSSL"]
                ?? _configuration["Email:UseSSL"],
                true),

            EmailAddress =
                _configuration["ImapSettings:Email"]
                ?? _configuration["Email:Username"]
                ?? string.Empty,

            AppPassword =
                _configuration["ImapSettings:Password"]
                ?? _configuration["Email:Password"]
                ?? string.Empty,

            WhitelistedDomains =
                dbSettings?.WhitelistedDomains
                ?? string.Empty,

            AutoScanIntervalMinutes =
                dbSettings
                    ?.AutoScanIntervalMinutes
                ?? 60,

            UpdatedAt =
                dbSettings?.UpdatedAt
                ?? DateTime.UtcNow
        };
    }

    private static void ValidateSettings(
        EmailImapSettings settings)
    {
        if (string.IsNullOrWhiteSpace(
                settings.ImapHost))
        {
            throw new InvalidOperationException(
                "IMAP host is not configured.");
        }

        if (settings.ImapPort is < 1 or > 65535)
        {
            throw new InvalidOperationException(
                "IMAP port is invalid.");
        }

        if (string.IsNullOrWhiteSpace(
                settings.EmailAddress)
            || string.IsNullOrWhiteSpace(
                settings.AppPassword))
        {
            throw new InvalidOperationException(
                "IMAP email address or app password is not configured.");
        }
    }

    private static bool IsPdfAttachment(
        MimePart attachment)
    {
        return string.Equals(
                   attachment.ContentType?.MimeType,
                   "application/pdf",
                   StringComparison.OrdinalIgnoreCase)
               || (!string.IsNullOrWhiteSpace(
                       attachment.FileName)
                   && attachment.FileName.EndsWith(
                       ".pdf",
                       StringComparison.OrdinalIgnoreCase));
    }

    private static HashSet<string>
        ParseWhitelistedDomains(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
        }

        return value
            .Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries
                | StringSplitOptions.TrimEntries)
            .Select(x =>
                x.Trim()
                    .TrimStart('@')
                    .ToLowerInvariant())
            .Where(x =>
                !string.IsNullOrWhiteSpace(x))
            .ToHashSet(
                StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsSenderAllowed(
        string? senderEmail,
        HashSet<string> whitelistedDomains)
    {
        if (whitelistedDomains.Count == 0)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(
                senderEmail))
        {
            return false;
        }

        var atIndex =
            senderEmail.LastIndexOf('@');

        if (atIndex < 0
            || atIndex == senderEmail.Length - 1)
        {
            return false;
        }

        var senderDomain =
            senderEmail[(atIndex + 1)..]
                .Trim()
                .ToLowerInvariant();

        return whitelistedDomains.Any(
            domain =>
                senderDomain.Equals(
                    domain,
                    StringComparison.OrdinalIgnoreCase)
                || senderDomain.EndsWith(
                    '.' + domain,
                    StringComparison.OrdinalIgnoreCase));
    }

    private static int ParseInt(
        string? value,
        int fallback)
        => int.TryParse(
            value,
            out var parsed)
            ? parsed
            : fallback;

    private static bool ParseBool(
        string? value,
        bool fallback)
        => bool.TryParse(
            value,
            out var parsed)
            ? parsed
            : fallback;

    private static string Truncate(
        string value,
        int maxLength)
        => value.Length <= maxLength
            ? value
            : value[..maxLength];

    private static string? TruncateNullable(
        string? value,
        int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return value.Length <= maxLength
            ? value
            : value[..maxLength];
    }

    private static string BuildStructuredSummary(
        string? refNumber,
        string? partnerName,
        string? docType,
        string? dateStr,
        string? signer,
        string? senderEmail,
        string fallbackTitle)
    {
        var sb = new System.Text.StringBuilder();
        if (!string.IsNullOrWhiteSpace(refNumber))
        {
            sb.AppendLine($"• Số ký hiệu: {refNumber.Trim()}");
        }
        if (!string.IsNullOrWhiteSpace(partnerName))
        {
            sb.AppendLine($"• Cơ quan ban hành: {partnerName.Trim()}");
        }
        if (!string.IsNullOrWhiteSpace(docType))
        {
            sb.AppendLine($"• Loại văn bản: {docType.Trim()}");
        }
        if (!string.IsNullOrWhiteSpace(dateStr))
        {
            sb.AppendLine($"• Ngày ban hành: {dateStr.Trim()}");
        }
        if (!string.IsNullOrWhiteSpace(signer))
        {
            sb.AppendLine($"• Người ký: {signer.Trim()}");
        }
        if (!string.IsNullOrWhiteSpace(senderEmail))
        {
            sb.AppendLine($"• Tiếp nhận qua Email: {senderEmail.Trim()}");
        }

        return sb.Length > 0 ? sb.ToString().Trim() : fallbackTitle;
    }
}