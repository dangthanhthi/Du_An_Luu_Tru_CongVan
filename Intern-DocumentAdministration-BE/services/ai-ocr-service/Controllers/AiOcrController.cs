using AiOcrService.DTOs;
using AiOcrService.Models;
using AiOcrService.Services;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace AiOcrService.Controllers
{
    [ApiController]
    [Route("api/ai-ocr")]
    public class AiOcrController : ControllerBase
    {
        private static List<PartnerDto>? _cachedPartners;
        private static DateTime _partnerCacheExpiry = DateTime.MinValue;
        private static readonly TimeSpan _partnerCacheTtl = TimeSpan.FromMinutes(5);

        private readonly IOcrEngine _ocrEngine;
        private readonly IPartnerMatcher _partnerMatcher;
        private readonly IOcrRuleService _ruleService;
        private readonly IDynamicFieldExtractor _fieldExtractor;
        private readonly IHttpClientFactory _httpClientFactory;

        public AiOcrController(
            IOcrEngine ocrEngine, 
            IPartnerMatcher partnerMatcher,
            IOcrRuleService ruleService,
            IDynamicFieldExtractor fieldExtractor,
            IHttpClientFactory httpClientFactory)
        {
            _ocrEngine = ocrEngine;
            _partnerMatcher = partnerMatcher;
            _ruleService = ruleService;
            _fieldExtractor = fieldExtractor;
            _httpClientFactory = httpClientFactory;
        }

        /// <summary>
        /// Phân tích toàn diện tài liệu PDF: OCR chữ, bóc tách động các trường (Số hiệu, Trích yếu, Ngày tháng, Người ký) và nhận diện đối tác
        /// </summary>
        [HttpPost("analyze")]
        public async Task<IActionResult> AnalyzeDocument([FromBody] AnalyzeRequest request)
        {
            if (request == null || request.FileId == Guid.Empty)
            {
                return BadRequest(new { success = false, message = "FileId không hợp lệ.", errors = new[] { "FileId: Required" } });
            }

            try
            {
                var client = _httpClientFactory.CreateClient();

                if (Request.Headers.TryGetValue("Authorization", out var authHeader) && !string.IsNullOrWhiteSpace(authHeader))
                {
                    if (AuthenticationHeaderValue.TryParse(authHeader, out var parsedHeader))
                    {
                        client.DefaultRequestHeaders.Authorization = parsedHeader;
                    }
                }

                var fileServiceUrl = Environment.GetEnvironmentVariable("Services__FilesService") 
                                  ?? Environment.GetEnvironmentVariable("FILE_SERVICE_URL") 
                                  ?? "http://localhost:5004";
                
                var fileResponse = await client.GetAsync($"{fileServiceUrl.TrimEnd('/')}/api/files/{request.FileId}");

                if (!fileResponse.IsSuccessStatusCode)
                {
                    return NotFound(new { success = false, message = $"Không thể tải file từ FileService (Status: {fileResponse.StatusCode})." });
                }

                var pdfBytes = await fileResponse.Content.ReadAsByteArrayAsync();
                using var pdfStream = new MemoryStream(pdfBytes);

                var extractedText = _ocrEngine.ExtractTextFromPdfStream(pdfStream);

                var extractedFields = await _fieldExtractor.ExtractFieldsAsync(extractedText, request.FileName, pdfBytes);

                // TRẠM 4: Lấy danh sách đối tác từ PartnerService và tiến hành So khớp đa tầng
                // TRẠM 4: Lấy danh sách đối tác từ PartnerService và tiến hành So khớp đa tầng
                var partners = await EnsurePartnersCachedAsync(client);

                var matchResult = _partnerMatcher.MatchPartner(extractedText, partners, request.SenderEmail);

                if (!string.IsNullOrWhiteSpace(extractedFields.PartnerName))
                {
                    var headerPartner = partners.FirstOrDefault(p =>
                        p.FullName.Equals(extractedFields.PartnerName, StringComparison.OrdinalIgnoreCase) ||
                        (!string.IsNullOrWhiteSpace(p.ShortName) && extractedFields.PartnerName.Contains(p.ShortName, StringComparison.OrdinalIgnoreCase)));
                    if (headerPartner != null)
                    {
                        matchResult.PartnerId = headerPartner.Id;
                        matchResult.Confidence = 0.99;
                        matchResult.MatchMethod = "HeaderAgencyMatch";
                    }
                }

                var finalPartnerName = extractedFields.PartnerName;
                if (matchResult.PartnerId != null)
                {
                    var matchedPartner = partners.FirstOrDefault(p => p.Id == matchResult.PartnerId);
                    if (matchedPartner != null)
                    {
                        finalPartnerName = matchedPartner.FullName;
                    }
                }

                var finalRefNumber = extractedFields.ReferenceNumber ?? _partnerMatcher.ExtractReferenceNumber(extractedText);

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        extractedText = extractedText,
                        extractedReferenceNumber = finalRefNumber,
                        extractedSubject = extractedFields.Subject,
                        extractedDate = extractedFields.DocumentDate,
                        extractedDateString = extractedFields.DocumentDateString,
                        extractedSigner = extractedFields.Signer,
                        extractedDocumentType = extractedFields.DocumentType,
                        extractedPartnerName = finalPartnerName,
                        matchedPartnerId = matchResult.PartnerId,
                        confidence = matchResult.Confidence,
                        matchMethod = matchResult.MatchMethod
                    },
                    message = (string?)null,
                    errors = Array.Empty<string>()
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message, errors = new[] { ex.ToString() } });
            }
        }

        /// <summary>
        /// </summary>
        [HttpPost("analyze-file")]
        public async Task<IActionResult> AnalyzeUploadedFile([FromForm] Microsoft.AspNetCore.Http.IFormFile file, [FromForm] string? senderEmail = null)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { success = false, message = "File tải lên không hợp lệ." });
            }

            try
            {
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                var pdfBytes = ms.ToArray();
                using var pdfStream = new MemoryStream(pdfBytes);

                var extractedText = _ocrEngine.ExtractTextFromPdfStream(pdfStream);
                var extractedFields = await _fieldExtractor.ExtractFieldsAsync(extractedText, file.FileName, pdfBytes);

                var client = _httpClientFactory.CreateClient();
                var partners = await EnsurePartnersCachedAsync(client);
                var matchResult = _partnerMatcher.MatchPartner(extractedText, partners, senderEmail);

                if (!string.IsNullOrWhiteSpace(extractedFields.PartnerName))
                {
                    var headerPartner = partners.FirstOrDefault(p =>
                        p.FullName.Equals(extractedFields.PartnerName, StringComparison.OrdinalIgnoreCase) ||
                        (!string.IsNullOrWhiteSpace(p.ShortName) && extractedFields.PartnerName.Contains(p.ShortName, StringComparison.OrdinalIgnoreCase)));
                    if (headerPartner != null)
                    {
                        matchResult.PartnerId = headerPartner.Id;
                        matchResult.Confidence = 0.99;
                        matchResult.MatchMethod = "HeaderAgencyMatch";
                    }
                }

                var finalPartnerName = extractedFields.PartnerName;
                if (matchResult.PartnerId != null)
                {
                    var matchedPartner = partners.FirstOrDefault(p => p.Id == matchResult.PartnerId);
                    if (matchedPartner != null)
                    {
                        finalPartnerName = matchedPartner.FullName;
                    }
                }

                var finalRefNumber = extractedFields.ReferenceNumber ?? _partnerMatcher.ExtractReferenceNumber(extractedText);

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        extractedText = extractedText,
                        extractedReferenceNumber = finalRefNumber,
                        extractedSubject = extractedFields.Subject,
                        extractedDate = extractedFields.DocumentDate,
                        extractedDateString = extractedFields.DocumentDateString,
                        extractedSigner = extractedFields.Signer,
                        extractedDocumentType = extractedFields.DocumentType,
                        extractedPartnerName = finalPartnerName,
                        matchedPartnerId = matchResult.PartnerId,
                        confidence = matchResult.Confidence,
                        matchMethod = matchResult.MatchMethod
                    },
                    message = "Nhận dạng quang học tài liệu trực tiếp thành công."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// </summary>
        [HttpGet("rules")]
        public async Task<IActionResult> GetRules([FromQuery] string? ruleType = null, [FromQuery] bool? isActive = null)
        {
            var rules = await _ruleService.GetRulesAsync(ruleType, isActive);
            return Ok(new
            {
                success = true,
                data = rules,
                totalCount = rules.Count,
                message = (string?)null,
                errors = Array.Empty<string>()
            });
        }

        /// <summary>
        /// </summary>
        [HttpGet("rules/{id:guid}")]
        public async Task<IActionResult> GetRuleById(Guid id)
        {
            var rule = await _ruleService.GetRuleByIdAsync(id);
            if (rule == null)
            {
                return NotFound(new { success = false, message = $"Không tìm thấy quy tắc ID: {id}", errors = new[] { "Rule not found" } });
            }

            return Ok(new { success = true, data = rule, message = (string?)null, errors = Array.Empty<string>() });
        }

        /// <summary>
        /// </summary>
        [HttpPost("rules")]
        public async Task<IActionResult> CreateRule([FromBody] CreateOcrRuleRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.RuleType) || string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Pattern))
            {
                return BadRequest(new { success = false, message = "RuleType, Name và Pattern là các trường bắt buộc.", errors = new[] { "Validation error" } });
            }

            var createdRule = await _ruleService.CreateRuleAsync(request);
            return CreatedAtAction(nameof(GetRuleById), new { id = createdRule.Id }, new
            {
                success = true,
                data = createdRule,
                message = "Đã tạo quy tắc nhận diện động thành công.",
                errors = Array.Empty<string>()
            });
        }

        /// <summary>
        /// </summary>
        [HttpPut("rules/{id:guid}")]
        public async Task<IActionResult> UpdateRule(Guid id, [FromBody] UpdateOcrRuleRequest request)
        {
            var updatedRule = await _ruleService.UpdateRuleAsync(id, request);
            if (updatedRule == null)
            {
                return NotFound(new { success = false, message = $"Không tìm thấy quy tắc ID: {id}", errors = new[] { "Rule not found" } });
            }

            return Ok(new
            {
                success = true,
                data = updatedRule,
                message = "Đã cập nhật quy tắc nhận diện thành công.",
                errors = Array.Empty<string>()
            });
        }

        /// <summary>
        /// </summary>
        [HttpDelete("rules/{id:guid}")]
        public async Task<IActionResult> DeleteRule(Guid id)
        {
            var deleted = await _ruleService.DeleteRuleAsync(id);
            if (!deleted)
            {
                return NotFound(new { success = false, message = $"Không tìm thấy quy tắc ID: {id}", errors = new[] { "Rule not found" } });
            }

            return Ok(new
            {
                success = true,
                data = (object?)null,
                message = "Đã xóa quy tắc nhận diện thành công.",
                errors = Array.Empty<string>()
            });
        }

        /// <summary>
        /// </summary>
        [HttpPost("rules/reset-defaults")]
        public async Task<IActionResult> ResetDefaults()
        {
            var defaultRules = await _ruleService.ResetToDefaultsAsync();
            return Ok(new
            {
                success = true,
                data = defaultRules,
                message = "Đã khôi phục toàn bộ quy tắc mặc định thành công.",
                errors = Array.Empty<string>()
            });
        }

        /// <summary>
        /// </summary>
        [HttpPost("rules/test")]
        public IActionResult TestPattern([FromBody] TestPatternRequest request)
        {
            var result = _ruleService.TestPattern(request);
            return Ok(new
            {
                success = true,
                data = result,
                message = "Đã thực thi kiểm thử mẫu Regex.",
                errors = Array.Empty<string>()
            });
        }

        private async Task<List<PartnerDto>> EnsurePartnersCachedAsync(HttpClient client)
        {
            var partners = new List<PartnerDto>();

            if (_cachedPartners != null && DateTime.UtcNow < _partnerCacheExpiry)
            {
                return _cachedPartners;
            }

            var partnerServiceUrl = Environment.GetEnvironmentVariable("Services__PartnerService") 
                                 ?? Environment.GetEnvironmentVariable("PARTNER_SERVICE_URL") 
                                 ?? "http://localhost:5003";

            try
            {
                var partnerResponse = await client.GetAsync($"{partnerServiceUrl.TrimEnd('/')}/api/partners?pageSize=1000");
                if (partnerResponse.IsSuccessStatusCode)
                {
                    var jsonString = await partnerResponse.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(jsonString);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("data", out var dataElem))
                    {
                        JsonElement itemsArray = default;
                        if (dataElem.ValueKind == JsonValueKind.Array)
                        {
                            itemsArray = dataElem;
                        }
                        else if (dataElem.ValueKind == JsonValueKind.Object && dataElem.TryGetProperty("items", out var itemsProp) && itemsProp.ValueKind == JsonValueKind.Array)
                        {
                            itemsArray = itemsProp;
                        }

                        if (itemsArray.ValueKind == JsonValueKind.Array)
                        {
                            var parsedList = JsonSerializer.Deserialize<List<PartnerDto>>(itemsArray.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                            if (parsedList != null) 
                            {
                                partners = parsedList;
                                _cachedPartners = partners;
                                _partnerCacheExpiry = DateTime.UtcNow.Add(_partnerCacheTtl);
                            }
                        }
                    }
                }
            }
            catch
            {
            }

            return partners;
        }
    }

    public class AnalyzeRequest
    {
        public Guid FileId { get; set; }
        public string? SenderEmail { get; set; }
        public string? FileName { get; set; }
    }
}
