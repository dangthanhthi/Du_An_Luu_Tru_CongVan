using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace AiOcrService.Services.FuzzyCorrection
{
    /// <summary>
    /// Thay thế "Dictionary Auto-Correct" tĩnh (liệt kê tay từng cặp lỗi->đúng) bằng tra cứu
    /// gần đúng tổng quát: nếu 1 từ không có trong từ điển hành chính đúng (WordTrie),
    /// tìm từ gần nhất trong ngưỡng Levenshtein ≤2 (BKTree). Vì vậy sửa được cả lỗi
    /// CHƯA TỪNG THẤY, miễn là đủ gần 1 từ đã biết - đây là điểm khác biệt cốt lõi so với
    /// bảng ánh xạ cứng chỉ bắt đúng lỗi đã liệt kê.
    /// </summary>
    public sealed class TrieBkTreeFuzzyCorrector : IFuzzyTextCorrector
    {
        private readonly WordTrie _knownCorrectWords;
        private readonly BKTree _bkTree;
        private readonly List<CorrectionSuggestion> _lastCorrections = new();

        private static readonly Regex WordPattern = new(@"[\p{L}\d]+", RegexOptions.Compiled);

        public IReadOnlyList<CorrectionSuggestion> LastCorrections => _lastCorrections;

        /// <param name="vocabulary">
        /// Danh sách từ ĐÚNG trong từ vựng hành chính (không phải bảng lỗi->đúng cũ).
        /// Xem VocabularySeed.cs để chuyển bảng cũ sang danh sách này.
        /// </param>
        public TrieBkTreeFuzzyCorrector(IEnumerable<string> vocabulary)
        {
            var words = vocabulary.Select(w => w.Trim()).Where(w => w.Length > 0).Distinct().ToList();
            _knownCorrectWords = new WordTrie();
            _knownCorrectWords.AddRange(words);
            _bkTree = new BKTree();
            _bkTree.AddRange(words);
        }

        public string CorrectText(string text)
        {
            _lastCorrections.Clear();
            if (string.IsNullOrWhiteSpace(text)) return text ?? string.Empty;

            var sb = new StringBuilder();
            int lastIndex = 0;

            foreach (Match m in WordPattern.Matches(text))
            {
                sb.Append(text, lastIndex, m.Index - lastIndex); // giữ nguyên phần phân cách/khoảng trắng
                sb.Append(CorrectWord(m.Value));
                lastIndex = m.Index + m.Length;
            }
            sb.Append(text, lastIndex, text.Length - lastIndex);

            return sb.ToString();
        }

        private string CorrectWord(string word)
        {
            // 1. Đã đúng sẵn -> không đụng vào (đường đi nhanh nhất, không cần BK-Tree)
            if (_knownCorrectWords.Contains(word)) return word;
            if (_knownCorrectWords.Contains(word.ToLowerInvariant())) return word;

            // 2. Mã luật/số hiệu/từ quá ngắn -> KHÔNG sửa dù trông "gần giống" 1 từ nào đó
            if (ProtectedTokenGuard.IsProtected(word)) return word;

            // 3. Ngưỡng edit-distance co giãn theo độ dài từ - từ càng ngắn càng dễ bị
            // fuzzy-match nhầm sang từ khác hoàn toàn nghĩa, nên siết ngưỡng chặt hơn.
            int maxDistance = word.Length <= 5 ? 1 : 2;

            var candidates = _bkTree.FindWithinDistance(word.ToLowerInvariant(), maxDistance);
            if (candidates.Count == 0) return word; // không tìm được gì đủ gần -> giữ nguyên, không đoán mò

            var best = candidates.OrderBy(c => c.Distance).ToList();
            int minDistance = best[0].Distance;
            var bestCandidates = best.Where(c => c.Distance == minDistance).ToList();

            // 4. Nhiều ứng viên đồng hạng cùng khoảng cách -> không chắc chắn -> KHÔNG sửa.
            // Thà bỏ sót còn hơn sửa sai sang nghĩa khác (VD "hoa" gần cả "hòa" lẫn "họa" ở distance=1).
            if (bestCandidates.Count > 1) return word;

            var corrected = ApplyOriginalCasing(word, bestCandidates[0].Word);
            _lastCorrections.Add(new CorrectionSuggestion(word, corrected, minDistance));
            return corrected;
        }

        /// <summary>Giữ dạng viết hoa/thường gốc khi thay từ (VD "Thm" -> "Thẩm" giữ hoa đầu).</summary>
        private static string ApplyOriginalCasing(string original, string corrected)
        {
            if (original.Length == 0 || corrected.Length == 0) return corrected;
            if (char.IsUpper(original[0]) && !char.IsUpper(corrected[0]))
            {
                return char.ToUpperInvariant(corrected[0]) + corrected.Substring(1);
            }
            return corrected;
        }
    }
}
