using AiOcrService.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AiOcrService.Services
{
    public class OcrRuleService : IOcrRuleService
    {
        private readonly string _storageFilePath;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private List<OcrPatternRule> _cachedRules = new();

        public OcrRuleService()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var dataDir = Path.Combine(baseDir, "data");
            if (!Directory.Exists(dataDir))
            {
                Directory.CreateDirectory(dataDir);
            }
            _storageFilePath = Path.Combine(dataDir, "ocr_rules.json");
            InitializeStorage();
        }

        private void InitializeStorage()
        {
            if (File.Exists(_storageFilePath))
            {
                try
                {
                    var json = File.ReadAllText(_storageFilePath);
                    var rules = JsonSerializer.Deserialize<List<OcrPatternRule>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (rules != null && rules.Any())
                    {
                        _cachedRules = rules;
                        return;
                    }
                }
                catch
                {
                    // If corrupted, re-seed defaults
                }
            }

            _cachedRules = GetDefaultRules();
            SaveToFile();
        }

        private void SaveToFile()
        {
            try
            {
                var json = JsonSerializer.Serialize(_cachedRules, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_storageFilePath, json);
            }
            catch { }
        }

        public static List<OcrPatternRule> GetDefaultRules()
        {
            return new List<OcrPatternRule>
            {
                // ==========================================
                // 1. NHÓM TRÍCH YẾU / TIÊU ĐỀ (SUBJECT)
                // ==========================================
                new()
                {
                    RuleType = "Subject",
                    Name = "Tiếng Việt - V/v và các biến thể OCR (VIv, Vlv, V/v, Về việc)",
                    Pattern = @"(?:^|\n|[\s(])(?:\bV[\/\\]v|\bV[\/\\]V|\bv[\/\\]v|\bVIv\b|\bVlv\b|\bV1v\b|\bV\|v\b|\bV\.v\b|\bVv\b|Về việc|Ve viec|VỀ VIỆC)[:.:-]?\s*([\s\S]{5,500}?(?=\n\s*(?:Kính|K[ií]nh|Kinh|Xét\s*đề\s*nghị|X6t|Căn cứ|C[aă]n|Điều \d|CON-G|CỔNG|Nơi nhận|Ndi|\n\s*\n)|$))",
                    Priority = 1,
                    IsActive = true,
                    Description = "Bóc tách trích yếu theo mẫu 'V/v: Hướng dẫn...', 'VIv thu hut dau tu...'"
                },
                new()
                {
                    RuleType = "Subject",
                    Name = "Tiếng Việt - Trích yếu",
                    Pattern = @"(?:^|\n|[\s(])(?:Trích yếu|Trich yeu|TRÍCH YẾU)[:.:-]?\s*([\s\S]{5,500}?(?=\n\s*(?:Kính|K[ií]nh|Kinh|Xét|Căn cứ|C[aă]n|Điều \d|Nơi nhận|Ndi|\n\s*\n)|$))",
                    Priority = 2,
                    IsActive = true,
                    Description = "Bóc tách trích yếu theo mẫu văn thư 'Trích yếu: ...'"
                },
                new()
                {
                    RuleType = "Subject",
                    Name = "Tiêu đề theo thể thức văn bản (Thông báo, Quyết định, Giấy mời)",
                    Pattern = @"(?:^|\n)\s*(?:THÔNG BÁO|QUYẾT ĐỊNH|GIẤY MỜI|TỜ TRÌNH|CHỈ THỊ)\s*\n\s*([\s\S]{5,500}?(?=\n\s*(?:Kính|K[ií]nh|Kinh|Xét|Căn cứ|Điều \d|Nơi nhận|\n\s*\n)|$))",
                    Priority = 3,
                    IsActive = true,
                    Description = "Bóc tách tiêu đề trực tiếp dưới tên loại văn bản"
                },
                new()
                {
                    RuleType = "Subject",
                    Name = "Tiếng Anh - Regarding / Re:",
                    Pattern = @"(?:^|\n|[\s(])(?:Regarding|regarding|\bRe\b|\bRE\b)[:.]?\s*([\s\S]{5,500}?(?=\n\s*(?:To:|Dear|\n)|$))",
                    Priority = 4,
                    IsActive = true,
                    Description = "Bóc tách trích yếu công văn quốc tế 'Regarding: Cooperation on digital...'"
                },
                new()
                {
                    RuleType = "Subject",
                    Name = "Tiếng Anh - Subject:",
                    Pattern = @"(?:^|\n|[\s(])(?:Subject|SUBJECT)[:.]?\s*([\s\S]{5,500}?(?=\n\s*(?:To:|Dear|\n)|$))",
                    Priority = 5,
                    IsActive = true,
                    Description = "Bóc tách trích yếu công văn theo nhãn 'Subject: ...'"
                },

                // ==========================================
                // 2. NHÓM SỐ KÝ HIỆU (REFERENCE NUMBER)
                // ==========================================
                new()
                {
                    RuleType = "ReferenceNumber",
                    Name = "Chuẩn Nghị định 30 & Hỗ trợ số viết tay / OCR nhiễu",
                    Pattern = @"(?:Số|Sô|Sổ|Sé|So|S6|86|8ô|8o|No|Ref|Ký\s*hiệu|Số\s*hiệu)\s*[:.]?\s*([0-9A-Za-z\s,\[\]\(\)\{\}\-]*\s*[\/\\|]\s*[A-ZĐa-z0-9\-_]+(?:\s*[\/\\|]\s*[A-ZĐa-z0-9\-_]+)*)",
                    Priority = 1,
                    IsActive = true,
                    Description = "Bóc tách số công văn dạng 'Số: 128/BGDĐT-GDĐH', '86: 1128 /TTg-ĐMDN', 'Số: 45/UBND-VX'"
                },
                new()
                {
                    RuleType = "ReferenceNumber",
                    Name = "Số công văn không dấu hai chấm",
                    Pattern = @"\b(?:Số|So|S6|86)\s+([0-9A-Za-z\s,\[\]\-]+\s*[\/\\|]\s*[A-Z0-9Đa-z\-_]+(?:\s*[\/\\|]\s*[A-Z0-9Đa-z\-_]+)*)",
                    Priority = 2,
                    IsActive = true,
                    Description = "Bóc tách số công văn dạng 'Số 789/FPT-CNTT'"
                },
                new()
                {
                    RuleType = "ReferenceNumber",
                    Name = "Chuẩn Quốc tế (Ref. No. / Reference)",
                    Pattern = @"(?:Ref|Reference)\s*(?:No|Number|\.)?\s*[:.]\s*([0-9A-Za-z\/\-_]{2,40})",
                    Priority = 3,
                    IsActive = true,
                    Description = "Bóc tách số công văn nước ngoài 'Ref. No.: SEV-2026/0815'"
                },

                // ==========================================
                // 3. NHÓM NGÀY BAN HÀNH (DOCUMENT DATE)
                // ==========================================
                new()
                {
                    RuleType = "DocumentDate",
                    Name = "Chuẩn Ngày Tháng Năm Tiếng Việt",
                    Pattern = @"(?:ngày|ngay)\s+([0-9]{1,2})\s+(?:tháng|thang)\s+([0-9]{1,2})\s+(?:năm|nam)\s+([0-9]{4})",
                    Priority = 1,
                    IsActive = true,
                    Description = "Bóc tách ngày ban hành dạng 'Hà Nội, ngày 15 tháng 8 năm 2026'"
                },
                new()
                {
                    RuleType = "DocumentDate",
                    Name = "Chuẩn Ngày DD/MM/YYYY",
                    Pattern = @"(?:Date|Ngày|ngay)\s*[:.]\s*([0-9]{1,2}[\/\-\.][0-9]{1,2}[\/\-\.][0-9]{4})",
                    Priority = 2,
                    IsActive = true,
                    Description = "Bóc tách ngày dạng 'Date: 15/08/2026' hoặc 'Ngày: 15-08-2026'"
                },
                new()
                {
                    RuleType = "DocumentDate",
                    Name = "Ngày DD/MM/YYYY độc lập",
                    Pattern = @"\b([0-9]{1,2})[/\-.]([0-9]{1,2})[/\-.]([0-9]{4})\b",
                    Priority = 3,
                    IsActive = true,
                    Description = "Bóc tách ngày độc lập không có tiền tố Ngày/Date"
                },

                // ==========================================
                // 4. NHÓM NGƯỜI KÝ & CHỨC DANH (SIGNER)
                // ==========================================
                new()
                {
                    RuleType = "Signer",
                    Name = "Chức danh lãnh đạo và Họ tên",
                    Pattern = @"(?:KT\.\s*BỘ TRƯỞNG|THỨ TRƯỞNG|TỔNG GIÁM ĐỐC|GIÁM ĐỐC|CHỦ TỊCH|PHÓ CHỦ TỊCH|PHÓ TỔNG GIÁM ĐỐC|GENERAL DIRECTOR)\s*[\r\n]+(?:\s*[\r\n]+)?([A-ZÀÁẢÃẠĂẮẰẲẴẶÂẤẦẨẪẬÈÉẺẼẸÊẾỀỂỄỆÌÍỈĨỊÒÓỎÕỌÔỐỒỔỖỘƠỚỜỞỠỢÙÚỦŨỤƯỨỪỬỮỰỲÝỶỸỴĐa-zàáảãạăắằẳẵặâấầẩẫậèéẻẽẹêếềểễệìíỉĩịòóỏõọôốồổỗộơớờởỡợùúủũụưứừửữựỳýỷỹỵđ\s]{3,40})",
                    Priority = 1,
                    IsActive = true,
                    Description = "Bóc tách tên người ký sau chức danh hành chính"
                },

                // ==========================================
                // 5. NHÓM PHÂN LOẠI LOẠI VĂN BẢN (DOCUMENT TYPE)
                // ==========================================
                new()
                {
                    RuleType = "DocumentType",
                    Name = "Từ khóa phân loại văn bản chính quy",
                    Pattern = @"\b(QUYẾT ĐỊNH|THÔNG BÁO|TỜ TRÌNH|KẾ HOẠCH|CHỈ THỊ|BÁO CÁO|CÔNG VĂN|HỢP ĐỒNG|BIÊN BẢN|NGHỊ QUYẾT|QUY CHẾ|QUY ĐỊNH|THƯ KÊU GỌI|GIẤY MỜI)\b",
                    Priority = 1,
                    IsActive = true,
                    Description = "Phân loại loại văn bản dựa trên từ khóa in hoa đầu văn bản"
                }
            };
        }

        public async Task<List<OcrPatternRule>> GetRulesAsync(string? ruleType = null, bool? isActive = null)
        {
            await _lock.WaitAsync();
            try
            {
                var query = _cachedRules.AsEnumerable();
                if (!string.IsNullOrWhiteSpace(ruleType))
                {
                    query = query.Where(r => r.RuleType.Equals(ruleType.Trim(), StringComparison.OrdinalIgnoreCase));
                }
                if (isActive.HasValue)
                {
                    query = query.Where(r => r.IsActive == isActive.Value);
                }

                return query.OrderBy(r => r.RuleType).ThenBy(r => r.Priority).ToList();
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<OcrPatternRule?> GetRuleByIdAsync(Guid id)
        {
            await _lock.WaitAsync();
            try
            {
                return _cachedRules.FirstOrDefault(r => r.Id == id);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<OcrPatternRule> CreateRuleAsync(CreateOcrRuleRequest request)
        {
            await _lock.WaitAsync();
            try
            {
                var rule = new OcrPatternRule
                {
                    Id = Guid.NewGuid(),
                    RuleType = request.RuleType.Trim(),
                    Name = request.Name.Trim(),
                    Pattern = request.Pattern.Trim(),
                    Priority = request.Priority,
                    IsActive = request.IsActive,
                    Description = request.Description,
                    CreatedAt = DateTime.UtcNow
                };

                _cachedRules.Add(rule);
                SaveToFile();
                return rule;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<OcrPatternRule?> UpdateRuleAsync(Guid id, UpdateOcrRuleRequest request)
        {
            await _lock.WaitAsync();
            try
            {
                var rule = _cachedRules.FirstOrDefault(r => r.Id == id);
                if (rule == null) return null;

                if (!string.IsNullOrWhiteSpace(request.RuleType)) rule.RuleType = request.RuleType.Trim();
                if (!string.IsNullOrWhiteSpace(request.Name)) rule.Name = request.Name.Trim();
                if (!string.IsNullOrWhiteSpace(request.Pattern)) rule.Pattern = request.Pattern.Trim();
                if (request.Priority.HasValue) rule.Priority = request.Priority.Value;
                if (request.IsActive.HasValue) rule.IsActive = request.IsActive.Value;
                if (request.Description != null) rule.Description = request.Description;
                rule.UpdatedAt = DateTime.UtcNow;

                SaveToFile();
                return rule;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<bool> DeleteRuleAsync(Guid id)
        {
            await _lock.WaitAsync();
            try
            {
                var rule = _cachedRules.FirstOrDefault(r => r.Id == id);
                if (rule == null) return false;

                _cachedRules.Remove(rule);
                SaveToFile();
                return true;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<List<OcrPatternRule>> ResetToDefaultsAsync()
        {
            await _lock.WaitAsync();
            try
            {
                _cachedRules = GetDefaultRules();
                SaveToFile();
                return _cachedRules;
            }
            finally
            {
                _lock.Release();
            }
        }

        public object TestPattern(TestPatternRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Pattern))
            {
                return new { success = false, message = "Pattern không được để trống.", matched = false };
            }

            try
            {
                var regex = new Regex(request.Pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline, TimeSpan.FromSeconds(2));
                var match = regex.Match(request.SampleText ?? string.Empty);

                if (match.Success)
                {
                    var capturedGroup = match.Groups.Count > 1 ? match.Groups[1].Value.Trim() : match.Value.Trim();
                    return new
                    {
                        success = true,
                        matched = true,
                        fullMatch = match.Value,
                        extractedValue = capturedGroup,
                        groups = match.Groups.Cast<Group>().Select((g, i) => new { index = i, value = g.Value }).ToList()
                    };
                }

                return new
                {
                    success = true,
                    matched = false,
                    message = "Không tìm thấy kết quả khớp trong đoạn văn bản mẫu."
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    success = false,
                    matched = false,
                    message = $"Lỗi cú pháp Regex: {ex.Message}"
                };
            }
        }
    }
}
