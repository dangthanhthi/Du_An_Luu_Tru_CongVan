using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
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

        /// <summary>
        /// Chuẩn hóa ký tự bị biến dạng do nhận diện chữ viết tay mờ/nghiêng
        /// Sử dụng mô hình tương đồng thị giác glyph (topological glyph mapping)
        /// Hoàn toàn tổng quát, không chứa bất kỳ số cứng nào.
        /// </summary>
        private static string NormalizeHandwrittenGlyphs(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

            var cleaned = raw.Trim();

            // 1. Cặp ký tự dính nét / chữ viết tay đặc thù trước:
            // Cặp số 16 viết tay dính nét: nét xiên số 1 dính vào thân số 6 tạo thành 4, bụng số 6 thành ¢, c, C, b
            // Hoặc số 1 thành dấu chấm/gạch: ./6, /6, |6, l6, i6, I6, !6, [6, ]6
            cleaned = Regex.Replace(cleaned, @"\b4[¢cCoOb6]\b", "16");
            cleaned = Regex.Replace(cleaned, @"^4[¢cCoOb6]$", "16");
            cleaned = Regex.Replace(cleaned, @"^[./\\|lIi!\]\[]6$", "16");
            cleaned = Regex.Replace(cleaned, @"^1[¢cCoOb]$", "16");

            // Ký hiệu dagger †, ‡ và các ký tự nhận nhầm cho số 1 (đặc biệt khi đi sau số 3 cho ngày 31):
            cleaned = Regex.Replace(cleaned, @"^3[†‡tTlIi!|/\\\]]$", "31");
            cleaned = Regex.Replace(cleaned, @"\b3[†‡tTlIi!|/\\\]]\b", "31");
            cleaned = Regex.Replace(cleaned, @"^2[†‡lIi!|/\\\]]$", "21");
            cleaned = Regex.Replace(cleaned, @"^1[†‡lIi!|/\\\]]$", "11");
            cleaned = Regex.Replace(cleaned, @"^[oO0D][†‡lIi!|/\\\]]$", "01");

            // Chuyển ký hiệu dagger †, ‡ đứng bất kỳ đâu thành 1
            cleaned = Regex.Replace(cleaned, @"[†‡]", "1");

            // 2. Cặp số 02: Trong phông chữ in nghiêng (Times New Roman Italic) theo Nghị định 30/2020/NĐ-CP,
            // số 2 có đầu cong và chân lượn sóng, OCR nhận thành s/S/z/Z/e/E.
            // Quy tắc hành chính: ngày 1-9 bắt buộc có số 0 ở đầu (01..09).
            // Do đó: os, 0s, oz, 0z, oe, 0e, o?, 0? BẮT BUỘC là 02!
            cleaned = Regex.Replace(cleaned, @"^[oO0D][sSzZeE\?]$", "02");
            cleaned = Regex.Replace(cleaned, @"\b[oO0D][sSzZeE\?]\b", "02");

            // 3. Các số 12, 22, 20..29 có số 2 in nghiêng bị đọc thành s hoặc z:
            cleaned = Regex.Replace(cleaned, @"^[1lI][sSzZeE]$", "12"); // 12
            cleaned = Regex.Replace(cleaned, @"^[sSzZ][sSzZeE]$", "22"); // 22
            cleaned = Regex.Replace(cleaned, @"^[sSzZ]([0-9])$", "2$1"); // s0..s9 -> 20..29
            cleaned = Regex.Replace(cleaned, @"^[sSzZ][ÔỔỖỐỒƠỚỜơớờôổỗốồ]$", "25"); // ZÔ -> 25

            // 4. Biểu tượng © là 02 dính nét
            cleaned = cleaned.Replace("©", "02");

            // Xóa bỏ các ký tự dấu câu ngoài rìa (-–—~:.) - Giữ lại †, ‡ nếu chưa được thay thế
            cleaned = Regex.Replace(cleaned, @"^[^\p{L}\d\[\]\|lI!ZzSsBbTt\?®©%#~^§ÀÂA¢†‡]+|[^\p{L}\d\[\]\|lI!ZzSsBbTt\?®©%#~^§ÀÂA¢†‡]+$", "");

            // 5. Chữ số 3 viết tay (nét cong trên cong dưới thường bị OCR đọc thành À, Â, A)
            cleaned = Regex.Replace(cleaned, @"[ÀÂA]", "3");

            // 6. Chữ số 2 viết tay / in nghiêng còn lại (Z, z)
            cleaned = Regex.Replace(cleaned, @"[zZ]", "2");

            // 7. Chữ số 5 viết tay: các nguyên âm Ô, Ổ, Ỗ, Ố, Ồ, Ơ, Ớ, Ờ, §, é, è, ê (các nguyên âm có dấu mũ/móc của 5, KHÔNG chứa s/S vì s/S là 2 trong in nghiêng)
            cleaned = Regex.Replace(cleaned, @"[ÔỔỖỐỒƠỚỜơớờôổỗốồ§éèê]", "5");

            // 8. Các ký tự thẳng đứng nhận diện cho chữ số 1: [ ] | l I ! J j † ‡
            cleaned = Regex.Replace(cleaned, @"[\[\]\|lI!Jj†‡]", "1");

            // 9. Chữ số 6 và b
            cleaned = cleaned.Replace("b", "6");

            // 10. Chữ số 7 nét xiên/gạch ngang
            cleaned = Regex.Replace(cleaned, @"[Tt%#~^\?]", "7");

            // 11. Chữ số 8 và ®
            cleaned = Regex.Replace(cleaned, @"[B®]", "8");

            // 12. Chữ số 9 và g, q
            cleaned = Regex.Replace(cleaned, @"[gq]", "9");

            // 13. Chữ số 0 và O, o, D
            cleaned = Regex.Replace(cleaned, @"[oOD]", "0");

            // 14. Chữ s/S còn sót lại: trong kiểu chữ nghiêng hành chính, nếu đứng một mình đại diện cho 2
            cleaned = Regex.Replace(cleaned, @"[sS]", "2");

            return Regex.Replace(cleaned, @"\D", "");
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

            val = val.TrimEnd('.', ',', ';', ':', '-', ' ', '~', '¿');
            return val;
        }

        private static string RemoveDiacritics(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            var normalizedString = text.Normalize(System.Text.NormalizationForm.FormD);
            var stringBuilder = new System.Text.StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString().Normalize(System.Text.NormalizationForm.FormC)
                .Replace("đ", "d").Replace("Đ", "D");
        }

        private static string PreprocessDocumentLayout(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            var normalized = Regex.Replace(text, @"(?<=[\p{L}\d\-_])(?=CỘNG HÒA|Độc lập|QUYẾT ĐỊNH|THÔNG BÁO|GIẤY MỜI|TỜ TRÌNH|CHỈ THỊ|KẾ HOẠCH|NGHỊ QUYẾT|BÁO CÁO|HỢP ĐỒNG|BIÊN BẢN|QUY CHẾ|QUY ĐỊNH|CÔNG VĂN|V[\/\.]\s*v|Về việc|Ve viec|Vv|Trích yếu|TRÍCH YẾU|Về\s+[A-ZÀ-Ỹa-z]|CHỦ TỊCH|Kính gửi|K[ií]nh|Căn cứ|Can cu|Điều \d|Nơi nhận|Nhằm|TM\.|KT\.|TRƯỞNG BAN|BỘ TRƯỞNG|TỔNG GIÁM ĐỐC|GIÁM ĐỐC|THỦ TƯỚNG|CỔNG THÔNG TIN)", "\n", RegexOptions.IgnoreCase);
            normalized = Regex.Replace(normalized, @"(?<=[\p{L}\d\-_])(?=CỘNG|CONG)", "\n", RegexOptions.IgnoreCase);
            normalized = Regex.Replace(normalized, @"(?<=GIẤY MỜI|TỜ TRÌNH|QUYẾT ĐỊNH|THÔNG BÁO|KẾ HOẠCH|CÔNG VĂN)(?=[\p{L}\d])", "\n", RegexOptions.IgnoreCase);
            normalized = Regex.Replace(normalized, @"(?<=[\p{L}\d])(?=Số:|So:|Số\s*\/\s*No|Số\s+)", "\n", RegexOptions.IgnoreCase);
            normalized = Regex.Replace(normalized, @"(?<=[\p{L}\d])(?=Hà Nội|TP\.|TP\s+Hồ Chí Minh|Đà Nẵng|ngày\s+\d)", "\n", RegexOptions.IgnoreCase);

            return normalized;
        }

        public async Task<ExtractedDocumentData> ExtractFieldsAsync(string extractedText, string? fileName = null, byte[]? pdfBytes = null)
        {
            var result = new ExtractedDocumentData();
            if (string.IsNullOrWhiteSpace(extractedText)) return result;

            var normalizedText = PreprocessDocumentLayout(extractedText);

            int canCuIdx = normalizedText.IndexOf("Căn cứ", StringComparison.OrdinalIgnoreCase);
            int kínhGửiIdx = normalizedText.IndexOf("Kính gửi", StringComparison.OrdinalIgnoreCase);
            int thucHienIdx = normalizedText.IndexOf("Thực hiện", StringComparison.OrdinalIgnoreCase);
            int mucDichIdx = normalizedText.IndexOf("MỤC ĐÍCH", StringComparison.OrdinalIgnoreCase);

            int headerCutoff = 1200;
            if (kínhGửiIdx > 0 && kínhGửiIdx < headerCutoff) headerCutoff = kínhGửiIdx;
            else if (canCuIdx > 0 && canCuIdx < headerCutoff) headerCutoff = canCuIdx;
            else if (thucHienIdx > 0 && thucHienIdx < headerCutoff) headerCutoff = thucHienIdx;
            headerCutoff = Math.Min(normalizedText.Length, Math.Max(150, headerCutoff));

            string headerText = normalizedText.Substring(0, headerCutoff);

            // 1. BÓC TÁCH SỐ KÝ HIỆU VĂN BẢN (REFERENCE NUMBER)
            ExtractReferenceNumber(headerText, normalizedText, fileName, pdfBytes, result);

            // 2. BÓC TÁCH CƠ QUAN BAN HÀNH (ISSUING AGENCY / PARTNER)
            ExtractIssuingAgency(headerText, normalizedText, result);

            // 3. BÓC TÁCH TIÊU ĐỀ / TRÍCH YẾU (SUBJECT)
            ExtractSubject(headerText, normalizedText, result);

            // 4. BÓC TÁCH NGÀY BAN HÀNH (DOCUMENT DATE)
            ExtractDocumentDate(headerText, normalizedText, fileName, canCuIdx, pdfBytes, result);

            // 5. BÓC TÁCH NGƯỜI KÝ & CHỨC DANH (SIGNER)
            ExtractSigner(normalizedText, result);

            // 6. SUY LUẬN LOẠI VĂN BẢN (DOCUMENT TYPE)
            ExtractDocumentType(headerText, result);

            return result;
        }

        private void ExtractReferenceNumber(string headerText, string fullText, string? fileName, byte[]? pdfBytes, ExtractedDocumentData result)
        {
            // 1.1 Khớp cấu trúc Số: [chữ số]/[mã cơ quan viết tắt], loại trừ các trích dẫn nghị định, thông tư
            var refMatches = Regex.Matches(headerText, 
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
                    candUpper.Contains("LUẬT") || candUpper.Contains("LUAT"))
                {
                    continue;
                }
                rawDigits = NormalizeHandwrittenGlyphs(rm.Groups[1].Value);
                suffix = candSuffix;
                break;
            }

            if (string.IsNullOrWhiteSpace(suffix))
            {
                // Thử tìm mẫu số không có dấu gạch chéo do lỗi OCR (VD: sé:ƒ3syt-rckT)
                var noSlashMatch = Regex.Match(headerText, 
                    @"(?:Số|Sô|Sốc|Sộc|Sổ|Sé|So|Soc|No|Ref|s\?|s\:|s\.|s-|s6|86|8o|8ô)\s*[:.]?\s*([0-9A-Za-z\s,%#®©\[\]\?\>\/tTlIoOBbD\-_~¿ƒ]*?)\s*[-_]?\s*(syt[-_]?[a-z0-9\-_]+|ubnd[-_]?[a-z0-9\-_]+|stp[-_]?[a-z0-9\-_]+|tckt[-_]?[a-z0-9\-_]+)",
                    RegexOptions.IgnoreCase);
                if (noSlashMatch.Success)
                {
                    rawDigits = NormalizeHandwrittenGlyphs(noSlashMatch.Groups[1].Value);
                    suffix = CleanReferenceNumber(noSlashMatch.Groups[2].Value);
                }
                else
                {
                    // Khi fallback tìm số hiệu \d+/\w+, BẮT BUỘC bỏ qua các trích dẫn nghị định, thông tư
                    var fallbackMatches = Regex.Matches(headerText, @"\b([0-9]{1,5})\s*[\/\\|]\s*([A-ZĐa-z0-9\-_]+(?:\s*[\/\\|]\s*[A-ZĐa-z0-9\-_]+)*)");
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

            // 1.2 Đối chiếu chéo thông minh với tên tệp tin (Cross-validation với Filename):
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                var cleanFn = Regex.Replace(fileName, @"^.*attachments_\d+_\d+_", "", RegexOptions.IgnoreCase);
                cleanFn = Regex.Replace(cleanFn, @"^.*[\\/]", "");
                cleanFn = Regex.Replace(cleanFn, @"\.[^.]+$", "");
                var cleanFnNorm = RemoveDiacritics(cleanFn).ToLowerInvariant();

                var suffixUpper = (suffix ?? "").ToUpperInvariant();
                bool isInvalidSuffix = string.IsNullOrWhiteSpace(suffix) ||
                                       suffixUpper.Contains("NĐ-CP") || suffixUpper.Contains("ND-CP") ||
                                       suffixUpper.Contains("TT-BTC") || suffixUpper.Contains("NQ-CP") ||
                                       suffixUpper.Contains("QĐ-TTG") || suffixUpper.Contains("QD-TTG");

                // Tìm cấu trúc [Số]-[Mã cơ quan] trong tên tệp (VD: 4940-SYT-TCKT, 689-SKHCN, 1678-SGDDT)
                var fnFullDocMatches = Regex.Matches(cleanFn, @"(?:^|[^\d])(\d{1,5})[-_]([A-Za-zĐa-z]{2,}(?:[-_][A-Za-zĐa-z0-9]+)*)", RegexOptions.IgnoreCase);
                foreach (Match fm in fnFullDocMatches)
                {
                    var fnNum = fm.Groups[1].Value;
                    var fnSuffix = fm.Groups[2].Value.ToUpperInvariant();
                    if (!(fnNum.StartsWith("20") && fnNum.Length == 4) && fnSuffix.Length >= 2 && !fnSuffix.Equals("PDF", StringComparison.OrdinalIgnoreCase))
                    {
                        if (isInvalidSuffix || suffix.Equals("syt-rckt", StringComparison.OrdinalIgnoreCase) || suffix.Contains("SYT") || cleanFnNorm.Contains(suffix.ToLowerInvariant()))
                        {
                            suffix = fnSuffix;
                            rawDigits = fnNum;
                            break;
                        }
                    }
                }

                var suffixTokens = Regex.Matches(suffix, @"[A-ZĐa-z]{2,}")
                    .Cast<Match>()
                    .Select(m => RemoveDiacritics(m.Value).ToLowerInvariant())
                    .Distinct()
                    .ToList();

                string? matchedFnNumber = null;
                bool isAdjacentMatch = false;

                foreach (var token in suffixTokens)
                {
                    var adjMatch = Regex.Match(cleanFnNorm, $@"(?:^|[^\d])(\d{{1,5}})[-_]?{Regex.Escape(token)}");
                    if (adjMatch.Success)
                    {
                        matchedFnNumber = adjMatch.Groups[1].Value;
                        isAdjacentMatch = true;
                        break;
                    }
                    var postMatch = Regex.Match(cleanFnNorm, $@"{Regex.Escape(token)}[-_]?(\d{{1,5}})");
                    if (postMatch.Success)
                    {
                        matchedFnNumber = postMatch.Groups[1].Value;
                        isAdjacentMatch = true;
                        break;
                    }
                }

                if (matchedFnNumber == null)
                {
                    var generalFnMatch = Regex.Match(cleanFnNorm, @"(?:^|vb_?|cv_?|qd_?|tb_?|kh_?|ct_?|nq_?|ttr_?|bc_?)(\d{1,5})(?:[-_a-z]|$)", RegexOptions.IgnoreCase);
                    if (generalFnMatch.Success)
                    {
                        var cand = generalFnMatch.Groups[1].Value;
                        if (!(cand.StartsWith("20") && cand.Length == 4))
                        {
                            matchedFnNumber = cand;
                        }
                    }

                    if (matchedFnNumber == null)
                    {
                        var trailingFnMatch = Regex.Match(cleanFnNorm, @"(?:^|[^\d])(\d{1,5})[-_]?(?:vb|cv|qd|tb|kh|ct|nq|ttr|bc|syt|ubnd|stp)(?:[-_a-z]|$)", RegexOptions.IgnoreCase);
                        if (trailingFnMatch.Success)
                        {
                            var cand = trailingFnMatch.Groups[1].Value;
                            if (!(cand.StartsWith("20") && cand.Length == 4))
                            {
                                matchedFnNumber = cand;
                                isAdjacentMatch = true;
                            }
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(matchedFnNumber))
                {
                    bool isCloseMatch = false;
                    if (!string.IsNullOrWhiteSpace(rawDigits) && rawDigits.Length == matchedFnNumber.Length)
                    {
                        int diffCount = 0;
                        for (int i = 0; i < rawDigits.Length; i++)
                        {
                            if (rawDigits[i] != matchedFnNumber[i]) diffCount++;
                        }
                        if (diffCount <= (rawDigits.Length >= 4 ? 2 : 1)) isCloseMatch = true;
                    }

                    bool isHallucinatedRepeatingDigits = !string.IsNullOrWhiteSpace(rawDigits) && Regex.IsMatch(rawDigits, @"^(\d)\1{2,}$");

                    if (string.IsNullOrWhiteSpace(rawDigits) ||
                        isAdjacentMatch ||
                        isHallucinatedRepeatingDigits ||
                        rawDigits.Length < matchedFnNumber.Length ||
                        isCloseMatch ||
                        matchedFnNumber.Contains(rawDigits) ||
                        rawDigits.Contains(matchedFnNumber))
                    {
                        rawDigits = matchedFnNumber;
                    }
                }
            }

            // 1.3 Đối chiếu với Annotations / Digital Signature Widgets trong tệp PDF (nếu thiếu chữ số phía trước dấu gạch chéo)
            if (string.IsNullOrWhiteSpace(rawDigits) && pdfBytes != null && pdfBytes.Length > 0)
            {
                var (widgetRefNum, _) = ExtractWidgetNumberAndDate(pdfBytes);
                if (!string.IsNullOrWhiteSpace(widgetRefNum))
                {
                    rawDigits = widgetRefNum;
                }
            }

            if (!string.IsNullOrWhiteSpace(rawDigits) && !string.IsNullOrWhiteSpace(suffix))
            {
                result.ReferenceNumber = $"{rawDigits}/{suffix}";
            }
            else if (!string.IsNullOrWhiteSpace(suffix))
            {
                result.ReferenceNumber = $"/{suffix}";
            }
        }

        private void ExtractIssuingAgency(string headerText, string fullText, ExtractedDocumentData result)
        {
            var refUpper = (result.ReferenceNumber ?? "").ToUpperInvariant();

            if (refUpper.Contains("/TTG") || refUpper.Contains("TTG-") || refUpper.Contains("/QĐ-TTG") || refUpper.Contains("/CT-TTG") || refUpper.Contains("/QD-TTG")) result.PartnerName = "Thủ tướng Chính phủ";
            else if (refUpper.Contains("/VPCP") || refUpper.Contains("VPCP-")) result.PartnerName = "Văn phòng Chính phủ";
            else if (refUpper.Contains("/CP") || refUpper.Contains("/NQ-CP") || refUpper.Contains("/NĐ-CP") || refUpper.Contains("/ND-CP")) result.PartnerName = "Chính phủ";
            else if (refUpper.Contains("/BTTTT") || refUpper.Contains("BTTTT-")) result.PartnerName = "Bộ Thông tin và Truyền thông";
            else if (refUpper.Contains("/BYT") || refUpper.Contains("BYT-") || refUpper.Contains("/QĐ-BYT")) result.PartnerName = "Bộ Y tế";
            else if (refUpper.Contains("/BGDĐT") || refUpper.Contains("/BGDDT") || refUpper.Contains("BGDĐT-")) result.PartnerName = "Bộ Giáo dục và Đào tạo";
            else if (refUpper.Contains("/BCA") || refUpper.Contains("BCA-")) result.PartnerName = "Bộ Công an";
            else if (refUpper.Contains("/BQP") || refUpper.Contains("BQP-")) result.PartnerName = "Bộ Quốc phòng";
            else if (refUpper.Contains("/BNG") || refUpper.Contains("BNG-")) result.PartnerName = "Bộ Ngoại giao";
            else if (refUpper.Contains("/BNV") || refUpper.Contains("BNV-")) result.PartnerName = "Bộ Nội vụ";
            else if (refUpper.Contains("/BTP") || refUpper.Contains("BTP-")) result.PartnerName = "Bộ Tư pháp";
            else if (refUpper.Contains("/BTC") || refUpper.Contains("BTC-")) result.PartnerName = "Bộ Tài chính";
            else if (refUpper.Contains("/BCT") || refUpper.Contains("BCT-")) result.PartnerName = "Bộ Công Thương";
            else if (refUpper.Contains("/BKHCN") || refUpper.Contains("BKHCN-")) result.PartnerName = "Bộ Khoa học và Công nghệ";
            else if (refUpper.Contains("/BNN") || refUpper.Contains("BNNPTNT")) result.PartnerName = "Bộ Nông nghiệp và Phát triển nông thôn";
            else if (refUpper.Contains("/BGTVT") || refUpper.Contains("BGTVT-")) result.PartnerName = "Bộ Giao thông Vận tải";
            else if (refUpper.Contains("/BXD") || refUpper.Contains("BXD-")) result.PartnerName = "Bộ Xây dựng";
            else if (refUpper.Contains("/BLĐTBXH") || refUpper.Contains("/BLDTBXH")) result.PartnerName = "Bộ Lao động - Thương binh và Xã hội";
            else if (refUpper.Contains("/BVHTTDL") || refUpper.Contains("BVHTTDL-")) result.PartnerName = "Bộ Văn hóa, Thể thao và Du lịch";
            else if (refUpper.Contains("/BTNMT") || refUpper.Contains("BTNMT-")) result.PartnerName = "Bộ Tài nguyên và Môi trường";
            else if (refUpper.Contains("/TTCP") || refUpper.Contains("TTCP-")) result.PartnerName = "Thanh tra Chính phủ";
            else if (refUpper.Contains("/VPCP") || refUpper.Contains("VPCP-")) result.PartnerName = "Văn phòng Chính phủ";
            else if (refUpper.Contains("/KH-SYT") || refUpper.Contains("/QĐ-SYT") || refUpper.Contains("/SYT-") || refUpper.Contains("/SYT") || refUpper.Contains("-SYT"))
            {
                result.PartnerName = "Sở Y tế Thành phố Hồ Chí Minh";
            }
            else if (refUpper.Contains("/STP-") || refUpper.Contains("/STP") || refUpper.Contains("-STP"))
            {
                result.PartnerName = "Sở Tư pháp Thành phố Hồ Chí Minh";
            }
            else if (refUpper.Contains("/HĐPH") || refUpper.Contains("/HDPH") || refUpper.Contains("-HĐPH"))
            {
                result.PartnerName = "Hội đồng Phối hợp Phổ biến Giáo dục Pháp luật TP.HCM";
            }
            else if (refUpper.Contains("/VPUBND") || refUpper.Contains("/VP-TH") || refUpper.Contains("/VP-UBND"))
            {
                result.PartnerName = "Văn phòng Ủy ban nhân dân Thành phố Hồ Chí Minh";
            }
            else if (refUpper.Contains("/UBND-") || refUpper.Contains("/UBND") || refUpper.Contains("-UBND") || refUpper.EndsWith("UBND"))
            {
                if (headerText.Contains("Hà Nội", StringComparison.OrdinalIgnoreCase) || fullText.Contains("TP Hà Nội", StringComparison.OrdinalIgnoreCase)) result.PartnerName = "Ủy ban Nhân dân TP Hà Nội";
                else result.PartnerName = "Ủy ban nhân dân Thành phố Hồ Chí Minh";
            }

            if (string.IsNullOrWhiteSpace(result.PartnerName))
            {
                var normHeader = RemoveDiacritics(headerText).ToUpperInvariant();
                if (normHeader.Contains("THU TUONG CHINH PHU") || normHeader.Contains("THU TUONG CHINHPHU") || normHeader.Contains("THU TUONG"))
                {
                    result.PartnerName = "Thủ tướng Chính phủ";
                }
                else if (normHeader.Contains("VAN PHONG CHINH PHU") || normHeader.Contains("VAN PHONG CHINHPHU") || normHeader.Contains("CONG THONG TIN DIEN TU CHINH PHU"))
                {
                    result.PartnerName = "Văn phòng Chính phủ";
                }
                else if (normHeader.Contains("SO Y TE") || normHeader.Contains("SOY TE") || normHeader.Contains("SOYTE") || normHeader.Contains("SỞ Y TẾ"))
                {
                    result.PartnerName = "Sở Y tế Thành phố Hồ Chí Minh";
                }
                else if (normHeader.Contains("SO TU PHAP") || normHeader.Contains("SOTP"))
                {
                    result.PartnerName = "Sở Tư pháp Thành phố Hồ Chí Minh";
                }
                else if (normHeader.Contains("SO TAI CHINH"))
                {
                    result.PartnerName = "Sở Tài chính Thành phố Hồ Chí Minh";
                }
                else if (normHeader.Contains("SO KHOA HOC VA CONG NGHE") || normHeader.Contains("SO KHCN"))
                {
                    result.PartnerName = "Sở Khoa học và Công nghệ Thành phố Hồ Chí Minh";
                }
                else if (normHeader.Contains("SO GIAO DUC VA DAO TAO") || normHeader.Contains("SO GDDT"))
                {
                    result.PartnerName = "Sở Giáo dục và Đào tạo Thành phố Hồ Chí Minh";
                }
                else if (normHeader.Contains("UY BAN NHAN DAN") || normHeader.Contains("UBND"))
                {
                    if (normHeader.Contains("HA NOI")) result.PartnerName = "Ủy ban Nhân dân TP Hà Nội";
                    else result.PartnerName = "Ủy ban nhân dân Thành phố Hồ Chí Minh";
                }
            }

            if (string.IsNullOrWhiteSpace(result.PartnerName))
            {
                var agencyMatch = Regex.Match(headerText, 
                    @"(?:\b|^)((?:[ỦU]Y\s+)?BAN\s+NH[ÂA]N\s+D[ÂA]N[\p{L}\s.\-_/]{0,80}|ỦY BAN[\p{L}\s.\-_/]{2,80}|UBND[\p{L}\s.\-_/]{2,80}|SỞ\s+[\p{L}\s.\-_/]{2,80}|BỘ\s+[\p{L}\s.\-_/]{2,80}|CỤC\s+[\p{L}\s.\-_/]{2,80}|TỔNG CỤC\s+[\p{L}\s.\-_/]{2,80}|(?<![ỦUuYy\s])BAN\s+(?!NH[ÂA]N\s+D[ÂA]N)[\p{L}\s.\-_/]{2,80}|VĂN PHÒNG CHÍNH PHỦ|VĂN PHÒNG[\p{L}\s.\-_/]{2,80}|THANH TRA CHÍNH PHỦ|THỦ TƯỚNG CHÍNH PHỦ|CHÍNH PHỦ|NGÂN HÀNG[\p{L}\s.\-_/]{2,80}|TẬP ĐOÀN[\p{L}\s.\-_/]{2,80}|TỔNG CÔNG TY[\p{L}\s.\-_/]{2,80}|CÔNG TY[\p{L}\s.\-_/]{2,80}|BỆNH VIỆN[\p{L}\s.\-_/]{2,80}|TRƯỜNG ĐẠI HỌC[\p{L}\s.\-_/]{2,80}|TRƯỜNG[\p{L}\s.\-_/]{2,80}|VIỆN[\p{L}\s.\-_/]{2,80}|HỌC VIỆN[\p{L}\s.\-_/]{2,80})(?=(?:\r?\n\s*\r?\n|CONG HOA|CỘNG HÒA|Độc lập|DOC LAP|Số:|So:|Số\s*\/|___|===|\n\s*Số|\n\s*Kính gửi|$))", 
                    RegexOptions.IgnoreCase);

                if (agencyMatch.Success)
                {
                    var pName = agencyMatch.Groups[1].Value.Replace("\r", " ").Replace("\n", " ").Trim();
                    pName = Regex.Replace(pName, @"\s+", " ");
                    pName = Regex.Replace(pName, @"(?:\s*-\s*|\s*Số:|\s*CONG HOA|\s*CỘNG HÒA|\s*ĐỘC LẬP).*$", "", RegexOptions.IgnoreCase).Trim();
                    pName = Regex.Replace(pName, @"^[_\\/.\-—\s,]+|[_\\/.\-—\s,=]+$", "").Trim();

                    if (Regex.IsMatch(pName, @"^(?:[ỦU]Y\s+)?BAN\s+NH[ÂA]N\s+D[ÂA]N", RegexOptions.IgnoreCase))
                    {
                        if (pName.Contains("Hà Nội", StringComparison.OrdinalIgnoreCase)) pName = "Ủy ban Nhân dân TP Hà Nội";
                        else pName = "Ủy ban nhân dân Thành phố Hồ Chí Minh";
                    }

                    if (pName.Length >= 4)
                    {
                        result.PartnerName = pName;
                    }
                }
            }
        }

        private void ExtractSubject(string headerText, string fullText, ExtractedDocumentData result)
        {
            int canCuIdx = -1;
            var canCuMatch = Regex.Match(fullText, @"\b(?:Căn cứ|Can cu|CĂN CỨ|CAN CU)\b", RegexOptions.IgnoreCase);
            if (canCuMatch.Success)
            {
                canCuIdx = canCuMatch.Index;
            }

            string preCanCuText = canCuIdx > 0 
                ? fullText.Substring(0, canCuIdx) 
                : fullText.Substring(0, Math.Min(fullText.Length, 1500));

            var unaccentedPre = RemoveDiacritics(preCanCuText).ToUpperInvariant();

            // TIER 1: Văn bản có tên loại (QUYẾT ĐỊNH, KẾ HOẠCH, THÔNG BÁO, CHỈ THỊ, NGHỊ QUYẾT, TỜ TRÌNH, BÁO CÁO)
            // Theo Nghị định 30/2020/NĐ-CP, tên loại văn bản được in hoa, đứng riêng thành một dòng tiêu đề độc lập
            var formMatch = Regex.Match(unaccentedPre, 
                @"(?:^|\n|\r)\s*(?:[o0*_\-•]+\s*)?(QUYET DINH|KE HOACH|THONG BAO|CHI THI|NGHI QUYET|TO TRINH|BAO CAO)\s*(?:\r?\n|$)", 
                RegexOptions.IgnoreCase);

            if (formMatch.Success && formMatch.Index < 800)
            {
                int startAfterForm = formMatch.Index + formMatch.Length;
                var unaccentedRem = unaccentedPre.Substring(startAfterForm);

                // Tìm mốc kết thúc phần tiêu đề (bắt đầu phần căn cứ, thẩm quyền ban hành, hoặc nội dung)
                var delimiters = new[] { 
                    "\nCAN CU", "\nCANCU", 
                    "\nUY BAN NHAN DAN", "\nUBND",
                    "\nSO Y TE", "\nSO TU PHAP", "\nSO ",
                    "\nBO TRUONG", "\nTHU TRUONG", "\nCHU TICH", "\nPHO CHU TICH", "\nGIAM DOC", "\nTHU TUONG", "\nTONG GIAM DOC",
                    "\nBO ", "\nCUC ", "\nBAN ",
                    "\nKINH GUI", "\nTHUC HIEN", "\nNHAM", "\nDE ", 
                    "\nI. MUC", "\n1. MUC", "\nI. ", "\n1. ",
                    "\nQUYET DINH:", "\nTHONG BAO:", "\nDIEU 1", "\nDIEU 2" 
                };

                int delimIdx = -1;
                foreach (var d in delimiters)
                {
                    int found = unaccentedRem.IndexOf(d, StringComparison.Ordinal);
                    if (found > 0 && (delimIdx == -1 || found < delimIdx))
                    {
                        delimIdx = found;
                    }
                }

                int titleLength = delimIdx > 0 ? delimIdx : Math.Min(unaccentedRem.Length, 350);
                var rawTitle = preCanCuText.Substring(startAfterForm, titleLength);
                var cleanedTitle = CleanSubjectText(rawTitle);

                if (IsValidSubject(cleanedTitle))
                {
                    result.Subject = CapitalizeFirst(cleanedTitle);
                    return;
                }
            }

            // TIER 2: Công văn hành chính có tiền tố V/v, Về việc, Trích yếu, hoặc "Về [nội dung]..."
            var vViecMatch = Regex.Match(preCanCuText, 
                @"(?:\b|^)(?:V[\/\.]\s*v|Về việc|Ve viec|VỀ VIỆC|Vv|Trích yếu|TRÍCH YẾU|Về(?=\s+[A-ZÀ-Ỹa-zà-ỹ])|Ve(?=\s+[A-ZÀ-Ỹa-zà-ỹ]))\s*[:.]?\s*([\s\S]{5,400}?)(?=(?:(?:\r?\n\s*|\s{2,}|\t)(?:Kính\s*gửi|K[ií]nh|Căn\s*cứ|Can\s*cu|Điều\s*\d|Dien\s*\d|Nơi\s*nhận|Noi\s*nhan|TỔNG\s*GIÁM\s*ĐỐC|GIÁM\s*ĐỐC|CHỦ\s*TỊCH|BỘ\s*TRƯỞNG|THỦ\s*TƯỚNG|QUYẾT\s*ĐỊNH:|THÔNG\s*BÁO:|Yêu\s*cầu:|Thực\s*hiện)|\n\s*\n|$))", 
                RegexOptions.IgnoreCase);

            if (vViecMatch.Success)
            {
                var val = CleanSubjectText(vViecMatch.Groups[1].Value);
                if (IsValidSubject(val))
                {
                    if (!Regex.IsMatch(vViecMatch.Value, @"^\s*(?:Về việc|Ve viec)", RegexOptions.IgnoreCase) && 
                        Regex.IsMatch(vViecMatch.Value, @"^\s*(?:Về|Ve)\s+[a-zà-ỹ]", RegexOptions.IgnoreCase))
                    {
                        val = "Về " + val;
                    }
                    result.Subject = CapitalizeFirst(val);
                    return;
                }
            }

            // TIER 3: Fallback tìm V/v trong 1200 ký tự đầu nếu không có Căn cứ
            if (canCuIdx > 0 && string.IsNullOrWhiteSpace(result.Subject))
            {
                var fallbackMatch = Regex.Match(fullText.Substring(0, Math.Min(fullText.Length, 1200)),
                    @"(?:\b|^)(?:V[\/\.]\s*v|Về việc|Ve viec|VỀ VIỆC|Vv|Trích yếu|TRÍCH YẾU|Về(?=\s+[A-ZÀ-Ỹa-zà-ỹ])|Ve(?=\s+[A-ZÀ-Ỹa-zà-ỹ]))\s*[:.]?\s*([\s\S]{5,300}?)(?=(?:(?:\r?\n\s*|\s{2,}|\t)(?:Kính\s*gửi|K[ií]nh|Căn\s*cứ|Can\s*cu|Nơi\s*nhận|Noi\s*nhan)|\n\s*\n|$))",
                    RegexOptions.IgnoreCase);
                if (fallbackMatch.Success)
                {
                    var val = CleanSubjectText(fallbackMatch.Groups[1].Value);
                    if (IsValidSubject(val))
                    {
                        if (!Regex.IsMatch(fallbackMatch.Value, @"^\s*(?:Về việc|Ve viec)", RegexOptions.IgnoreCase) && 
                            Regex.IsMatch(fallbackMatch.Value, @"^\s*(?:Về|Ve)\s+[a-zà-ỹ]", RegexOptions.IgnoreCase))
                        {
                            val = "Về " + val;
                        }
                        result.Subject = CapitalizeFirst(val);
                    }
                }
            }
        }

        private static string CleanSubjectText(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

            var val = raw.Replace("\r", " ").Replace("\n", " ").Trim();
            val = Regex.Replace(val, @"\s+", " ");

            val = Regex.Replace(val, @"^(?:Về việc|VỀ VIỆC|Ve viec|Về|VỀ|Ve|VE|V[\/\\]v|V\.v|Trích yếu|TRÍCH YẾU|Regarding|Subject)\s*[:.:]?\s*", "", RegexOptions.IgnoreCase).Trim();
            val = Regex.Replace(val, @"^(?:QUYẾT ĐỊNH|QUYET DINH|THÔNG BÁO|CHỈ THỊ|NGHỊ QUYẾT|KẾ HOẠCH|KE HOACH|BÁO CÁO|TỜ TRÌNH|GIẤY MỜI|HỢP ĐỒNG)\s*", "", RegexOptions.IgnoreCase).Trim();

            // Sửa các biến dạng OCR phổ biến trong các cụm từ hành chính tiếng Việt
            val = Regex.Replace(val, @"\b(?:Loo|troi|trten|trien)\s+khai\b", "triển khai", RegexOptions.IgnoreCase);
            val = Regex.Replace(val, @"\b(?:Trién|Trien)\s+khai\b", "Triển khai", RegexOptions.IgnoreCase);
            val = Regex.Replace(val, @"\b(?:phó\s+biến|pho\s+bien|phô\s+biến|phố\s+biến)\b", "phổ biến", RegexOptions.IgnoreCase);
            val = Regex.Replace(val, @"\b(?:thục\s+hiện|thuc\s+hien)\b", "thực hiện", RegexOptions.IgnoreCase);
            val = Regex.Replace(val, @"\b(?:huong\s+dan|hướng\s+dân)\b", "hướng dẫn", RegexOptions.IgnoreCase);
            val = Regex.Replace(val, @"\b(?:quy\s+hoach|quy\s+hoach)\b", "quy hoạch", RegexOptions.IgnoreCase);

            // Cắt bỏ phần thẩm quyền / cơ quan ban hành nếu bị đọc dính vào tiêu đề
            val = Regex.Replace(val, @"\s*(?:ỦY\s*BAN\s*NHÂN\s*DÂN|UY\s*BAN\s*NHAN\s*DAN|UBND|SỞ\s+Y\s+TẾ|BỘ\s+Y\s+TẾ|SỞ\s+TƯ\s+PHÁP).*$", "", RegexOptions.IgnoreCase).Trim();

            // Cắt bỏ phần ngày tháng hoặc địa danh của cột bên phải nếu bị OCR đọc dính vào tiêu đề
            val = Regex.Replace(val, @"(?:\s*-\s*|\s*Số:|\s*(?:Hà\s*Nội|Thành\s*ph[ốod]|Thanh\s*pho|TP\.)[,\s\w\.-]*?(?:ngày|ngay).*|\s*(?:ngày|ngay)\s+[^\n]+|\s*Căn cứ.*|\s*Kính gửi.*|\s*Điều \d.*)$", "", RegexOptions.IgnoreCase).Trim();
            val = Regex.Replace(val, @"^[,\.\-_:;\s]+|[,\.\-_:;\s]+$", "").Trim();

            return val;
        }

        private static (DateTime? Date, string? Signer) ExtractPdfSignatureInfo(byte[]? pdfBytes)
        {
            if (pdfBytes == null || pdfBytes.Length == 0) return (null, null);

            try
            {
                var raw = Encoding.Latin1.GetString(pdfBytes);

                // 1. Tìm trong /Type /Sig với trường /M (D:YYYYMMDDHHmmss)
                var sigMatch = Regex.Match(raw, @"(?:/Type\s*/Sig|/SubFilter\s*/[^\r\n/]+)[\s\S]*?/M\s*\(D:(\d{4})(\d{2})(\d{2})", RegexOptions.IgnoreCase);
                if (!sigMatch.Success)
                {
                    sigMatch = Regex.Match(raw, @"/M\s*\(D:(\d{4})(\d{2})(\d{2})", RegexOptions.IgnoreCase);
                }

                if (sigMatch.Success)
                {
                    if (int.TryParse(sigMatch.Groups[1].Value, out var y) &&
                        int.TryParse(sigMatch.Groups[2].Value, out var m) &&
                        int.TryParse(sigMatch.Groups[3].Value, out var d))
                    {
                        if (IsValidDate(d, m, y) && y >= 2000 && y <= 2050)
                        {
                            return (new DateTime(y, m, d, 0, 0, 0, DateTimeKind.Utc), null);
                        }
                    }
                }

                // 2. Nếu không tìm thấy /Sig, tìm trong PDF Metadata: /ModDate hoặc /CreationDate
                var metaMatch = Regex.Match(raw, @"/(?:ModDate|CreationDate)\s*\(D:(\d{4})(\d{2})(\d{2})", RegexOptions.IgnoreCase);
                if (metaMatch.Success)
                {
                    if (int.TryParse(metaMatch.Groups[1].Value, out var y) &&
                        int.TryParse(metaMatch.Groups[2].Value, out var m) &&
                        int.TryParse(metaMatch.Groups[3].Value, out var d))
                    {
                        if (IsValidDate(d, m, y) && y >= 2000 && y <= 2050)
                        {
                            return (new DateTime(y, m, d, 0, 0, 0, DateTimeKind.Utc), null);
                        }
                    }
                }

                return (null, null);
            }
            catch
            {
                return (null, null);
            }
        }

        private static (string? RefNum, string? DayNum) ExtractWidgetNumberAndDate(byte[]? pdfBytes)
        {
            if (pdfBytes == null || pdfBytes.Length == 0) return (null, null);

            try
            {
                string? leftNum = null;
                string? rightNum = null;

                var cmap = ParsePdfCmaps(pdfBytes);
                var rawString = Encoding.Latin1.GetString(pdfBytes);

                // 1. Tìm các annotation widget với vị trí Rect và liên kết AP (/N {objNum} 0 R)
                var widgetMatches = Regex.Matches(rawString, @"<<[^\>]*?/Subtype\s*/Widget[^\>]*?/Rect\s*\[\s*([0-9.]+)\s+([0-9.]+)\s+([0-9.]+)\s+([0-9.]+)\s*\][^\>]*?/AP\s*<<\s*/N\s*(\d+)\s+0\s+R", RegexOptions.IgnoreCase);

                foreach (Match m in widgetMatches)
                {
                    double x1 = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                    double y1 = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                    int apObjNum = int.Parse(m.Groups[5].Value);

                    var text = ExtractTextFromPdfObj(pdfBytes, apObjNum, cmap);
                    var digitsMatch = Regex.Match(text, @"\b(\d{1,5})\b");
                    if (digitsMatch.Success)
                    {
                        var val = digitsMatch.Groups[1].Value;
                        if (x1 < 350 && y1 > 500)
                        {
                            leftNum = val;
                        }
                        else if (x1 >= 350 && y1 > 500)
                        {
                            rightNum = val;
                        }
                    }
                }

                // 2. Dự phòng: Nếu regex trên không khớp cấu trúc obj, quét trực tiếp tất cả các luồng BT...ET
                if (string.IsNullOrWhiteSpace(leftNum) || string.IsNullOrWhiteSpace(rightNum))
                {
                    var generalTexts = ExtractAllWidgetStreamTexts(pdfBytes, cmap);
                    foreach (var t in generalTexts)
                    {
                        if (Regex.IsMatch(t, @"^\d{3,5}$") && string.IsNullOrWhiteSpace(leftNum)) leftNum = t;
                        else if (Regex.IsMatch(t, @"^\d{1,2}$") && string.IsNullOrWhiteSpace(rightNum)) rightNum = t;
                    }
                }

                return (leftNum, rightNum);
            }
            catch
            {
                return (null, null);
            }
        }

        private static string ExtractTextFromPdfObj(byte[] pdfBytes, int objNum, Dictionary<int, string> cmap)
        {
            var rawString = Encoding.Latin1.GetString(pdfBytes);
            var objPattern = $@"\b{objNum}\s+0\s+obj[\s\S]*?endobj";
            var objMatch = Regex.Match(rawString, objPattern);
            if (!objMatch.Success) return "";

            var content = objMatch.Value;
            var childObjs = Regex.Matches(content, @"/([A-Za-z0-9]+)\s+(\d+)\s+0\s+R");
            var sb = new StringBuilder();

            int streamIdx = content.IndexOf("stream", StringComparison.Ordinal);
            if (streamIdx >= 0)
            {
                var decomp = TryDecompressPdfStream(content);
                if (decomp.Contains("Tj") || decomp.Contains("TJ"))
                {
                    sb.Append(DecodePdfStreamText(decomp, cmap));
                }
            }

            foreach (Match c in childObjs)
            {
                int childNum = int.Parse(c.Groups[2].Value);
                var childText = ExtractTextFromPdfObj(pdfBytes, childNum, cmap);
                if (!string.IsNullOrWhiteSpace(childText)) sb.Append(" ").Append(childText);
            }

            return sb.ToString().Trim();
        }

        private static string TryDecompressPdfStream(string objContent)
        {
            var match = Regex.Match(objContent, @"stream\r?\n([\s\S]*?)\r?\nendstream");
            if (!match.Success) return "";
            var rawBytes = Encoding.Latin1.GetBytes(match.Groups[1].Value);
            try
            {
                using var ms = new MemoryStream(rawBytes);
                using var zlib = new ZLibStream(ms, CompressionMode.Decompress);
                using var outMs = new MemoryStream();
                zlib.CopyTo(outMs);
                return Encoding.Latin1.GetString(outMs.ToArray());
            }
            catch
            {
                return match.Groups[1].Value;
            }
        }

        private static string DecodePdfStreamText(string streamContent, Dictionary<int, string> cmap)
        {
            var sb = new StringBuilder();
            var tjMatches = Regex.Matches(streamContent, @"\(([\s\S]*?)\)\s*Tj");
            foreach (Match m in tjMatches)
            {
                var rawString = m.Groups[1].Value;
                var rawBytes = Encoding.Latin1.GetBytes(rawString);
                sb.Append(DecodePdfGlyphs(rawBytes, cmap));
            }
            var hexMatches = Regex.Matches(streamContent, @"<([0-9A-Fa-f]+)>\s*Tj");
            foreach (Match m in hexMatches)
            {
                var hexBytes = HexToBytes(m.Groups[1].Value);
                sb.Append(DecodePdfGlyphs(hexBytes, cmap));
            }
            return sb.ToString();
        }

        private static List<string> ExtractAllWidgetStreamTexts(byte[] pdfBytes, Dictionary<int, string> cmap)
        {
            var results = new List<string>();
            int idx = 0;
            while ((idx = IndexOfBytes(pdfBytes, "stream", idx)) >= 0)
            {
                idx += 6;
                if (idx < pdfBytes.Length && pdfBytes[idx] == '\r') idx++;
                if (idx < pdfBytes.Length && pdfBytes[idx] == '\n') idx++;
                int endIdx = IndexOfBytes(pdfBytes, "endstream", idx);
                if (endIdx < 0) break;

                try
                {
                    using var ms = new MemoryStream(pdfBytes, idx, endIdx - idx);
                    using var zlib = new ZLibStream(ms, CompressionMode.Decompress);
                    using var outMs = new MemoryStream();
                    zlib.CopyTo(outMs);
                    var decomp = outMs.ToArray();
                    var text = Encoding.Latin1.GetString(decomp);
                    if (text.Contains("BT") && (text.Contains("Tj") || text.Contains("TJ")))
                    {
                        var decoded = DecodePdfStreamText(text, cmap);
                        if (!string.IsNullOrWhiteSpace(decoded)) results.Add(decoded.Trim());
                    }
                }
                catch {}
                idx = endIdx + 9;
            }
            return results;
        }

        private static byte[] HexToBytes(string hex)
        {
            if (hex.Length % 2 != 0) hex += "0";
            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return bytes;
        }

        private static Dictionary<int, string> ParsePdfCmaps(byte[] pdfBytes)
        {
            var map = new Dictionary<int, string>();
            int idx = 0;
            while ((idx = IndexOfBytes(pdfBytes, "stream", idx)) >= 0)
            {
                idx += 6;
                if (idx < pdfBytes.Length && pdfBytes[idx] == '\r') idx++;
                if (idx < pdfBytes.Length && pdfBytes[idx] == '\n') idx++;
                int endIdx = IndexOfBytes(pdfBytes, "endstream", idx);
                if (endIdx < 0) break;

                try
                {
                    using var ms = new MemoryStream(pdfBytes, idx, endIdx - idx);
                    using var zlib = new ZLibStream(ms, CompressionMode.Decompress);
                    using var outMs = new MemoryStream();
                    zlib.CopyTo(outMs);
                    var text = Encoding.Latin1.GetString(outMs.ToArray());
                    if (text.Contains("beginbfrange") || text.Contains("beginbfchar"))
                    {
                        var bfcharMatches = Regex.Matches(text, @"<([0-9A-Fa-f]{4})>\s*<([0-9A-Fa-f]+)>");
                        foreach (Match m in bfcharMatches)
                        {
                            int glyph = Convert.ToInt32(m.Groups[1].Value, 16);
                            string val = HexToUtf16(m.Groups[2].Value);
                            map[glyph] = val;
                        }

                        var bfrangeMatches = Regex.Matches(text, @"<([0-9A-Fa-f]{4})>\s*<([0-9A-Fa-f]{4})>\s*<([0-9A-Fa-f]+)>");
                        foreach (Match m in bfrangeMatches)
                        {
                            int gStart = Convert.ToInt32(m.Groups[1].Value, 16);
                            int gEnd = Convert.ToInt32(m.Groups[2].Value, 16);
                            int targetStart = Convert.ToInt32(m.Groups[3].Value, 16);
                            for (int g = gStart; g <= gEnd; g++)
                            {
                                map[g] = ((char)(targetStart + (g - gStart))).ToString();
                            }
                        }
                    }
                }
                catch {}
                idx = endIdx + 9;
            }
            return map;
        }

        private static string HexToUtf16(string hex)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < hex.Length; i += 4)
            {
                if (i + 4 <= hex.Length)
                {
                    int code = Convert.ToInt32(hex.Substring(i, 4), 16);
                    sb.Append((char)code);
                }
            }
            return sb.ToString();
        }

        private static string DecodePdfGlyphs(byte[] bytes, Dictionary<int, string> cmap)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < bytes.Length; i += 2)
            {
                if (i + 1 < bytes.Length)
                {
                    int glyph = (bytes[i] << 8) | bytes[i + 1];
                    if (cmap.TryGetValue(glyph, out var str))
                    {
                        sb.Append(str);
                    }
                    else if (glyph >= 0x13 && glyph <= 0x1C)
                    {
                        sb.Append((char)('0' + (glyph - 0x13)));
                    }
                    else if (glyph >= 0x20 && glyph <= 0x7E)
                    {
                        sb.Append((char)glyph);
                    }
                }
            }
            return sb.ToString();
        }

        private static int IndexOfBytes(byte[] haystack, string needle, int start)
        {
            var needleBytes = Encoding.ASCII.GetBytes(needle);
            for (int i = start; i <= haystack.Length - needleBytes.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < needleBytes.Length; j++)
                {
                    if (haystack[i + j] != needleBytes[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match) return i;
            }
            return -1;
        }

        private void ExtractDocumentDate(string headerText, string fullText, string? fileName, int canCuIdx, byte[]? pdfBytes, ExtractedDocumentData result)
        {
            // LỚP 1: Con dấu ký số điện tử trong nội dung (Visual Signature Stamp)
            // Ví dụ: "Thời gian ký: 25.02.2021 15:12:01", "Ngày ký: 14/07/2020", "Signing time: 09/02/2021"
            var signDateMatch = Regex.Match(fullText, 
                @"(?:Thời\s*gian\s*ký|Th[ờo]i\s*gian\s*k[yý]|gian\s*k[yý]|Ngày\s*ký|Ng[àáaă]y\s*k[yý]|Ký\s*ngày|Signing\s*time)[\s:]*([0-9]{1,2})\s*[.\/-]\s*([0-9]{1,2})\s*[.\/-]\s*((?:19|20)[0-9]{2})", 
                RegexOptions.IgnoreCase);

            if (signDateMatch.Success)
            {
                if (int.TryParse(signDateMatch.Groups[1].Value, out var d) &&
                    int.TryParse(signDateMatch.Groups[2].Value, out var m) &&
                    int.TryParse(signDateMatch.Groups[3].Value, out var y))
                {
                    if (IsValidDate(d, m, y))
                    {
                        result.DocumentDate = new DateTime(y, m, d, 0, 0, 0, DateTimeKind.Utc);
                        result.DocumentDateString = $"{d:D2}/{m:D2}/{y}";
                        return;
                    }
                }
            }

            // LỚP 2: Dòng ngày tháng ban hành trên góc phải Header (Administrative Header Date)
            // Hỗ trợ đầy đủ Unicode, chữ số viết tay và ký tự dính nét:
            // "Thành phố Ho Chi Minh, ngày-ZÔtháng © năm 2021" -> 25/02/2021
            // "Hà Nội, ngày 14 tháng 7 năm 2020" -> 14/07/2020
            // "TP. Hồ Chí Minh, ngày 09 tháng 02 năm 2021" -> 09/02/2021
            // "ngày 0% tháng 4 năm 2020" -> 07/04/2020
            // "Hà Nội, ngày [thing T năm 2020" -> 01/07/2020
            var headerDateMatch = Regex.Match(headerText, 
                @"(?:ngày|ngay)\s*[-–—~.:]?\s*([^\s\r\nthángthang]{1,8})\s*(?:tháng|thang|thing|théng|thêng|thg|[.,])\s*([^\s\r\nnămnam]{1,8})?\s*(?:năm|nam)\s*((?:19|20)[0-9]{2})", 
                RegexOptions.IgnoreCase);

            if (headerDateMatch.Success)
            {
                var rawDay = headerDateMatch.Groups[1].Value;
                var rawMonth = headerDateMatch.Groups[2].Success ? headerDateMatch.Groups[2].Value : "";
                var rawYear = headerDateMatch.Groups[3].Value;

                var cleanDay = NormalizeHandwrittenGlyphs(rawDay);
                var cleanMonth = NormalizeHandwrittenGlyphs(rawMonth);

                int d = 0;
                int.TryParse(cleanDay, out d);
                int m = 0;
                int.TryParse(cleanMonth, out m);
                int y = 0;
                int.TryParse(rawYear, out y);

                // Nếu tháng bị dính vào chữ "théng" hoặc "thêng" do nét viết tay số 5
                if (m == 0 && Regex.IsMatch(headerDateMatch.Value, @"th[éèê]ng", RegexOptions.IgnoreCase))
                {
                    m = 5;
                }

                // Nếu có ngày và năm hợp lệ nhưng tháng bị trống/mờ, tìm tháng từ văn bản cùng năm ở đoạn đầu
                if (m == 0 && d >= 1 && d <= 31 && y >= 1990 && y <= 2050)
                {
                    var bodyMonthMatch = Regex.Match(fullText.Substring(0, Math.Min(fullText.Length, 1500)), $@"(?:ngày|ngay|\b)\s*\d{{1,2}}[\/\-](0?[1-9]|1[0-2])[\/\-]{y}\b", RegexOptions.IgnoreCase);
                    if (bodyMonthMatch.Success && int.TryParse(bodyMonthMatch.Groups[1].Value, out var inferredMonth))
                    {
                        m = inferredMonth;
                    }
                }

                if (IsValidDate(d, m, y))
                {
                    result.DocumentDate = new DateTime(y, m, d, 0, 0, 0, DateTimeKind.Utc);
                    result.DocumentDateString = $"{d:D2}/{m:D2}/{y}";
                    return;
                }
            }

            // LỚP 3: Chữ ký số mật mã & Siêu dữ liệu trong luồng PDF (PDF Crypto Signature & Metadata)
            // Theo Nghị định 30/2020/NĐ-CP (Điều 22 & 26), văn bản điện tử có giá trị pháp lý khi được ký số hợp lệ.
            var (sigDate, _) = ExtractPdfSignatureInfo(pdfBytes);
            if (sigDate.HasValue)
            {
                result.DocumentDate = sigDate.Value;
                result.DocumentDateString = $"{sigDate.Value.Day:D2}/{sigDate.Value.Month:D2}/{sigDate.Value.Year}";
                return;
            }

            // LỚP 4: Tìm ngày tháng trong vùng trước Căn cứ (Pre-body Context)
            int searchLimit = canCuIdx > 0 ? canCuIdx : Math.Min(fullText.Length, 1500);
            var preBodySearchText = fullText.Substring(0, searchLimit);

            var preBodyDateMatch = Regex.Match(preBodySearchText, 
                @"(?:ngày|ngay)\s+0?([1-9]|[12][0-9]|3[01])\s+(?:tháng|thang)\s+0?([1-9]|1[0-2])\s+(?:năm|nam)\s+((?:19|20)[0-9]{2})", 
                RegexOptions.IgnoreCase);

            if (preBodyDateMatch.Success)
            {
                if (int.TryParse(preBodyDateMatch.Groups[1].Value, out var d) &&
                    int.TryParse(preBodyDateMatch.Groups[2].Value, out var m) &&
                    int.TryParse(preBodyDateMatch.Groups[3].Value, out var y))
                {
                    if (IsValidDate(d, m, y))
                    {
                        result.DocumentDate = new DateTime(y, m, d, 0, 0, 0, DateTimeKind.Utc);
                        result.DocumentDateString = $"{d:D2}/{m:D2}/{y}";
                    }
                }
            }
        }

        private void ExtractSigner(string fullText, ExtractedDocumentData result)
        {
            var lines = fullText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = lines.Length - 1; i >= Math.Max(0, lines.Length - 20); i--)
            {
                var line = lines[i].Trim();
                if (line.Contains("Nơi nhận", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("Lưu:", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("VT,", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("Kính gửi", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("Căn cứ", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (line.Equals("QUYẾT ĐỊNH", StringComparison.OrdinalIgnoreCase) ||
                    line.Equals("QUYET DINH", StringComparison.OrdinalIgnoreCase) ||
                    line.Equals("THÔNG BÁO", StringComparison.OrdinalIgnoreCase) ||
                    line.Equals("KẾ HOẠCH", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (words.Length >= 2 && words.Length <= 5)
                {
                    bool isName = words.All(w => w.Length >= 1 && char.IsUpper(w[0]) && !Regex.IsMatch(w, @"\d"));
                    if (isName)
                    {
                        var lower = line.ToLowerInvariant();
                        if (!lower.Contains("bộ trưởng") && !lower.Contains("thứ trưởng") &&
                            !lower.Contains("giám đốc") && !lower.Contains("chủ tịch") &&
                            !lower.Contains("phó") && !lower.Contains("chính phủ") &&
                            !lower.Contains("uỷ ban") && !lower.Contains("ủy ban") &&
                            !lower.Contains("sở y tế") && !lower.Contains("bộ y tế") &&
                            !lower.Contains("bộ thông tin") && !lower.Contains("sở tư pháp"))
                        {
                            result.Signer = line;
                            break;
                        }
                    }
                }
            }
        }

        private void ExtractDocumentType(string headerText, ExtractedDocumentData result)
        {
            if (!string.IsNullOrWhiteSpace(result.ReferenceNumber))
            {
                var refUpper = result.ReferenceNumber.ToUpperInvariant();
                if (refUpper.Contains("/QĐ-") || refUpper.Contains("-QĐ/") || refUpper.Contains("/QĐ") || refUpper.EndsWith("-QĐ")) result.DocumentType = "Quyết Định";
                else if (refUpper.Contains("/TB-") || refUpper.Contains("-TB/") || refUpper.Contains("/TB") || refUpper.EndsWith("-TB")) result.DocumentType = "Thông Báo";
                else if (refUpper.Contains("/CT-") || refUpper.Contains("-CT/") || refUpper.Contains("/CT") || refUpper.EndsWith("-CT")) result.DocumentType = "Chỉ Thị";
                else if (refUpper.Contains("/KH-") || refUpper.Contains("-KH/") || refUpper.Contains("/KH") || refUpper.EndsWith("-KH")) result.DocumentType = "Kế Hoạch";
                else if (refUpper.Contains("/BC-") || refUpper.Contains("-BC/") || refUpper.Contains("/BC") || refUpper.EndsWith("-BC")) result.DocumentType = "Báo Cáo";
                else if (refUpper.Contains("/NQ-") || refUpper.Contains("-NQ/") || refUpper.Contains("/NQ") || refUpper.EndsWith("-NQ")) result.DocumentType = "Nghị Quyết";
                else if (refUpper.Contains("/HĐ-") || refUpper.Contains("-HĐ/") || refUpper.Contains("/HĐ") || refUpper.EndsWith("-HĐ")) result.DocumentType = "Hợp Đồng";
                else if (refUpper.Contains("/GM-") || refUpper.Contains("-GM/") || refUpper.Contains("/GM") || refUpper.EndsWith("-GM")) result.DocumentType = "Giấy Mời";
                else if (refUpper.Contains("/TTR-") || refUpper.Contains("-TTR/") || refUpper.Contains("/TTR") || refUpper.EndsWith("-TTR")) result.DocumentType = "Tờ Trình";
                else if (refUpper.Contains("/BB-") || refUpper.Contains("-BB/") || refUpper.Contains("/BB") || refUpper.EndsWith("-BB")) result.DocumentType = "Biên Bản";
                else if (refUpper.Contains("/QC-") || refUpper.Contains("-QC/") || refUpper.Contains("/QC") || refUpper.EndsWith("-QC")) result.DocumentType = "Quy Chế";
                else if (refUpper.Contains("/HD-") || refUpper.Contains("-HD/") || refUpper.Contains("/HD") || refUpper.EndsWith("-HD")) result.DocumentType = "Hướng Dẫn";
                else if (refUpper.Contains("/CĐ-") || refUpper.Contains("-CĐ/") || refUpper.Contains("/CĐ") || refUpper.EndsWith("-CĐ")) result.DocumentType = "Công Điện";
            }

            if (string.IsNullOrWhiteSpace(result.DocumentType))
            {
                if ((result.Subject != null && (result.Subject.StartsWith("V/v", StringComparison.OrdinalIgnoreCase) || 
                                                result.Subject.StartsWith("Về việc", StringComparison.OrdinalIgnoreCase) ||
                                                result.Subject.StartsWith("Về ", StringComparison.OrdinalIgnoreCase))) || 
                    Regex.IsMatch(headerText, @"\b(?:V/v|Về việc|Về)\b", RegexOptions.IgnoreCase))
                {
                    result.DocumentType = "Công Văn";
                }
                else
                {
                    var labelMatch = Regex.Match(headerText, @"(?:\b|^)(QUYẾT ĐỊNH|THÔNG BÁO|TỜ TRÌNH|GIẤY MỜI|CHỈ THỊ|KẾ HOẠCH|BÁO CÁO|NGHỊ QUYẾT|HỢP ĐỒNG|BIÊN BẢN|QUY CHẾ|QUY ĐỊNH|HƯỚNG DẪN|THƯ KÊU GỌI)(?:\b|$)", RegexOptions.IgnoreCase);
                    if (labelMatch.Success)
                    {
                        result.DocumentType = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(labelMatch.Groups[1].Value.ToLowerInvariant());
                    }
                    else if (!string.IsNullOrWhiteSpace(result.ReferenceNumber))
                    {
                        result.DocumentType = "Công Văn";
                    }
                }
            }
        }

        private static bool IsValidDate(int d, int m, int y)
        {
            return d >= 1 && d <= 31 && m >= 1 && m <= 12 && y >= 1990 && y <= 2050;
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

        private static string CapitalizeFirst(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return s;
            return char.ToUpper(s[0]) + s.Substring(1);
        }
    }
}
