using AiOcrService.DTOs;
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
        private readonly IOcrEngine _ocrEngine;
        private readonly IPartnerMatcher _partnerMatcher;
        private readonly IHttpClientFactory _httpClientFactory;

        public AiOcrController(
            IOcrEngine ocrEngine, 
            IPartnerMatcher partnerMatcher, 
            IHttpClientFactory httpClientFactory)
        {
            _ocrEngine = ocrEngine;
            _partnerMatcher = partnerMatcher;
            _httpClientFactory = httpClientFactory;
        }

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

                // Chuyển tiếp Token xác thực nếu Client có gắn Bearer Token
                if (Request.Headers.TryGetValue("Authorization", out var authHeader) && !string.IsNullOrWhiteSpace(authHeader))
                {
                    if (AuthenticationHeaderValue.TryParse(authHeader, out var parsedHeader))
                    {
                        client.DefaultRequestHeaders.Authorization = parsedHeader;
                    }
                }

                // TRẠM 1: Lấy file PDF binary từ FileService
                var fileServiceUrl = Environment.GetEnvironmentVariable("Services__FilesService") 
                                  ?? Environment.GetEnvironmentVariable("FILE_SERVICE_URL") 
                                  ?? "http://localhost:5004";
                
                var fileResponse = await client.GetAsync($"{fileServiceUrl.TrimEnd('/')}/api/files/{request.FileId}");

                if (!fileResponse.IsSuccessStatusCode)
                {
                    return NotFound(new { success = false, message = $"Không thể tải file từ FileService (Status: {fileResponse.StatusCode})." });
                }

                using var pdfStream = await fileResponse.Content.ReadAsStreamAsync();

                // TRẠM 2: Trích xuất chữ bằng Tesseract / OCR Engine
                var extractedText = _ocrEngine.ExtractTextFromPdfStream(pdfStream);

                // TRẠM 3: Lấy danh sách đối tác từ PartnerService và tiến hành So khớp
                var partnerServiceUrl = Environment.GetEnvironmentVariable("Services__PartnerService") 
                                     ?? Environment.GetEnvironmentVariable("PARTNER_SERVICE_URL") 
                                     ?? "http://localhost:5003";
                var partners = new List<PartnerDto>();

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
                            // Hỗ trợ cả 2 định dạng: data là mảng [ ... ] HOẶC data là object paged { items: [ ... ] }
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
                                if (parsedList != null) partners = parsedList;
                            }
                        }
                    }
                }
                catch
                {
                    // Nếu gặp lỗi khi gọi PartnerService, vẫn trả về chuỗi text đã OCR thành công
                }

                var (matchedPartnerId, confidence) = _partnerMatcher.MatchPartner(extractedText, partners);

                // Trả về kết quả JSON theo chuẩn API Contract
                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        extractedText = extractedText,
                        matchedPartnerId = matchedPartnerId,
                        confidence = confidence
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
    }

    public class AnalyzeRequest
    {
        public Guid FileId { get; set; }
    }
}