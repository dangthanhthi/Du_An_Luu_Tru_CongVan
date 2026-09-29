using System.Text.RegularExpressions;

namespace AiOcrService.Services.Glyphs
{
    /// <summary>
    /// Chuyển nguyên vẹn logic từ DynamicFieldExtractor.NormalizeHandwrittenGlyphs sang đây,
    /// KHÔNG đổi hành vi (đổi hành vi là việc của Bước 3 - Fuzzy Match). Mục tiêu Bước 2
    /// chỉ là: (1) cô lập khỏi God Class, (2) bắt buộc đi qua IsNumericToken trước khi normalize,
    /// (3) mỗi rule là 1 phần tử có thể unit-test riêng thay vì 1 hàm 80 dòng.
    /// </summary>
    public sealed class HandwrittenGlyphNormalizer : IGlyphNormalizer
    {
        // Thứ tự CỰC KỲ quan trọng - các rule sau phụ thuộc kết quả rule trước
        // (vd: rule "3" tổng quát ở cuối sẽ ăn luôn ký tự mà rule cụm "31" đặc thù phía trên
        // lẽ ra phải xử lý trước). KHÔNG sắp xếp lại danh sách này khi thêm rule mới -
        // chỉ chèn rule mới vào đúng vị trí ngữ nghĩa (rule đặc thù trước, rule tổng quát sau).
        private static readonly GlyphRule[] Rules =
        {
            // --- Nhóm 1: cặp ký tự dính nét / viết tay đặc thù (phải chạy TRƯỚC nhóm tổng quát) ---
            GlyphRule.Create("Cụm 16 dính nét (4c, 4C, 4o...)", @"\b4[¢cCoOb6]\b", "16"),
            GlyphRule.Create("Cụm 16 dính nét (neo đầu-cuối)", @"^4[¢cCoOb6]$", "16"),
            GlyphRule.Create("Số 1 thành gạch/chấm trước số 6", @"^[./\\|lIi!\]\[]6$", "16"),
            GlyphRule.Create("Cụm 16 biến thể (1c, 1C...)", @"^1[¢cCoOb]$", "16"),

            GlyphRule.Create("Dagger sau số 3 -> 31 (neo)", @"^3[†‡tTlIi!|/\\\]]$", "31"),
            GlyphRule.Create("Dagger sau số 3 -> 31 (word boundary)", @"\b3[†‡tTlIi!|/\\\]]\b", "31"),
            GlyphRule.Create("Dagger sau số 2 -> 21", @"^2[†‡lIi!|/\\\]]$", "21"),
            GlyphRule.Create("Dagger sau số 1 -> 11", @"^1[†‡lIi!|/\\\]]$", "11"),
            GlyphRule.Create("Dagger sau 0/O/D -> 01", @"^[oO0D][†‡lIi!|/\\\]]$", "01"),
            GlyphRule.Create("Dagger đứng đơn lẻ -> 1", @"[†‡]", "1"),

            GlyphRule.Create("Lỗi Tesseract đặc thù: (A -> 13", @"^\(A$", "13"),
            GlyphRule.Create("Lỗi Tesseract đặc thù: g2/q2/Q2 -> 12", @"^[gqQ]2$", "12"),

            // --- Nhóm 1b: cặp glyph mở rộng ---
            GlyphRule.Create("Cụm 11 (ll, II, 1l, l1, !!)", @"^[lI!][lI!]$", "11"),
            GlyphRule.Create("Cụm 11 (1 + l/I/!)", @"^1[lI!]$", "11"),
            GlyphRule.Create("Cụm 11 (l/I/! + 1)", @"^[lI!]1$", "11"),
            GlyphRule.Create("Cụm 77 (TT)", @"^TT$", "77"),
            GlyphRule.Create("Cụm 77 (7T)", @"^7T$", "77"),
            GlyphRule.Create("Cụm 77 (T7)", @"^T7$", "77"),
            GlyphRule.Create("Cụm 00 (OO, oo)", @"^[Oo][Oo]$", "00"),
            GlyphRule.Create("Cụm 00 (0O, 0o)", @"^0[Oo]$", "00"),
            GlyphRule.Create("Cyrillic Ч/ч -> 4", @"[Чч]", "4"),
            GlyphRule.Create("Modifier letter ʻʼ -> 9", @"[ʻʼ]", "9"),
            GlyphRule.Create("¡¦ -> 1", @"[¡¦]", "1"),
            GlyphRule.Create("&∞ -> 8", @"[&∞]", "8"),
            GlyphRule.Create("Cụm 19 (1q, 1g)", @"^1[qg]$", "19"),
            GlyphRule.Create("Cụm 29 (2q, 2g)", @"^2[qg]$", "29"),
            GlyphRule.Create("Cụm 10 (1o, lo, l0...)", @"^[1l][oO]$", "10"),

            // --- Nhóm 2: số 02 trong font in nghiêng (Nghị định 30/2020/NĐ-CP) ---
            GlyphRule.Create("02 in nghiêng (neo đầu-cuối)", @"^[oO0D][sSzZeE\?]$", "02"),
            GlyphRule.Create("02 in nghiêng (word boundary)", @"\b[oO0D][sSzZeE\?]\b", "02"),

            // --- Nhóm 3: 12/22/20-29 với số 2 in nghiêng đọc thành s/z ---
            GlyphRule.Create("12 in nghiêng", @"^[1lI][sSzZeE]$", "12"),
            GlyphRule.Create("22 in nghiêng", @"^[sSzZ][sSzZeE]$", "22"),
            GlyphRule.Create("2x in nghiêng (20-29)", @"^[sSzZ]([0-9])$", "2$1"),
            GlyphRule.Create("25 in nghiêng (ZÔ...)", @"^[sSzZ][ÔỔỖỐỒƠỚỜơớờôổỗốồ]$", "25"),

            // --- Nhóm 4: ký hiệu dính nét đơn lẻ ---
            GlyphRule.Create("© -> 02", @"©", "02"),

            // Xóa ký tự dấu câu ngoài rìa, giữ lại các glyph số tiềm năng
            GlyphRule.Create("Cắt dấu câu biên", @"^[^\p{L}\d\[\]\|lI!ZzSsBbTt\?®©%#~^§ÀÂA¢†‡]+|[^\p{L}\d\[\]\|lI!ZzSsBbTt\?®©%#~^§ÀÂA¢†‡]+$", ""),

            // --- Nhóm 5-14: ánh xạ glyph -> chữ số đơn (chạy SAU CÙNG vì mang tính tổng quát) ---
            GlyphRule.Create("3 viết tay (À, Â, A)", @"[ÀÂA]", "3"),
            GlyphRule.Create("2 viết tay/nghiêng còn lại (z, Z)", @"[zZ]", "2"),
            GlyphRule.Create("5 viết tay (nguyên âm có dấu mũ/móc)", @"[ÔỔỖỐỒƠỚỜơớờôổỗốồ§éèê]", "5"),
            GlyphRule.Create("1 (ký tự thẳng đứng)", @"[\[\]\|lI!Jj†‡]", "1"),
            GlyphRule.Create("6 (b)", @"b", "6"), // giữ Replace() thường như bản gốc, không cần regex
            GlyphRule.Create("7 (nét xiên/gạch ngang)", @"[Tt%#~^\?]", "7"),
            GlyphRule.Create("8 (B, ®)", @"[B®]", "8"),
            GlyphRule.Create("9 (g, q)", @"[gq]", "9"),
            GlyphRule.Create("0 (O, o, D)", @"[oOD]", "0"),
            GlyphRule.Create("2 (s/S còn sót lại)", @"[sS]", "2"),
        };

