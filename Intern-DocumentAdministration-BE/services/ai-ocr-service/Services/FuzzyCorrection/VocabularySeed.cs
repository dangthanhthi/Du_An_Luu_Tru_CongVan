using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AiOcrService.Services.FuzzyCorrection
{
    /// <summary>
    /// Bảng auto-correct cũ trong tài liệu dự án (mục 3) là dạng lỗi->đúng, chỉ dùng được
    /// cho ĐÚNG lỗi đã liệt kê. Với BK-Tree/Trie, ta chỉ cần vế ĐÚNG (từ vựng hợp lệ) - tách
    /// thành từng từ đơn, hệ thống sẽ tự tìm được các lỗi TƯƠNG TỰ chưa từng thấy.
    ///
    /// Tự động nạp từ file admin-vocabulary.txt nếu có, hoặc fallback về danh sách tĩnh.
    /// </summary>
    public static class VocabularySeed
    {
        public static IEnumerable<string> FromLegacyAutoCorrectTable()
        {
            var words = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Nạp từ file từ điển admin-vocabulary.txt nếu tồn tại (theo WIRING_NOTES.md Mục 5)
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var pathsToTry = new[]
            {
                Path.Combine(baseDir, "data", "admin-vocabulary.txt"),
                Path.Combine(baseDir, "..", "..", "..", "data", "admin-vocabulary.txt"),
                Path.Combine(baseDir, "..", "..", "..", "..", "services", "ai-ocr-service", "data", "admin-vocabulary.txt"),
                Path.Combine("Intern-DocumentAdministration-BE", "services", "ai-ocr-service", "data", "admin-vocabulary.txt")
            };

            foreach (var p in pathsToTry)
            {
                if (File.Exists(p))
                {
                    try
                    {
                        foreach (var line in File.ReadAllLines(p))
                        {
                            var trimmed = line.Trim();
                            if (!string.IsNullOrWhiteSpace(trimmed))
                                words.Add(trimmed);
                        }
                        if (words.Count > 0) return words;
                    }
                    catch { }
                }
            }

            // Vế ĐÚNG của bảng Mục 3 trong tài liệu dự án, tách thành từ đơn.
            var correctPhrases = new[]
            {
                "thủ tục", "thẩm định", "ý kiến", "đối với", "danh mục tài liệu",
                "hết giá trị", "đơn vị công lập", "trực thuộc", "sở y tế",
                "triển khai", "phổ biến", "thực hiện", "hướng dẫn", "định nhu cầu",
                "phương thức", "điều chỉnh tiền lương", "nghị định", "quốc tế", "tranh chấp",
                "quyết định", "thông báo", "chỉ thị", "kế hoạch", "báo cáo", "nghị quyết",
                "hợp đồng", "giấy mời", "tờ trình", "biên bản", "quy chế", "hướng dẫn", "công điện",
                "công văn", "cộng hòa", "độc lập", "tự do", "hạnh phúc",
                "mạng lưới", "kháng thuốc", "giám sát", "phân quyền", "chính quyền", "địa phương", "mô hình", "gắn với"
            };

            foreach (var phrase in correctPhrases)
            {
                foreach (var w in phrase.Split(' '))
                {
                    if (w.Length > 0) words.Add(w);
                }
            }

            return words;
        }
    }
}
