using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AiOcrService.Models;
using AiOcrService.Services.Glyphs;

namespace AiOcrService.Services.Extraction
{
    /// <summary>
    /// Bóc tách Số ký hiệu văn bản (VD: 1234/QĐ-UBND). Toàn bộ logic Bước 5 giữ NGUYÊN VẸN,
    /// KHÔNG đổi hành vi. BƯỚC 6 chỉ thêm 1 khối fallback DUY NHẤT ở cuối hàm Extract: nếu sau
    /// tất cả các bước trên (regex tĩnh, đối chiếu filename, đọc PDF widget) mà ReferenceNumber
    /// VẪN rỗng, mới thử rule động từ DB (xem DynamicRuleFallbackApplier.cs). Vì đây là fallback
    /// đặt SAU CÙNG và chỉ chạy khi kết quả đang rỗng, hành vi cũ (khi có rule động hay không)
    /// hoàn toàn không đổi trên mọi file đã trích đúng từ trước.
    /// </summary>
    public sealed class ReferenceNumberExtractor : IReferenceNumberExtractor
    {
        private readonly IGlyphNormalizer _glyphNormalizer;

        public ReferenceNumberExtractor(IGlyphNormalizer glyphNormalizer)
        {
            _glyphNormalizer = glyphNormalizer;
        }

        public void Extract(
            string headerText,
            string fullText,
            string? fileName,
            byte[]? pdfBytes,
            ExtractedDocumentData result,
            IReadOnlyList<OcrPatternRule>? dynamicRules = null)
        {
            // Trích xuất thông tin Widget nhị phân từ PDF nếu có (cho số hiệu Top-Left và ngày tháng Top-Right)
            var widgetData = PdfWidgetTextReader.ExtractWidgets(pdfBytes);

            // Tách headerText thành vùng Top-Left và Top-Right
            // Trong văn bản hành chính Việt Nam:
            // Top-Left: Cơ quan ban hành (dòng 1, 2) và Số ký hiệu (Số: .../...)
            // Top-Right: Quốc hiệu, Tiêu ngữ, và Địa danh - ngày tháng
            var (topLeftText, topRightText) = ExtractionTextUtils.SplitHeaderLeftRight(headerText);

            // ===== ƯU TIÊN 1: BÊN TRÁI TRÊN (TOP-LEFT) =====
            // Quy tắc: Đừng lấy thiếu số ký hiệu. Nếu xác định được ở Ưu tiên 1 thì dừng ngay, không tiếp tục.
            string? p1Ref = TryExtractReferenceNumberFromZone(topLeftText, widgetData?.LeftRefNum, fileName, isLeftZone: true);
            if (!string.IsNullOrWhiteSpace(p1Ref))
            {
                result.ReferenceNumber = p1Ref;
                return; // Đã xác định được ở Ưu tiên 1 -> DỪNG NGAY!
            }

            // Thử quét lại trên toàn bộ headerText nếu topLeftText bị cắt lệch do format OCR dính dòng
            string? p1HeaderFallback = TryExtractReferenceNumberFromZone(headerText, widgetData?.LeftRefNum, fileName, isLeftZone: true);
            if (!string.IsNullOrWhiteSpace(p1HeaderFallback))
            {
                result.ReferenceNumber = p1HeaderFallback;
                return; // Đã xác định được -> DỪNG NGAY!
            }

            // ===== ƯU TIÊN 2: BÊN PHẢI TRÊN (TOP-RIGHT) =====
            // Chỉ khi Ưu tiên 1 không tìm thấy số ký hiệu, mới quét sang vùng bên phải trên
            string? p2Ref = TryExtractReferenceNumberFromZone(topRightText, null, fileName, isLeftZone: false);
            if (!string.IsNullOrWhiteSpace(p2Ref))
            {
                result.ReferenceNumber = p2Ref;
                return; // Đã xác định được ở Ưu tiên 2 -> DỪNG NGAY!
            }

            // ===== FALLBACK CUỐI CÙNG: RULE ĐỘNG TỪ DATABASE =====
            // Chỉ chạy khi cả Ưu tiên 1 và Ưu tiên 2 tĩnh đều không tìm thấy gì
            if (string.IsNullOrWhiteSpace(result.ReferenceNumber))
            {
                var dynamicVal = DynamicRuleFallbackApplier.TryExtract(fullText, OcrRuleTypes.ReferenceNumber, dynamicRules);
                if (!string.IsNullOrWhiteSpace(dynamicVal))
                {
                    result.ReferenceNumber = dynamicVal;
                }
            }
        }

