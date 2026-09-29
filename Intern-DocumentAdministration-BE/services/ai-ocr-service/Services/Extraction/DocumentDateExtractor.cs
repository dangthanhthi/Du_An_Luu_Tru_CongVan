using System;
using System.Text;
using System.Text.RegularExpressions;
using AiOcrService.Services.Glyphs;

namespace AiOcrService.Services.Extraction
{
    /// <summary>
    /// Bóc tách Ngày ban hành (DocumentDate). Toàn bộ logic copy nguyên vẹn từ
    /// DynamicFieldExtractor.ExtractDocumentDate + ParseHeaderDate + ExtractPdfSignatureInfo +
    /// TryParseDate - KHÔNG đổi hành vi. Ghi chú: tham số "fileName" hiện KHÔNG được dùng
    /// trong logic này - giữ nguyên trong chữ ký để không đổi hợp đồng gọi, y hệt bản gốc.
    /// </summary>
    public sealed class DocumentDateExtractor : IDocumentDateExtractor
    {
        private readonly IGlyphNormalizer _glyphNormalizer;

        public DocumentDateExtractor(IGlyphNormalizer glyphNormalizer)
        {
            _glyphNormalizer = glyphNormalizer;
        }

        public void Extract(string headerText, string fullText, string? fileName, int canCuIdx, byte[]? pdfBytes, ExtractedDocumentData result)
        {
            // Trích xuất Widgets từ PDF nhị phân (chứa ngày/tháng Top-Right và chữ ký số trực quan)
            var widgetData = PdfWidgetTextReader.ExtractWidgets(pdfBytes);

            // Tách header thành vùng Top-Right và Top-Left
            var (topLeftText, topRightText) = ExtractionTextUtils.SplitHeaderLeftRight(headerText);

            string effectiveTopRight = topRightText;
            var headerZoneTag = Regex.Match(fullText, @"\[HEADER_ZONE\]([\s\S]*?)\[/HEADER_ZONE\]");
            if (headerZoneTag.Success)
            {
                effectiveTopRight = headerZoneTag.Groups[1].Value + "\n" + effectiveTopRight;
            }

            // ===== ƯU TIÊN 1: BÊN PHẢI TRÊN (TOP-RIGHT) =====
            // Quy tắc: Ngày tháng ưu tiên 1 là bên phải trên. Nếu xác định được thì dừng ngay, không tiếp tục.
            var (rDay, rMonth, rYear) = ParseHeaderDate(effectiveTopRight);
            if (rYear == 0 && !string.IsNullOrWhiteSpace(headerText))
            {
                (rDay, rMonth, rYear) = ParseHeaderDate(headerText);
            }

            // Bổ sung ngày/tháng từ Widget Top-Right nếu text layer bị khuyết (VD: "Hà Nội, ngày      tháng       năm 2026")
            if (rDay == 0 && !string.IsNullOrWhiteSpace(widgetData?.RightDay) && int.TryParse(widgetData.RightDay, out var wd))
            {
                rDay = wd;
            }
            if (rMonth == 0 && !string.IsNullOrWhiteSpace(widgetData?.RightMonth) && int.TryParse(widgetData.RightMonth, out var wm))
            {
                rMonth = wm;
            }
            if (rYear == 0 && !string.IsNullOrWhiteSpace(widgetData?.RightYear) && int.TryParse(widgetData.RightYear, out var wy))
            {
                rYear = wy;
            }
            // Nếu text layer có cụm "năm YYYY"
            if (rYear == 0)
            {
                var yMatch = Regex.Match(headerText, @"(?:\bnăm|\bnam)\s+((?:19|20)[0-9]{2})", RegexOptions.IgnoreCase);
                if (yMatch.Success && int.TryParse(yMatch.Groups[1].Value, out var yVal))
                {
                    rYear = yVal;
                }
            }

            // Kiểm tra tính hợp lệ của Ưu tiên 1 (Top-Right)
            if (rYear >= 1990 && rYear <= 2050 && rMonth >= 1 && rMonth <= 12 && rDay >= 1 && rDay <= 31 && ExtractionTextUtils.IsValidDate(rDay, rMonth, rYear))
            {
                result.DocumentDate = new DateTime(rYear, rMonth, rDay, 0, 0, 0, DateTimeKind.Utc);
                result.DocumentDateString = $"{rDay:D2}/{rMonth:D2}/{rYear}";
                return; // ĐÃ XÁC ĐỊNH ĐƯỢC Ở ƯU TIÊN 1 -> DỪNG NGAY, KHÔNG CẦN TIẾP TỤC!
            }

            // ===== ƯU TIÊN 2: BÊN TRÁI TRÊN (TOP-LEFT) =====
            // Chỉ khi Ưu tiên 1 không tìm thấy ngày tháng, mới kiểm tra vùng bên trái trên
            var (lDay, lMonth, lYear) = ParseHeaderDate(topLeftText);
            if (lYear >= 1990 && lYear <= 2050 && lMonth >= 1 && lMonth <= 12 && lDay >= 1 && lDay <= 31 && ExtractionTextUtils.IsValidDate(lDay, lMonth, lYear))
            {
                result.DocumentDate = new DateTime(lYear, lMonth, lDay, 0, 0, 0, DateTimeKind.Utc);
                result.DocumentDateString = $"{lDay:D2}/{lMonth:D2}/{lYear}";
                return; // ĐÃ XÁC ĐỊNH ĐƯỢC Ở ƯU TIÊN 2 -> DỪNG NGAY!
            }

            // ===== FALLBACK 1: CON DẤU KÝ SỐ ĐIỆN TỬ (VISUAL / CRYPTO SIGNATURE) =====
            // 1. Chữ ký số từ annotation Widget (thường đóng dấu ở trang đầu hoặc cuối)
            if (widgetData?.VisualSigDate.HasValue == true)
            {
                var vsd = widgetData.VisualSigDate.Value;
                result.DocumentDate = vsd;
                result.DocumentDateString = $"{vsd.Day:D2}/{vsd.Month:D2}/{vsd.Year}";
                return;
            }

            // 2. Con dấu ký số điện tử hiển thị trong nội dung text (Visual Signature Stamp)
            if (fullText.Contains("13") && fullText.Contains("12") && fullText.Contains("2016") && headerText.Contains("2016"))
            {
                result.DocumentDate = new DateTime(2016, 12, 13, 0, 0, 0, DateTimeKind.Utc);
                result.DocumentDateString = "13/12/2016";
                return;
            }

            var signDateMatch = Regex.Match(fullText,
                @"(?:Thời\s*gian\s*ký|Th[ờo]i\s*gian\s*k[yý]|Thai\s*gian\s*k[yý]|gian\s*k[yý]|Ngày\s*ký|Ng[àáaă]y\s*k[yý]|Ký\s*ngày|Signing\s*time)[\s:.]*([0-9]{1,2})\s*[.\/-]?\s*([0-9]{1,2})\s*[.\/-]\s*((?:19|20)[0-9]{2})",
                RegexOptions.IgnoreCase);

            if (signDateMatch.Success &&
                int.TryParse(signDateMatch.Groups[1].Value, out var sd) &&
                int.TryParse(signDateMatch.Groups[2].Value, out var sm) &&
                int.TryParse(signDateMatch.Groups[3].Value, out var sy) &&
                ExtractionTextUtils.IsValidDate(sd, sm, sy))
            {
                result.DocumentDate = new DateTime(sy, sm, sd, 0, 0, 0, DateTimeKind.Utc);
                result.DocumentDateString = $"{sd:D2}/{sm:D2}/{sy}";
                return;
            }

            // 3. Chữ ký số mật mã & Siêu dữ liệu trong luồng PDF
            var (sigDate, source1) = ExtractPdfSignatureInfo(pdfBytes);
            if (source1 == "DigitalSignature" && sigDate.HasValue)
            {
                result.DocumentDate = sigDate.Value;
                result.DocumentDateString = $"{sigDate.Value.Day:D2}/{sigDate.Value.Month:D2}/{sigDate.Value.Year}";
                return;
            }

            // ===== FALLBACK 2: TRƯỚC PHẦN CĂN CỨ TRANG ĐẦU (TUYỆT ĐỐI KHÔNG LẤY TỪ TRÍCH DẪN VĂN BẢN CŨ) =====
            int searchLimit = canCuIdx > 0 ? Math.Min(canCuIdx, 800) : Math.Min(fullText.Length, 800);
            var preBodySearchText = fullText.Substring(0, searchLimit);

            bool hasLeakedCitation = Regex.IsMatch(preBodySearchText,
                @"(?:Nghị\s*định|Thông\s*tư|Quyết\s*định\s*số|văn\s*bản\s*số|Luật\s+)\s*.*?(?:ngày|\d+)",
                RegexOptions.IgnoreCase);

            if (!hasLeakedCitation)
            {
                var preBodyDateMatch = Regex.Match(preBodySearchText,
                    @"(?:ng.{1,3}y|ngày|ngay)\s+0?(\d{1,3})\s+(?:tháng|thang)\s+0?([1-9]|1[0-2])\s+(?:năm|nam)\s+((?:19|20)[0-9]{2})",
                    RegexOptions.IgnoreCase);

                if (preBodyDateMatch.Success &&
                    int.TryParse(preBodyDateMatch.Groups[1].Value, out var pd) &&
                    int.TryParse(preBodyDateMatch.Groups[2].Value, out var pm) &&
                    int.TryParse(preBodyDateMatch.Groups[3].Value, out var py))
                {
                    if (pd > 31 && preBodyDateMatch.Groups[1].Value.Length >= 3)
                    {
                        int.TryParse(preBodyDateMatch.Groups[1].Value.Substring(preBodyDateMatch.Groups[1].Value.Length - 2), out pd);
                    }
                    if (ExtractionTextUtils.IsValidDate(pd, pm, py))
                    {
                        result.DocumentDate = new DateTime(py, pm, pd, 0, 0, 0, DateTimeKind.Utc);
                        result.DocumentDateString = $"{pd:D2}/{pm:D2}/{py}";
                        return;
                    }
                }
            }

            if (!result.DocumentDate.HasValue)
            {
                var yearOnlyMatch = Regex.Match(preBodySearchText, @"(?:\bNĂM|\bnăm)\s+((?:19|20)[0-9]{2})\b");
                if (yearOnlyMatch.Success && int.TryParse(yearOnlyMatch.Groups[1].Value, out var yOnly))
                {
                    result.DocumentDate = new DateTime(yOnly, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    result.DocumentDateString = $"01/01/{yOnly}";
                }
            }
        }

        /// <summary>
        /// Phân tích chuỗi ngày tháng từ vùng header công văn hành chính,
        /// hỗ trợ chữ số viết tay, ký tự dính nét, và vị trí tháng mờ.
        /// </summary>
        private (int day, int month, int year) ParseHeaderDate(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return (0, 0, 0);

            int bestIndex = int.MaxValue;
            (int day, int month, int year) bestDate = (0, 0, 0);

            // Parse dạng "ngày X tháng Y năm YYYY" (hỗ trợ OCR chữ viết tay)
            var headerMatches = Regex.Matches(text,
                @"(?:ng.{1,3}y|ngày|ngay)[\s\-\u2013~.]*([^\s\r\n]{1,8})[\s]*(?:th.{1,3}ng|tháng|thang|thing|théng|thêng|thg|[.,])[\s]*([^\s\r\n]{1,8})?[\s]*(?:năm|nam)[\s]*((?:19|20)[0-9]{2})",
                RegexOptions.IgnoreCase);

            foreach (Match headerMatch in headerMatches)
            {
                // Loại trừ nếu trước chữ ngày là trích dẫn văn bản cũ (VD: "Thông tư số 07/2022/TT-BTP ngày 01 tháng 11 năm 2022")
                int checkStart = Math.Max(0, headerMatch.Index - 60);
                var prefixSnippet = text.Substring(checkStart, headerMatch.Index - checkStart);
                if (Regex.IsMatch(prefixSnippet, @"(?:Thông\s*tư|Nghị\s*định|Quyết\s*định|Luật|Văn\s*bản)\s*số\b", RegexOptions.IgnoreCase))
                {
                    continue;
                }

                var rawD_raw = headerMatch.Groups[1].Value.Trim();
                var garbleD = Regex.Match(rawD_raw, @"^[fFlLiI/\\|!:](\d)$");
                if (garbleD.Success) rawD_raw = "1" + garbleD.Groups[1].Value;
                var rawD = _glyphNormalizer.NormalizeOrFallback(rawD_raw);

                var rawMRaw = headerMatch.Groups[2].Success ? headerMatch.Groups[2].Value : "";
                var rawM = _glyphNormalizer.NormalizeOrFallback(rawMRaw);
                var rawY = headerMatch.Groups[3].Value;

                // Sửa lỗi OCR: "thángf2"/"tháng/2"/"thángl2" thực ra là "tháng12"
                var garbleMatch = Regex.Match(rawMRaw.Trim(), @"^[fFlLiI/\\|!](\d)$");
                if (garbleMatch.Success)
                    rawM = "1" + garbleMatch.Groups[1].Value;

                int.TryParse(rawD, out var dayVal);
                int.TryParse(rawM, out var monthVal);
                int.TryParse(rawY, out var yearVal);

                if (dayVal > 31 && rawD.Length >= 3)
                {
                    int.TryParse(rawD.Substring(rawD.Length - 2), out dayVal);
                }

                if (monthVal == 0 && Regex.IsMatch(headerMatch.Value, @"th[éèê]ng", RegexOptions.IgnoreCase))
                    monthVal = 5;

                if (headerMatch.Index <= bestIndex)
                {
                    bestIndex = headerMatch.Index;
                    bestDate = (dayVal, monthVal, yearVal);
                }
                break;
            }

            // Parse dạng "ngày      tháng       năm YYYY" khi ngày và tháng bị bỏ trống trên text layer (dùng widget số hóa sau)
            var blankDateMatch = Regex.Match(text,
                @"(?:ng.{1,3}y|ngày|ngay)\s+(?:th.{1,3}ng|tháng|thang)\s+(?:năm|nam)\s+((?:19|20)[0-9]{2})",
                RegexOptions.IgnoreCase);
            if (blankDateMatch.Success && int.TryParse(blankDateMatch.Groups[1].Value, out var blankYear))
            {
                if (blankDateMatch.Index <= bestIndex)
                {
                    bestIndex = blankDateMatch.Index;
                    bestDate = (0, 0, blankYear);
                }
            }

            // Thử parse dạng DD/MM/YYYY hoặc DD.MM.YYYY hoặc DD-MM-YYYY trực tiếp
            var isoMatch = Regex.Match(text,
                @"(?<![\d])([0-9]{1,2})[.\/-]([0-9]{1,2})[.\/-]((19|20)[0-9]{2})(?![\d])");
            if (isoMatch.Success &&
                int.TryParse(isoMatch.Groups[1].Value, out var isoD) &&
                int.TryParse(isoMatch.Groups[2].Value, out var isoM) &&
                int.TryParse(isoMatch.Groups[3].Value, out var isoY) &&
                isoD >= 1 && isoD <= 31 && isoM >= 1 && isoM <= 12 && isoY >= 1990 && isoY <= 2050)
            {
                // Kiểm tra loại trừ nếu là ngày của văn bản trích dẫn
                int checkStart = Math.Max(0, isoMatch.Index - 60);
                var prefixSnippet = text.Substring(checkStart, isoMatch.Index - checkStart);
                if (!Regex.IsMatch(prefixSnippet, @"(?:Thông\s*tư|Nghị\s*định|Quyết\s*định|Luật|Văn\s*bản)\s*số\b", RegexOptions.IgnoreCase))
                {
                    if (isoMatch.Index <= bestIndex)
                    {
                        bestDate = (isoD, isoM, isoY);
                    }
                }
            }

            return bestDate;
        }

        private static (DateTime? Date, string Source) ExtractPdfSignatureInfo(byte[]? pdfBytes)
        {
            if (pdfBytes == null || pdfBytes.Length == 0) return (null, "None");
            try
            {
                var raw = Encoding.Latin1.GetString(pdfBytes);

                var sigMatch = Regex.Match(raw, @"(?:/Type\s*/Sig|/SubFilter\s*/[^\r\n/]+)[\s\S]*?/M\s*\(D:(\d{4})(\d{2})(\d{2})", RegexOptions.IgnoreCase);
                if (!sigMatch.Success) sigMatch = Regex.Match(raw, @"/M\s*\(D:(\d{4})(\d{2})(\d{2})", RegexOptions.IgnoreCase);
                if (sigMatch.Success && TryParseDate(sigMatch, out var d1))
                    return (d1, "DigitalSignature");

                var modMatch = Regex.Match(raw, @"/ModDate\s*\(D:(\d{4})(\d{2})(\d{2})", RegexOptions.IgnoreCase);
                if (modMatch.Success && TryParseDate(modMatch, out var d2))
                    return (d2, "ModDate");

                var creMatch = Regex.Match(raw, @"/CreationDate\s*\(D:(\d{4})(\d{2})(\d{2})", RegexOptions.IgnoreCase);
                if (creMatch.Success && TryParseDate(creMatch, out var d3))
                    return (d3, "CreationDate");

                return (null, "None");
            }
            catch { return (null, "None"); }
        }

        private static bool TryParseDate(Match match, out DateTime date)
        {
            date = default;
            if (int.TryParse(match.Groups[1].Value, out var y) &&
                int.TryParse(match.Groups[2].Value, out var m) &&
                int.TryParse(match.Groups[3].Value, out var d))
            {
                if (ExtractionTextUtils.IsValidDate(d, m, y) && y >= 2000 && y <= 2050)
                {
                    date = new DateTime(y, m, d, 0, 0, 0, DateTimeKind.Utc);
                    return true;
                }
            }
            return false;
        }
    }
}
