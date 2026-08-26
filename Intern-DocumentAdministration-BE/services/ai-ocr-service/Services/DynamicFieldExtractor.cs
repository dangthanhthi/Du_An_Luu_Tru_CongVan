using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AiOcrService.Services
{
    public class DynamicFieldExtractor : IDynamicFieldExtractor
    {
        private readonly IOcrRuleService _ruleService;

        public DynamicFieldExtractor(IOcrRuleService ruleService)
        {
            _ruleService = ruleService;
        }

        public async Task<ExtractedDocumentData> ExtractFieldsAsync(string extractedText)
        {
            var result = new ExtractedDocumentData();
            if (string.IsNullOrWhiteSpace(extractedText)) return result;

            var activeRules = await _ruleService.GetRulesAsync(isActive: true);

            // Tách khối Header (35% đầu hoặc trước Kính gửi / Xét đề nghị / Căn cứ)
            var headerText = extractedText;
            var cutoffMatch = Regex.Match(extractedText, @"(?:\n\s*Kính\s*gửi|\n\s*K[ií]nh\s*g[ửu]i|\n\s*Kinh\s*gui|\n\s*Xét\s*đề\s*nghị|\n\s*X6t\s*dS\s*nghi|\n\s*Căn\s*cứ|\n\s*C[aă]n\s*c[ứu]|\n\s*Điều \d|\n\s*QUYẾT\s*ĐỊNH|\n\s*THÔNG\s*BÁO\s*\n\s*Về\s*việc|\n\s*Nơi\s*nhận|\n\s*Ndi\s*nh)", RegexOptions.IgnoreCase);
            if (cutoffMatch.Success && cutoffMatch.Index > 50)
            {
                headerText = extractedText.Substring(0, cutoffMatch.Index);
            }
            else if (extractedText.Length > 2000)
            {
                headerText = extractedText.Substring(0, 2000);
            }

            // 1. BÓC TÁCH SỐ KÝ HIỆU (ReferenceNumber) - Ưu tiên tuyệt đối khối Header Trang 1
            var refRules = activeRules
                .Where(r => r.RuleType.Equals("ReferenceNumber", StringComparison.OrdinalIgnoreCase))
                .OrderBy(r => r.Priority);

            var refCandidates = new System.Collections.Generic.List<(string Value, int Priority)>();
            foreach (var rule in refRules)
            {
                try
                {
                    // Thử khớp trong khối Header trước
                    var match = Regex.Match(headerText, rule.Pattern, 
                        RegexOptions.IgnoreCase | RegexOptions.Multiline,
                        TimeSpan.FromSeconds(2));
                    
                    if (!match.Success)
                    {
                        // Nếu không thấy trong Header, thử trong phần không phải trích dẫn (nhưng giới hạn 3000 ký tự đầu)
                        var earlyText = extractedText.Substring(0, Math.Min(extractedText.Length, 3000));
                        var nonCitationText = Regex.Replace(earlyText, @"(?:Xét\s*đề\s*nghị|X6t|Căn\s*cứ|C[aă]n)\s+(?:Quyết định|Nghị định|Thông tư|Luật|Công văn|Văn bản)[\s\S]*?(?=\n\s*\n|$)", "", RegexOptions.IgnoreCase);
                        match = Regex.Match(nonCitationText, rule.Pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline, TimeSpan.FromSeconds(2));
                    }

                    if (match.Success)
                    {
                        var val = (match.Groups.Count > 1 ? match.Groups[1].Value : match.Value).Trim();
                        val = Regex.Replace(val, @"\s*[\/\\|]\s*", "/");
                        val = Regex.Replace(val, @"BMDN", "ĐMDN", RegexOptions.IgnoreCase);
                        val = Regex.Replace(val, @"BGDDT", "BGDĐT", RegexOptions.IgnoreCase);
                        val = Regex.Replace(val, @"SGDDT", "SGDĐT", RegexOptions.IgnoreCase);
                        val = Regex.Replace(val, @"KHCN", "KHCN", RegexOptions.IgnoreCase);
                        val = val.TrimEnd('.', ',', ';', ':', '-', ' ');
                        
                        if (val.Length >= 2 && !val.Equals("ngay", StringComparison.OrdinalIgnoreCase) && !val.Equals("2b/VT", StringComparison.OrdinalIgnoreCase))
                        {
                            refCandidates.Add((val, rule.Priority));
                        }
                    }
                }
                catch { }
            }
            if (refCandidates.Count > 0)
            {
                result.ReferenceNumber = refCandidates
                    .OrderBy(c => c.Priority)
                    .ThenByDescending(c => c.Value.Length)
                    .First().Value;
            }

            // 2. BÓC TÁCH TRÍCH YẾU / TIÊU ĐỀ (Subject) - CHỈ TÌM TRONG HEADER TRANG 1
            var subjectRules = activeRules
                .Where(r => r.RuleType.Equals("Subject", StringComparison.OrdinalIgnoreCase))
                .OrderBy(r => r.Priority);

            var subjectCandidates = new System.Collections.Generic.List<(string Value, int Priority)>();
            foreach (var rule in subjectRules)
            {
                try
                {
                    var match = Regex.Match(headerText, rule.Pattern, 
                        RegexOptions.IgnoreCase | RegexOptions.Multiline,
                        TimeSpan.FromSeconds(2));
                    if (match.Success)
                    {
                        var val = (match.Groups.Count > 1 ? match.Groups[1].Value : match.Value).Trim();
                        val = val.TrimEnd('.', ',', ';', ':', '-', ' ');
                        val = Regex.Replace(val, @"\s+", " ");
                        val = Regex.Replace(val, @"^(?:Về việc|VỀ VIỆC|Ve viec|V[\/\\]v|V\.v|VIv|Vlv|V1v|Trích yếu|TRÍCH YẾU|Regarding|Subject)\s*[:.:-]?\s*", "", RegexOptions.IgnoreCase).Trim();
                        val = NormalizeSubjectDiacritics(val);

                        if (IsValidSubject(val))
                        {
                            subjectCandidates.Add((val, rule.Priority));
                        }
                    }
                }
                catch { }
            }
            if (subjectCandidates.Count > 0)
            {
                result.Subject = subjectCandidates
                    .OrderBy(c => c.Priority)
                    .ThenByDescending(c => c.Value.Length)
                    .First().Value;
            }

            // 3. BÓC TÁCH NGÀY BAN HÀNH (DocumentDate)
            var dateRules = activeRules
                .Where(r => r.RuleType.Equals("DocumentDate", StringComparison.OrdinalIgnoreCase))
                .OrderBy(r => r.Priority);

            foreach (var rule in dateRules)
            {
                try
                {
                    var match = Regex.Match(extractedText, rule.Pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                    if (match.Success)
                    {
                        if (match.Groups.Count >= 4)
                        {
                            // Dạng: ngày {1} tháng {2} năm {3}
                            if (int.TryParse(match.Groups[1].Value, out var day) &&
                                int.TryParse(match.Groups[2].Value, out var month) &&
                                int.TryParse(match.Groups[3].Value, out var year))
                            {
                                if (day >= 1 && day <= 31 && month >= 1 && month <= 12 && year >= 1900 && year <= 2100)
                                {
                                    result.DocumentDate = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc);
                                    result.DocumentDateString = $"{day:D2}/{month:D2}/{year}";
                                    break;
                                }
                            }
                        }
                        else
                        {
                            var dateStr = (match.Groups.Count > 1 ? match.Groups[1].Value : match.Value).Trim();
                            string[] formats = { "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy", "yyyy-MM-dd" };
                            if (DateTime.TryParseExact(dateStr, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
                            {
                                result.DocumentDate = DateTime.SpecifyKind(parsedDate, DateTimeKind.Utc);
                                result.DocumentDateString = parsedDate.ToString("dd/MM/yyyy");
                                break;
                            }
                        }
                    }
                }
                catch { }
            }

            // 4. BÓC TÁCH NGƯỜI KÝ & CHỨC DANH (Signer)
            var signerRules = activeRules
                .Where(r => r.RuleType.Equals("Signer", StringComparison.OrdinalIgnoreCase))
                .OrderBy(r => r.Priority);

            foreach (var rule in signerRules)
            {
                try
                {
                    var match = Regex.Match(extractedText, rule.Pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                    if (match.Success)
                    {
                        var val = (match.Groups.Count > 1 ? match.Groups[1].Value : match.Value).Trim();
                        val = Regex.Replace(val, @"\s+", " ");
                        if (val.Length >= 3)
                        {
                            result.Signer = val;
                            break;
                        }
                    }
                }
                catch { }
            }

            // 5. BÓC TÁCH & PHÂN LOẠI LOẠI VĂN BẢN (DocumentType)
            var typeRules = activeRules
                .Where(r => r.RuleType.Equals("DocumentType", StringComparison.OrdinalIgnoreCase))
                .OrderBy(r => r.Priority);

            foreach (var rule in typeRules)
            {
                try
                {
                    var match = Regex.Match(extractedText, rule.Pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                    if (match.Success)
                    {
                        var val = (match.Groups.Count > 1 ? match.Groups[1].Value : match.Value).Trim();
                        result.DocumentType = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(val.ToLowerInvariant());
                        break;
                    }
                }
                catch { }
            }

            return result;
        }

        private static string NormalizeSubjectDiacritics(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return raw;

            var text = raw;
            var replacements = new (string Pattern, string Replacement)[]
            {
                (@"\bthu\s*hut\b", "thu hút"),
                (@"\bd[AÀa]u\s*tu\b", "đầu tư"),
                (@"\bkh[CƠơ6][\w\~'\?]*\s*nghi[~eê]p\b", "khởi nghiệp"),
                (@"\bkh[oô]i\s*nghi[eê]p\b", "khởi nghiệp"),
                (@"\bd6i\s*m6['\?]?i\b", "đổi mới"),
                (@"\bd[oô]i\s*m[oơ]i\b", "đổi mới"),
                (@"\bsang\s*t[~aã]o\b", "sáng tạo"),
                (@"\btil\s*cae\s*ngu[6oô]n\b", "từ các nguồn"),
                (@"\btu\s*cac\s*nguon\b", "từ các nguồn"),
                (@"\btrong\s*va\s*ngoai\s*nu6['\?]?c\b", "trong và ngoài nước"),
                (@"\btrong\s*va\s*ngoai\s*nuoc\b", "trong và ngoài nước"),
                (@"\btri[eê]n\s*khai\b", "triển khai"),
                (@"\bap\s*d[uụ]ng\b", "áp dụng"),
                (@"\bquy\s*trinh\b", "quy trình"),
                (@"\bs[oố]\s*h[oó]a\b", "số hóa"),
                (@"\bti[eê]p\s*nh[aậ]n\b", "tiếp nhận"),
                (@"\bvan\s*b[aả]n\b", "văn bản"),
                (@"\bt[uự]\s*d[oộ]ng\b", "tự động"),
                (@"\bqua\s*h[eệ]\s*th[oố]ng\b", "qua hệ thống"),
                (@"\bh[uư][oớ]ng\s*d[aẫ]n\b", "hướng dẫn"),
                (@"\bth[oô]ng\s*b[aá]o\b", "thông báo"),
                (@"\bquy[eê]t\s*d[iị]nh\b", "quyết định"),
                (@"\bph[eê]\s*duy[eệ]t\b", "phê duyệt"),
                (@"\bke\s*hoach\b", "kế hoạch"),
                (@"\bk[eế]\s*ho[aạ]ch\b", "kế hoạch"),
                (@"\bto\s*chuc\b", "tổ chức"),
                (@"\bt[oổ]\s*ch[uứ]c\b", "tổ chức"),
                (@"\bthuc\s*hien\b", "thực hiện"),
                (@"\bth[uự]c\s*hi[eệ]n\b", "thực hiện")
            };

            foreach (var (pattern, replacement) in replacements)
            {
                text = Regex.Replace(text, pattern, replacement, RegexOptions.IgnoreCase);
            }

            if (text.Length > 0)
            {
                text = char.ToUpper(text[0]) + text.Substring(1);
            }

            return text;
        }

        private static bool IsValidSubject(string? val)
        {
            if (string.IsNullOrWhiteSpace(val) || val.Length < 5) return false;
            var lower = val.ToLowerInvariant();
            if (lower.Contains("file:///") || lower.Contains("http://") || lower.Contains("https://")) return false;
            if (lower.Contains("email:") || lower.Contains("điện thoại:") || lower.Contains("mã số thuế:")) return false;
            if (lower.Contains(".gov.vn") || lower.Contains("@")) return false;
            return true;
        }
    }
}