        private string? TryExtractReferenceNumberFromZone(string zoneText, string? widgetNum, string? fileName, bool isLeftZone)
        {
            if (string.IsNullOrWhiteSpace(zoneText)) return null;

            // 1. Khớp cấu trúc Số: [chữ số]/[mã cơ quan viết tắt], loại trừ các trích dẫn nghị định, thông tư
            var refMatches = Regex.Matches(zoneText,
                @"(?<!Nghị\s*định\s*|Nghi\s*dinh\s*|Thông\s*tư\s*|Thong\s*tu\s*|Luật\s*|Luat\s*|theo\s+)(?:Số|Sô|Sốc|Sộc|Sổ|Sé|So|Soc|No|Ref|s\?|s\:|s\.|s-|s6|86|8o|8ô)\s*[:.]?\s*([0-9A-Za-z\s,%#®©\[\]\?\>\/tTlIoOBbD\-_~¿]*?)\s*[\/\\|]\s*([A-ZĐa-z0-9\-_]+(?:\s*[\/\\|]\s*[A-ZĐa-z0-9\-_]+)*)",
                RegexOptions.IgnoreCase);

            string rawDigits = "";
            string suffix = "";

            foreach (Match rm in refMatches)
            {
                var candSuffix = CleanReferenceNumber(rm.Groups[2].Value);
                var candUpper = candSuffix.ToUpperInvariant();
                if (candUpper.Contains("NĐ-CP") || candUpper.Contains("ND-CP") ||
                    candUpper.Contains("TT-BTC") || candUpper.Contains("NQ-CP") ||
                    candUpper.Contains("QĐ-TTG") || candUpper.Contains("QD-TTG") ||
                    candUpper.Contains("LUẬT") || candUpper.Contains("LUAT") ||
                    candUpper.Contains("HEADER_ZONE") || candUpper.Contains("ZONE"))
                {
                    continue;
                }

                rawDigits = _glyphNormalizer.NormalizeOrFallback(rm.Groups[1].Value);
                suffix = candSuffix;

                // Xử lý trường hợp lồng slash hoặc năm nằm trong rawDigits (VD: /2026/VBHN-TT-BNNMT)
                if (rawDigits.Length == 4 && (rawDigits.StartsWith("19") || rawDigits.StartsWith("20")) && !suffix.Contains(rawDigits))
                {
                    suffix = $"{rawDigits}/{suffix}";
                    rawDigits = "";
                }

                // Xử lý trường hợp lồng slash: VD suffix = "2026/VBHN-TT-BTP"
                var nestedSlashMatch = Regex.Match(suffix, @"^(\d{1,5})\/([A-ZĐa-z0-9\-_]+.*)$");
                if (nestedSlashMatch.Success)
                {
                    var nestedDigits = nestedSlashMatch.Groups[1].Value;
                    var nestedRemaining = nestedSlashMatch.Groups[2].Value;

                    // Nếu có widgetNum (như "05" ở góc trái trên) thì số hiệu chính là widgetNum,
                    // và "2026/VBHN-TT-BTP" là nguyên vẹn suffix năm + mã cơ quan
                    if (!string.IsNullOrWhiteSpace(widgetNum))
                    {
                        rawDigits = widgetNum;
                        // giữ nguyên suffix là "2026/VBHN-TT-BTP" để không mất năm và không mất số 05
                    }
                    else if (string.IsNullOrWhiteSpace(rawDigits))
                    {
                        rawDigits = nestedDigits;
                        suffix = nestedRemaining;
                    }
                }
                break;
            }

            if (string.IsNullOrWhiteSpace(suffix))
            {
                // Thử tìm mẫu số không có dấu gạch chéo do lỗi OCR (VD: sé:ƒ3syt-rckT)
                var noSlashMatch = Regex.Match(zoneText,
                    @"(?:Số|Sô|Sốc|Sộc|Sổ|Sé|So|Soc|No|Ref|s\?|s\:|s\.|s-|s6|86|8o|8ô)\s*[:.]?\s*([0-9A-Za-z\s,%#®©\[\]\?\>\/tTlIoOBbD\-_~¿ƒ]*?)\s*[-_]?\s*(syt[-_]?[a-z0-9\-_]+|ubnd[-_]?[a-z0-9\-_]+|stp[-_]?[a-z0-9\-_]+|tckt[-_]?[a-z0-9\-_]+)",
                    RegexOptions.IgnoreCase);
                if (noSlashMatch.Success)
                {
                    rawDigits = _glyphNormalizer.NormalizeOrFallback(noSlashMatch.Groups[1].Value);
                    suffix = CleanReferenceNumber(noSlashMatch.Groups[2].Value);
                }
                else
                {
                    // Fallback tìm số hiệu \d+/\w+
                    var fallbackMatches = Regex.Matches(zoneText, @"\b([0-9]{1,5})\s*[\/\\|]\s*([A-ZĐa-z0-9\-_]+(?:\s*[\/\\|]\s*[A-ZĐa-z0-9\-_]+)*)");
                    foreach (Match fm in fallbackMatches)
                    {
                        var candSuffix = CleanReferenceNumber(fm.Groups[2].Value);
                        var candUpper = candSuffix.ToUpperInvariant();
                        if (candUpper.Contains("NĐ-CP") || candUpper.Contains("ND-CP") ||
                            candUpper.Contains("TT-BTC") || candUpper.Contains("NQ-CP") ||
                            candUpper.Contains("QĐ-TTG") || candUpper.Contains("QD-TTG") ||
                            candUpper.Contains("LUẬT") || candUpper.Contains("LUAT"))
                        {
                            continue;
                        }
                        rawDigits = fm.Groups[1].Value;
                        suffix = candSuffix;
                        break;
                    }
                }
            }

            // Tích hợp số từ Widget PDF (ĐẶC BIỆT QUAN TRỌNG: Không để thiếu số ký hiệu)
            if (!string.IsNullOrWhiteSpace(widgetNum))
            {
                if (string.IsNullOrWhiteSpace(rawDigits) ||
                    (rawDigits.Length == 4 && (rawDigits.StartsWith("20") || rawDigits.StartsWith("19")) && !suffix.Contains(rawDigits)))
                {
                    // Nếu rawDigits là năm (như 2026) mà widget là số ngắn hơn (như 05),
                    // ghép năm vào suffix để tạo thành 05/2026/...
                    if (rawDigits.Length == 4 && !string.IsNullOrWhiteSpace(suffix))
                    {
                        suffix = $"{rawDigits}/{suffix}";
                    }
                    rawDigits = widgetNum;
                }
            }

            // Đối chiếu chéo với Filename (nếu có)
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                var matchedFnNumber = ExtractNumberFromFileName(fileName, suffix);
                if (!string.IsNullOrWhiteSpace(matchedFnNumber))
                {
                    if (string.IsNullOrWhiteSpace(rawDigits) || rawDigits.Length < matchedFnNumber.Length)
                    {
                        rawDigits = matchedFnNumber;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(rawDigits) && !string.IsNullOrWhiteSpace(suffix))
            {
                return $"{rawDigits}/{suffix}";
            }
            else if (!string.IsNullOrWhiteSpace(suffix) && !string.IsNullOrWhiteSpace(widgetNum))
            {
                return $"{widgetNum}/{suffix}";
            }
            else if (!string.IsNullOrWhiteSpace(suffix))
            {
                return $"/{suffix}";
            }

            return null;
        }

        private static string? ExtractNumberFromFileName(string fileName, string? suffix)
        {
            var cleanFn = Regex.Replace(fileName, @"^.*attachments_\d+_\d+_", "", RegexOptions.IgnoreCase);
            cleanFn = Regex.Replace(cleanFn, @"^.*[\\/]", "");
            cleanFn = Regex.Replace(cleanFn, @"\.[^.]+$", "");
            var cleanFnNorm = ExtractionTextUtils.RemoveDiacritics(cleanFn).ToLowerInvariant();

            var suffixTokens = Regex.Matches(suffix ?? "", @"[A-ZĐa-z]{2,}")
                .Cast<Match>()
                .Select(m => ExtractionTextUtils.RemoveDiacritics(m.Value).ToLowerInvariant())
                .Distinct()
                .ToList();

            foreach (var token in suffixTokens)
            {
                var adjMatch = Regex.Match(cleanFnNorm, $@"(?:^|[^\d])(\d{{1,5}})[-_]?{Regex.Escape(token)}");
                if (adjMatch.Success) return adjMatch.Groups[1].Value;

                var postMatch = Regex.Match(cleanFnNorm, $@"{Regex.Escape(token)}[-_]?(\d{{1,5}})");
                if (postMatch.Success) return postMatch.Groups[1].Value;
            }

            var leadingNumMatch = Regex.Match(cleanFnNorm, @"^(\d{1,5})[-_]");
            if (leadingNumMatch.Success)
            {
                var cand = leadingNumMatch.Groups[1].Value;
                if (!(cand.StartsWith("20") && cand.Length == 4)) return cand;
            }

            var generalFnMatch = Regex.Match(cleanFnNorm, @"(?:^|vb_?|cv_?|qd_?|tb_?|kh_?|ct_?|nq_?|ttr_?|bc_?)(\d{1,5})(?:[-_a-z]|$)", RegexOptions.IgnoreCase);
            if (generalFnMatch.Success)
            {
                var cand = generalFnMatch.Groups[1].Value;
                if (!(cand.StartsWith("20") && cand.Length == 4)) return cand;
            }

            return null;
        }

        private static string CleanReferenceNumber(string val)
        {
            if (string.IsNullOrWhiteSpace(val)) return string.Empty;
            val = Regex.Replace(val, @"\s*[\/\\|]\s*", "/");
            val = Regex.Replace(val, @"(?:CỘNG|CONG|ĐỘC|DOC|HÒA|HOA|LẬP|LAP).*$", "", RegexOptions.IgnoreCase).Trim();

            // Sửa các lỗi OCR phổ biến trong mã cơ quan hành chính
            val = Regex.Replace(val, @"\bSTIP\b", "STP", RegexOptions.IgnoreCase);
            val = Regex.Replace(val, @"\bBMDN\b", "ĐMDN", RegexOptions.IgnoreCase);
            val = Regex.Replace(val, @"\bBGDDT\b", "BGDĐT", RegexOptions.IgnoreCase);
            val = Regex.Replace(val, @"\bSGDDT\b", "SGDĐT", RegexOptions.IgnoreCase);
            val = Regex.Replace(val, @"\bsyt[-_]?r?ckt\b", "SYT-TCKT", RegexOptions.IgnoreCase);
            val = Regex.Replace(val, @"\bsyt[-_]?tckt\b", "SYT-TCKT", RegexOptions.IgnoreCase);

            // Cắt bỏ chữ dính vào đuôi năm (VD: 2026Th do dính chữ Thành phố / Tháng)
            val = Regex.Replace(val, @"(?<=\b\d{4})[A-Za-zÀ-ỹ]+$", "");

            // Cắt bỏ chữ dính vào cuối mã cơ quan (VD: QD-UBNDTh do OCR dính chữ Thành phố)
            val = Regex.Replace(val, @"(?<=[A-ZĐ]{2,})[A-ZĐ][a-zà-ỹ]+$", "");
            val = Regex.Replace(val, @"(?<=UBNDBN)B$", "", RegexOptions.IgnoreCase);

            // Sửa các biến dạng OCR phổ biến cho Quyết định
            val = val.Replace("Q-UBND", "QĐ-UBND");
            val = val.Replace("QO-UBND", "QĐ-UBND");
            val = val.Replace("QD-UBND", "QĐ-UBND");

            val = val.TrimEnd('.', ',', ';', ':', '-', ' ', '~', '¿');
            return val;
        }
    }
}