        /// <inheritdoc />
        public bool IsNumericToken(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return false;
            var trimmed = raw.Trim();
            if (trimmed.Length > 8) return false;

            if (VietnameseLetterPattern.IsMatch(trimmed)) return false;

            return AllowedGlyphPattern.IsMatch(trimmed);
        }

        /// <inheritdoc />
        public string Normalize(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            var cleaned = raw.Trim();

            foreach (var rule in Rules)
            {
                cleaned = rule.Apply(cleaned);
            }

            return Regex.Replace(cleaned, @"\D", "");
        }

        /// <inheritdoc />
        public string NormalizeOrFallback(string raw)
        {
            return IsNumericToken(raw)
                ? Normalize(raw)
                : Regex.Replace(raw ?? string.Empty, @"\D", ""); // không đoán mò, chỉ giữ số sẵn có
        }

        private static readonly Regex VietnameseLetterPattern = new(
            @"[áàảãạăắằẳẵặâấầẩẫậéèẻẽẹêếềểễệíìỉĩịóòỏõọôốồổỗộơớờởỡợúùủũụưứừửữựýỳỷỹỵđĐÁÀẢÃẠĂẮẰẲẴẶÂẤẦẨẪẬÉÈẺẼẸÊẾỀỂỄỆÍÌỈĨỊÓÒỎÕỌÔỐỒỔỖỘƠỚỜỞỠỢÚÙỦŨỤƯỨỪỬỮỰÝỲỶỸỴ]",
            RegexOptions.Compiled);

        private static readonly Regex AllowedGlyphPattern = new(
            @"^[\d\s\.\-bszoODlITtgqBcC¢©†‡ÀÂAÇçŒœSZsZzÔỔỖỐỒƠỚỜơớờôổỗốồ§éèêБЧч\[\]|!?%#~^&∞¡¦ʻʼ®]+$",
            RegexOptions.Compiled);
    }
}
