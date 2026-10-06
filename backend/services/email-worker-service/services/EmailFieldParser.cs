using MimeKit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace EmailWorkerService.Services;

/// <summary>
/// Phân tích động các trường công văn (số hiệu, ngày ban hành, trích yếu)
/// từ email thông báo doanh nghiệp với vị trí không cố định.
/// Sử dụng cơ chế best-match scoring thay vì thứ tự cứng.
/// </summary>
public class EmailFieldParser
{
    // ─── Patterns số hiệu công văn ───
    private static readonly Regex[] RefNumPatterns =
    [
        // Dạng chuẩn: 1234/SYT-NVY, 56/QĐ-UBND
        new(@"(?<![\d/])(?<refnum>\d{1,5}\s*/\s*[A-ZĐ][A-ZĐa-z0-9\-]{1,25}(?:\s*/\s*[A-ZĐa-z0-9\-]{1,20})*)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase),
        // Dạng trong ngoặc: [Số: 456/TB-VP] hoặc (456/TB-VP)
        new(@"[\[\(]\s*(?:Số|So|Ref|No)[.:]?\s*(?<refnum>\d{1,5}\s*/\s*[A-ZĐa-z0-9\-]+)\s*[\]\)]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase),
        // Dạng dấu gạch (lỗi OCR): 1234-SYT-NVY
        new(@"(?<![\d\-])(?<refnum>\d{1,5}[-][A-ZĐ]{2,}[-][A-ZĐa-z0-9]{2,})",
            RegexOptions.Compiled | RegexOptions.IgnoreCase),
    ];

    private static readonly string[] RefNumBoostKeywords =
        ["số:", "so:", "số hiệu", "so hieu", "ký hiệu", "ky hieu",
         "ref:", "reference", "số công văn", "so cong van",
         "số quyết định", "số thông báo", "mã số văn bản"];

    // ─── Patterns ngày tháng ───
    private static readonly Regex[] DatePatterns =
    [
        // Tiếng Việt đầy đủ: ngày 15 tháng 09 năm 2026
        new(@"(?:ngày|ngay)\s*(\d{1,2})\s*(?:tháng|thang|thg)\s*(\d{1,2})\s*(?:năm|nam)\s*((19|20)\d{2})",
            RegexOptions.Compiled | RegexOptions.IgnoreCase),
        // Số ngắn: 15/09/2026 hoặc 15-09-2026 hoặc 15.09.2026
        new(@"(?<![\d/\-\.])(?<day>\d{1,2})[/\-\.](?<month>0?[1-9]|1[0-2])[/\-\.](?<year>(19|20)\d{2})(?![\d])",
            RegexOptions.Compiled),
    ];

    private static readonly string[] DateBoostKeywords =
        ["ngày", "ngay", "date", "ngày ban hành", "ban hành ngày",
         "ngày phát hành", "ngày ký", "signed", "issued", "phát hành"];

    // ─── Kết quả ───
    public class ParsedEmailFields
    {
        public string? ReferenceNumber { get; set; }
        public DateTime? DocumentDate { get; set; }
        public string? Subject { get; set; }
        public int ReferenceNumberConfidence { get; set; }
        public int DateConfidence { get; set; }
    }

    // ─── Entry point ───
    public ParsedEmailFields ParseEmailFields(
        string? emailSubject,
        string? emailBodyText,
        string? emailBodyHtml,
        string? attachmentFileName,
        DateTime? emailSentDate)
    {
        var result = new ParsedEmailFields();

        var sources = new List<TextSource>();
        if (!string.IsNullOrWhiteSpace(emailSubject))
            sources.Add(new(emailSubject, SourceKind.Subject, 1.4));
        if (!string.IsNullOrWhiteSpace(emailBodyText))
            sources.Add(new(Truncate(emailBodyText, 3000), SourceKind.Body, 1.0));
        if (!string.IsNullOrWhiteSpace(emailBodyHtml))
        {
            var stripped = StripHtmlTags(emailBodyHtml);
            sources.Add(new(Truncate(stripped, 3000), SourceKind.HtmlBody, 0.9));
        }
        if (!string.IsNullOrWhiteSpace(attachmentFileName))
            sources.Add(new(attachmentFileName, SourceKind.FileName, 1.2));

        var refCandidates = sources.SelectMany(ExtractRefCandidates).ToList();
        var dateCandidates = sources.SelectMany(ExtractDateCandidates).ToList();

        // HTML table parse (highest priority if present)
        if (!string.IsNullOrWhiteSpace(emailBodyHtml) &&
            emailBodyHtml.Contains("<table", StringComparison.OrdinalIgnoreCase))
        {
            var (tRef, tDate) = ParseHtmlTable(emailBodyHtml);
            if (!string.IsNullOrWhiteSpace(tRef))
                refCandidates.Add(new(tRef, 90, SourceKind.HtmlTable));
            if (tDate.HasValue)
                dateCandidates.Add(new(tDate.Value.ToString("dd/MM/yyyy"), 90, SourceKind.HtmlTable, tDate));
        }

        BoostNearbyPairs(refCandidates, dateCandidates, sources);

        var bestRef = refCandidates.OrderByDescending(c => c.Score).FirstOrDefault();
        var bestDate = dateCandidates.OrderByDescending(c => c.Score).FirstOrDefault();

        if (bestRef != null && bestRef.Score >= 30)
        {
            result.ReferenceNumber = NormalizeRef(bestRef.Value);
            result.ReferenceNumberConfidence = Math.Min(bestRef.Score, 100);
        }

        if (bestDate?.ParsedDate != null && bestDate.Score >= 25)
        {
            result.DocumentDate = bestDate.ParsedDate;
            result.DateConfidence = Math.Min(bestDate.Score, 100);
        }
        else if (emailSentDate.HasValue)
        {
            result.DocumentDate = emailSentDate;
            result.DateConfidence = 10;
        }

        result.Subject = BuildSubject(emailSubject, result.ReferenceNumber);
        return result;
    }

    // ─── Extract ref candidates ───
    private static IEnumerable<ScoredCandidate> ExtractRefCandidates(TextSource src)
    {
        var list = new List<ScoredCandidate>();
        foreach (var pat in RefNumPatterns)
        {
            foreach (Match m in pat.Matches(src.Text))
            {
                var val = m.Value.Trim();
                if (Regex.IsMatch(val, @"^(19|20)\d{2}$")) continue; // skip bare years

                int score = (int)(28 * src.Weight);
                if (Regex.IsMatch(val, @"/[A-ZĐ]{2,}")) score += 20; // has org code

                int pos = src.Text.IndexOf(val, StringComparison.OrdinalIgnoreCase);
                var win = Window(src.Text, pos, 80).ToLowerInvariant();
                if (RefNumBoostKeywords.Any(kw => win.Contains(kw))) score += 40;

                // Penalize legislation citations
                var upper = val.ToUpperInvariant();
                if (upper.Contains("NĐ-CP") || upper.Contains("ND-CP") ||
                    upper.Contains("TT-BTC") || upper.Contains("QĐ-TTG") || upper.Contains("NQ-CP"))
                    score -= 60;

                if (score > 0) list.Add(new(val, score, src.Kind));
            }
        }
        return list;
    }

    // ─── Extract date candidates ───
    private static IEnumerable<ScoredCandidate> ExtractDateCandidates(TextSource src)
    {
        var list = new List<ScoredCandidate>();
        foreach (var pat in DatePatterns)
        {
            foreach (Match m in pat.Matches(src.Text))
            {
                int d = 0, mo = 0, y = 0;
                if (m.Groups.Count >= 4 && m.Groups[3].Value.Length == 4)
                {
                    // Tiếng Việt đầy đủ
                    int.TryParse(m.Groups[1].Value, out d);
                    int.TryParse(m.Groups[2].Value, out mo);
                    int.TryParse(m.Groups[3].Value, out y);
                }
                else
                {
                    var dg = m.Groups["day"]; var mg = m.Groups["month"]; var yg = m.Groups["year"];
                    if (dg.Success) int.TryParse(dg.Value, out d);
                    if (mg.Success) int.TryParse(mg.Value, out mo);
                    if (yg.Success) int.TryParse(yg.Value, out y);
                }

                if (d < 1 || d > 31 || mo < 1 || mo > 12 || y < 1990 || y > 2050) continue;
                DateTime? dt = null;
                try { dt = new DateTime(y, mo, d, 0, 0, 0, DateTimeKind.Utc); } catch { continue; }

                int score = (int)(22 * src.Weight);
                int pos = src.Text.IndexOf(m.Value, StringComparison.OrdinalIgnoreCase);
                var win = Window(src.Text, pos, 60).ToLowerInvariant();
                if (DateBoostKeywords.Any(kw => win.Contains(kw))) score += 40;

                list.Add(new(m.Value, score, src.Kind, dt));
            }
        }
        return list;
    }

    // ─── HTML table parser ───
    private static (string? refNum, DateTime? date) ParseHtmlTable(string html)
    {
        string? refNum = null;
        DateTime? date = null;
        var rows = Regex.Matches(html,
            @"<tr[^>]*>[\s\S]*?<td[^>]*>([^<]{2,80})</td>[\s\S]*?<td[^>]*>([^<]{1,200})</td>[\s\S]*?</tr>",
            RegexOptions.IgnoreCase);
        foreach (Match row in rows)
        {
            var label = StripHtmlTags(row.Groups[1].Value).Trim().ToLowerInvariant();
            var value = StripHtmlTags(row.Groups[2].Value).Trim();
            if (string.IsNullOrWhiteSpace(value)) continue;

            bool isRef = label.ContainsAny("số", "so", "ký hiệu", "ref", "mã số");
            bool isDate = label.ContainsAny("ngày", "date", "ban hành");

            if (isRef && refNum == null)
            {
                var m = Regex.Match(value, @"\d{1,5}[/\-][A-ZĐa-z0-9\-]+", RegexOptions.IgnoreCase);
                if (m.Success) refNum = m.Value;
            }
            if (isDate && date == null)
            {
                var m = Regex.Match(value, @"(\d{1,2})[/\-.](0?[1-9]|1[0-2])[/\-.]((?:19|20)\d{2})");
                if (m.Success &&
                    int.TryParse(m.Groups[1].Value, out var dv) &&
                    int.TryParse(m.Groups[2].Value, out var mv) &&
                    int.TryParse(m.Groups[3].Value, out var yv))
                {
                    try { date = new DateTime(yv, mv, dv, 0, 0, 0, DateTimeKind.Utc); } catch { }
                }
            }
        }
        return (refNum, date);
    }

    // ─── Cross-proximity boost ───
    private static void BoostNearbyPairs(
        List<ScoredCandidate> refs,
        List<ScoredCandidate> dates,
        List<TextSource> sources)
    {
        foreach (var src in sources)
        {
            foreach (var r in refs.Where(x => x.Kind == src.Kind))
            {
                int rPos = src.Text.IndexOf(r.Value, StringComparison.OrdinalIgnoreCase);
                if (rPos < 0) continue;
                foreach (var d in dates.Where(x => x.Kind == src.Kind))
                {
                    int dPos = src.Text.IndexOf(d.Value, StringComparison.OrdinalIgnoreCase);
                    if (dPos >= 0 && Math.Abs(rPos - dPos) < 250)
                    {
                        r.Score += 15;
                        d.Score += 15;
                    }
                }
            }
        }
    }

    // ─── Helpers ───
    private static string BuildSubject(string? raw, string? foundRef)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var s = Regex.Replace(raw, @"^\s*(?:Re|Fwd|FW|TR)\s*:\s*", "", RegexOptions.IgnoreCase).Trim();
        s = Regex.Replace(s, @"^[\[\(][^\]\)]{1,60}[\]\)]\s*", "").Trim();
        if (!string.IsNullOrWhiteSpace(foundRef))
            s = s.Replace(foundRef, "").Trim(' ', '-', '–', ':');
        return s.Length >= 3 ? s : raw.Trim();
    }

    private static string NormalizeRef(string val)
    {
        val = Regex.Replace(val, @"\s*/\s*", "/");
        val = Regex.Replace(val, @"\s*\\\s*", "/");
        return val.Trim();
    }

    private static string StripHtmlTags(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        var t = Regex.Replace(html, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase);
        t = Regex.Replace(t, @"<[^>]+>", " ");
        t = t.Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">").Replace("&nbsp;", " ");
        return Regex.Replace(t, @"\s{2,}", " ").Trim();
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s.Substring(0, max);

    private static string Window(string text, int pos, int half) =>
        pos < 0 ? string.Empty :
        text.Substring(Math.Max(0, pos - half), Math.Min(half * 2, text.Length - Math.Max(0, pos - half)));

    // ─── Internal types ───
    private enum SourceKind { Subject, Body, HtmlBody, HtmlTable, FileName }

    private record TextSource(string Text, SourceKind Kind, double Weight);

    private class ScoredCandidate(string value, int score, SourceKind kind, DateTime? parsedDate = null)
    {
        public string Value { get; } = value;
        public int Score { get; set; } = score;
        public SourceKind Kind { get; } = kind;
        public DateTime? ParsedDate { get; } = parsedDate;
    }
}

// ─── Helper extension ───
file static class StringExtensions
{
    public static bool ContainsAny(this string s, params string[] values) =>
        values.Any(v => s.Contains(v, StringComparison.OrdinalIgnoreCase));
}

/// <summary>MimeKit visitor để trích text/html body.</summary>
public class BodyTextExtractor : MimeVisitor
{
    public string? PlainText { get; private set; }
    public string? HtmlText { get; private set; }

    protected override void VisitTextPart(TextPart entity)
    {
        if (entity.IsHtml) HtmlText ??= entity.Text;
        else PlainText ??= entity.Text;
    }
}
