using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace AiOcrService.Services.Extraction
{
    /// <summary>
    /// Bóc tách Người ký (Signer).
    /// Hỗ trợ cả văn bản số hóa (Digital Signature Stamp) và văn bản hành chính quét (Scan).
    /// </summary>
    public sealed class SignerExtractor : ISignerExtractor
    {
        private static readonly string[] FormBlacklist = new[]
        {
            "CÔNG VĂN", "CONG VAN", "QUYẾT ĐỊNH", "QUYET DINH", "THÔNG BÁO", "THONG BAO",
            "KẾ HOẠCH", "KE HOACH", "TỜ TRÌNH", "TO TRINH", "GIẤY MỜI", "GIAY MOI",
            "BIÊN BẢN", "BIEN BAN", "CHỈ THỊ", "CHI THI", "NGHỊ QUYẾT", "NGHI QUYET",
            "HƯỚNG DẪN", "HUONG DAN", "BÁO CÁO", "BAO CAO", "QUY CHẾ", "QUY CHE", "QUY ĐỊNH", "QUY DINH",
            "CHÍNH PHỦ", "CHINH PHU"
        };

        private static readonly string[] AgencyBlacklist = new[]
        {
            "bộ công thương", "bộ ngoại giao", "bộ giáo dục", "bộ y tế", "bộ thông tin", "bộ tư pháp",
            "ủy ban nhân dân", "uỷ ban nhân dân", "ubnd", "sở y tế", "sở tư pháp", "sở giáo dục",
            "tập đoàn", "công ty cổ phần", "đại học quốc gia", "trung tâm thông tin"
        };

        public void Extract(string fullText, ExtractedDocumentData result)
        {
            if (string.IsNullOrWhiteSpace(fullText)) return;

            // 1. ƯU TIÊN 1: Chữ ký số điện tử (Digital Signature Block)
            var digSigMatch = Regex.Match(fullText,
                @"(?:KÝ\s*BỞI\s*CHỮ\s*KÝ\s*SỐ|CHỮ\s*KÝ\s*SỐ\s*HỢP\s*LỆ|Digitally\s*signed\s*by)[\s\S]*?(?:Cơ\s*quan\s*:\s*|Người\s*ký\s*:\s*|by\s*:\s*)([A-ZÀ-Ỹ][A-Za-zÀ-ỹ\s\.\-]{2,50}?)(?=(?:Thời\s*gian\s*ký|Date\s*:|\d{4}[\.\/-]\d{2}|$))",
                RegexOptions.IgnoreCase);

            if (digSigMatch.Success)
            {
                var candidate = CleanCandidateName(digSigMatch.Groups[1].Value);
                if (IsValidPersonName(candidate))
                {
                    result.Signer = candidate;
                    return;
                }
            }

            var digTimestampMatch = Regex.Match(fullText,
                @"(?:KÝ\s*BỞI\s*CHỮ\s*KÝ\s*SỐ|CHỮ\s*KÝ\s*SỐ\s*HỢP\s*LỆ)[\s\S]*?(?:\+07:00|\d{2}:\d{2}:\d{2})\s*([A-ZÀ-Ỹ][A-Za-zÀ-ỹ\s\.\-]{2,50}?)$",
                RegexOptions.IgnoreCase);
            if (digTimestampMatch.Success)
            {
                var candidate = CleanCandidateName(digTimestampMatch.Groups[1].Value);
                if (IsValidPersonName(candidate))
                {
                    result.Signer = candidate;
                    return;
                }
            }

            // 2. ƯU TIÊN 2: Khối chức danh + Tên người ký ở phần cuối văn bản (quét từ dưới lên bằng RightToLeft)
            var titleBlockMatch = Regex.Match(fullText,
                @"(?:TM\.|KT\.|Q\.)?\s*(?:ỦY\s*BAN\s*NHÂN\s*DÂN|CƠ\s*QUAN\s*BAN\s*HÀNH|BỘ\s+[A-ZÀ-Ỹ\s]+|SỞ\s+[A-ZÀ-Ỹ\s]+)?\s*(?:CHÁNH\s*VĂN\s*PHÒNG|PHÓ\s*CHÁNH\s*VĂN\s*PHÒNG|CHỦ\s*TỊCH\s*HỘI\s*ĐỒNG|CHỦ\s*TỊCH|PHÓ\s*CHỦ\s*TỊCH|THỦ\s*TƯỚNG\s*CHÍNH\s*PHỦ|THỦ\s*TƯỚNG|PHÓ\s*THỦ\s*TƯỚNG|BỘ\s*TRƯỞNG|THỨ\s*TRƯỞNG|GIÁM\s*ĐỐC\s*TRUNG\s*TÂM|GIÁM\s*ĐỐC|PHÓ\s*GIÁM\s*ĐỐC|HIỆU\s*TRƯỞNG|PHÓ\s*HIỆU\s*TRƯỞNG|TỔNG\s*GIÁM\s*ĐỐC|TRƯỞNG\s*BAN[^\n\r]*?)\s*(?:(?:GS|PGS|TS|ThS|BS|CN|KTS|BSCKI|BSCKII)\.?\s*)*([A-ZÀ-Ỹ][a-zà-ỹ]+(?:\s+[A-ZÀ-Ỹ][a-zà-ỹ]+){1,4})(?=\s*(?:\r?\n|Nơi\s*nhận|Noi\s*nhan|Lưu\b|$))",
                RegexOptions.IgnoreCase | RegexOptions.RightToLeft);

            if (titleBlockMatch.Success)
            {
                var candidate = CleanCandidateName(titleBlockMatch.Groups[1].Value);
                if (IsValidPersonName(candidate))
                {
                    result.Signer = candidate;
                    return;
                }
            }

            // 3. ƯU TIÊN 3: Quét các dòng văn bản từ dưới lên (Dành cho bản in / scan / cuối văn bản)
            var lines = fullText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = lines.Length - 1; i >= Math.Max(0, lines.Length - 25); i--)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrWhiteSpace(line)) continue;

                if (line.Contains("Nơi nhận", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("Lưu:", StringComparison.OrdinalIgnoreCase))
                {
                    var sub = Regex.Replace(line, @"(?:Nơi\s*nhận|Lưu\s*:).*$", "", RegexOptions.IgnoreCase).Trim();
                    if (!string.IsNullOrWhiteSpace(sub))
                    {
                        var cand = CleanCandidateName(sub);
                        if (IsValidPersonName(cand))
                        {
                            result.Signer = cand;
                            return;
                        }
                    }
                    continue;
                }

                if (line.Contains("VT,", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("Kính gửi", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("Căn cứ", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Loại trừ nếu dòng chính là tên thể thức văn bản
                if (IsFormHeader(line)) continue;

                // Loại trừ nếu dòng là tên cơ quan hành chính
                var lower = line.ToLowerInvariant();
                if (AgencyBlacklist.Any(ab => lower.Contains(ab))) continue;

                // Xử lý trường hợp dòng chứa Chức danh + Tên người ký (VD: "Chánh Văn phòng Đặng Quốc Toàn")
                var cleanedName = CleanCandidateName(line);

                if (IsValidPersonName(cleanedName))
                {
                    result.Signer = cleanedName;
                    break;
                }
            }
        }

        private static bool IsFormHeader(string line)
        {
            var clean = line.Trim().Trim('_', '-', '=', ' ', ':');
            return FormBlacklist.Any(f => string.Equals(clean, f, StringComparison.OrdinalIgnoreCase));
        }

        private static string CleanCandidateName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            var name = raw.Trim().Trim('_', '-', '=', ' ', ':');

            // Cắt bỏ tiền tố chức danh hành chính
            name = Regex.Replace(name, @"^(?:TM\.|KT\.|Q\.)?\s*(?:ỦY\s*BAN\s*NHÂN\s*DÂN|CƠ\s*QUAN\s*BAN\s*HÀNH)\s*", "", RegexOptions.IgnoreCase);
            name = Regex.Replace(name, @"^(?:Chánh\s*Văn\s*phòng|Phó\s*Chánh\s*Văn\s*phòng|Chủ\s*tịch\s*Hội\s*đồng|Chủ\s*tịch|Phó\s*Chủ\s*tịch|Thủ\s*tướng(?:\s*Chính\s*phủ)?|Phó\s*Thủ\s*tướng|Bộ\s*trưởng|Thứ\s*trưởng|Giám\s*đốc\s*Trung\s*tâm|Giám\s*đốc|Phó\s*Giám\s*đốc|Tổng\s*Giám\s*đốc|Hiệu\s*trưởng|Phó\s*Hiệu\s*trưởng|Trưởng\s*ban[^\n\r]*?)\s*", "", RegexOptions.IgnoreCase);
            
            // Cắt bỏ toàn bộ học hàm học vị (kể cả chuỗi lồng nhau như GS.TS, PGS.TS, GS.TS.BS)
            name = Regex.Replace(name, @"^(?:(?:GS|PGS|TS|ThS|BS|CN|KTS|BSCKI|BSCKII)\.?\s*)+", "", RegexOptions.IgnoreCase);
            
            name = name.Trim().Trim('.', ',', ';', ':', '-', '_');
            return name;
        }

        private static bool IsValidPersonName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            if (name.Length < 4 || name.Length > 40) return false;
            if (Regex.IsMatch(name, @"\d")) return false;

            var lower = name.ToLowerInvariant();
            if (AgencyBlacklist.Any(ab => lower.Contains(ab))) return false;
            if (FormBlacklist.Any(fb => lower.Contains(fb.ToLowerInvariant()))) return false;

            var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length < 2 || words.Length > 5) return false;

            // Mỗi từ phải bắt đầu bằng chữ hoa tiếng Việt
            return words.All(w => w.Length >= 1 && char.IsUpper(w[0]));
        }
    }
}
