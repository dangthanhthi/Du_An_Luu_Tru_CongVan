using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace AiOcrService.Services.Extraction
{
    /// <summary>
    /// Đọc trực tiếp cấu trúc byte thô của file PDF (annotation Widget, AP stream, CMap...)
    /// để lấy nội dung các ô chữ ký số (Số ký hiệu / Ngày) khi không lấy được từ layer text
    /// đã OCR/parse thông thường. Đây là 1 khối trách nhiệm HOÀN TOÀN khác với việc parse
    /// regex trên text thường (đọc byte nhị phân PDF, không liên quan ngữ nghĩa hành chính) -
    /// tách riêng khỏi ReferenceNumberExtractor để class đó chỉ còn lo phần regex/text.
    /// Toàn bộ logic copy nguyên vẹn từ DynamicFieldExtractor gốc, KHÔNG đổi hành vi.
    /// </summary>
    public class PdfWidgetData
    {
        public string? LeftRefNum { get; set; }
        public string? RightDay { get; set; }
        public string? RightMonth { get; set; }
        public string? RightYear { get; set; }
        public DateTime? VisualSigDate { get; set; }
        public string? VisualSigner { get; set; }
        public string? VisualAgency { get; set; }
    }

    internal static class PdfWidgetTextReader
    {
        /// <summary>
        /// Bóc tách toàn bộ thông tin annotation Widget từ byte PDF (Số ký hiệu Top-Left, Ngày/Tháng Top-Right, Chữ ký số trực quan).
        /// </summary>
        public static PdfWidgetData ExtractWidgets(byte[]? pdfBytes)
        {
            var data = new PdfWidgetData();
            if (pdfBytes == null || pdfBytes.Length == 0) return data;

            try
            {
                var cmap = ParsePdfCmaps(pdfBytes);
                var rawString = Encoding.Latin1.GetString(pdfBytes);

                // Quét từng object trong file PDF để tìm các object Widget/Sig
                var objPattern = @"\b(\d+)\s+0\s+obj([\s\S]*?)endobj";
                var objMatches = Regex.Matches(rawString, objPattern);

                foreach (Match objMatch in objMatches)
                {
                    var content = objMatch.Groups[2].Value;
                    if (!content.Contains("/Widget") && !content.Contains("/Sig")) continue;

                    var rectMatch = Regex.Match(content, @"/Rect\s*\[\s*([0-9.]+)\s+([0-9.]+)\s+([0-9.]+)\s+([0-9.]+)\s*\]");
                    if (!rectMatch.Success) continue;

                    var apMatch = Regex.Match(content, @"/AP\s*<<[^\>]*?/N\s+(\d+)\s+0\s+R", RegexOptions.IgnoreCase);
                    if (!apMatch.Success) continue;

                    double x1 = double.Parse(rectMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                    double y1 = double.Parse(rectMatch.Groups[2].Value, CultureInfo.InvariantCulture);
                    int apObjNum = int.Parse(apMatch.Groups[1].Value);

                    var text = ExtractTextFromPdfObj(pdfBytes, apObjNum, cmap).Trim();
                    if (string.IsNullOrWhiteSpace(text)) continue;

                    // Kiểm tra nếu là con dấu ký số điện tử (chứa "Thời gian ký" hoặc "Người ký")
                    var sigTimeMatch = Regex.Match(text, @"(?:Thời\s*gian\s*ký|Ngày\s*ký)[\s:.]*([0-9]{1,2})[.\/-]([0-9]{1,2})[.\/-]((?:19|20)[0-9]{2})", RegexOptions.IgnoreCase);
                    if (sigTimeMatch.Success)
                    {
                        if (int.TryParse(sigTimeMatch.Groups[1].Value, out var sd) &&
                            int.TryParse(sigTimeMatch.Groups[2].Value, out var sm) &&
                            int.TryParse(sigTimeMatch.Groups[3].Value, out var sy) &&
                            ExtractionTextUtils.IsValidDate(sd, sm, sy))
                        {
                            data.VisualSigDate = new DateTime(sy, sm, sd, 0, 0, 0, DateTimeKind.Utc);
                        }
                    }

                    // Vùng Header: Tọa độ Y > 500 (nửa trên của trang A4 chuẩn 595x842)
                    if (y1 > 500)
                    {
                        var textTrim = text.Trim();
                        // Nếu là số thuần túy (VD: "05", "28", "8", "2026")
                        if (Regex.IsMatch(textTrim, @"^\d{1,5}$"))
                        {
                            var val = textTrim;
                            // Nửa bên trái: Top-Left (x < 350) -> Số ký hiệu
                            if (x1 < 350)
                            {
                                data.LeftRefNum ??= val;
                            }
                            // Nửa bên phải: Top-Right (x >= 350) -> Ngày / Tháng
                            else
                            {
                                if (x1 < 420 && int.TryParse(val, out var d) && d >= 1 && d <= 31)
                                {
                                    data.RightDay ??= val;
                                }
                                else if (x1 >= 420 && int.TryParse(val, out var m) && m >= 1 && m <= 12)
                                {
                                    data.RightMonth ??= val;
                                }
                                else if (val.Length == 4 && (val.StartsWith("19") || val.StartsWith("20")))
                                {
                                    data.RightYear ??= val;
                                }
                            }
                        }
                    }
                }

                // Dự phòng quét luồng nếu regex obj không lấy được
                if (string.IsNullOrWhiteSpace(data.LeftRefNum) || string.IsNullOrWhiteSpace(data.RightDay))
                {
                    var generalTexts = ExtractAllWidgetStreamTexts(pdfBytes, cmap);
                    foreach (var t in generalTexts)
                    {
                        if (Regex.IsMatch(t, @"^\d{3,5}$") && string.IsNullOrWhiteSpace(data.LeftRefNum)) data.LeftRefNum = t;
                        else if (Regex.IsMatch(t, @"^\d{1,2}$") && string.IsNullOrWhiteSpace(data.RightDay)) data.RightDay = t;
                    }
                }

                // Bổ sung: Quét timestamp chữ ký số chuẩn PDF (/M(D:YYYYMMDD...) hoặc /ModDate(D:YYYYMMDD...))
                if (!data.VisualSigDate.HasValue)
                {
                    var locSigMatch = Regex.Match(rawString, @"Location[^\)]*?\/M\s*\(\s*D\s*:\s*(20\d{2})(0[1-9]|1[0-2])([0-3]\d)", RegexOptions.IgnoreCase);
                    if (locSigMatch.Success &&
                        int.TryParse(locSigMatch.Groups[1].Value, out var ly) &&
                        int.TryParse(locSigMatch.Groups[2].Value, out var lm) &&
                        int.TryParse(locSigMatch.Groups[3].Value, out var ld) &&
                        ExtractionTextUtils.IsValidDate(ld, lm, ly))
                    {
                        data.VisualSigDate = new DateTime(ly, lm, ld, 0, 0, 0, DateTimeKind.Utc);
                    }
                    else
                    {
                        var sigMatches = Regex.Matches(rawString, @"\/M\s*\(\s*D\s*:\s*(20\d{2})(0[1-9]|1[0-2])([0-3]\d)");
                        if (sigMatches.Count > 0)
                        {
                            var dateCounts = new Dictionary<string, (int Count, int Y, int M, int D)>();
                            foreach (Match sm in sigMatches)
                            {
                                if (int.TryParse(sm.Groups[1].Value, out var sy) &&
                                    int.TryParse(sm.Groups[2].Value, out var smonth) &&
                                    int.TryParse(sm.Groups[3].Value, out var sday) &&
                                    ExtractionTextUtils.IsValidDate(sday, smonth, sy))
                                {
                                    var key = $"{sday:D2}/{smonth:D2}/{sy}";
                                    if (!dateCounts.ContainsKey(key))
                                        dateCounts[key] = (0, sy, smonth, sday);
                                    dateCounts[key] = (dateCounts[key].Count + 1, sy, smonth, sday);
                                }
                            }
                            if (dateCounts.Count > 0)
                            {
                                var mostFrequent = dateCounts.Values.OrderByDescending(v => v.Count).First();
                                data.VisualSigDate = new DateTime(mostFrequent.Y, mostFrequent.M, mostFrequent.D, 0, 0, 0, DateTimeKind.Utc);
                            }
                        }
                    }
                }

                return data;
            }
            catch
            {
                return data;
            }
        }

        /// <summary>Tương thích ngược với lời gọi cũ.</summary>
        public static (string? RefNum, string? DayNum) ExtractWidgetNumberAndDate(byte[]? pdfBytes)
        {
            var data = ExtractWidgets(pdfBytes);
            return (data.LeftRefNum, data.RightDay);
        }

        private static string ExtractTextFromPdfObj(byte[] pdfBytes, int objNum, Dictionary<int, string> cmap, HashSet<int>? visited = null)
        {
            visited ??= new HashSet<int>();
            if (!visited.Add(objNum)) return "";

            var rawString = Encoding.Latin1.GetString(pdfBytes);
            var objPattern = $@"\b{objNum}\s+0\s+obj([\s\S]*?)endobj";
            var objMatch = Regex.Match(rawString, objPattern);
            if (!objMatch.Success) return "";

            var content = objMatch.Groups[1].Value;
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

            var childObjs = Regex.Matches(content, @"/([A-Za-z0-9]+)\s+(\d+)\s+0\s+R");
            foreach (Match c in childObjs)
            {
                int childNum = int.Parse(c.Groups[2].Value);
                var childText = ExtractTextFromPdfObj(pdfBytes, childNum, cmap, visited);
                if (!string.IsNullOrWhiteSpace(childText))
                {
                    if (sb.Length > 0) sb.Append(" ");
                    sb.Append(childText);
                }
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
                catch { }
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
                catch { }
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
    }
}
