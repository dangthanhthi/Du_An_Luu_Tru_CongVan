using System;

namespace AiOcrService.Services.FuzzyCorrection
{
    public static class LevenshteinDistance
    {
        /// <summary>
        /// Tính khoảng cách Levenshtein, nhưng dừng sớm và trả về int.MaxValue nếu chắc chắn
        /// vượt quá maxDistance. Quan trọng cho hiệu năng: BK-Tree gọi hàm này hàng nghìn lần
        /// khi duyệt cây, nếu không early-exit sẽ rất chậm trên phần cứng yếu.
        /// </summary>
        public static int BoundedDistance(string a, string b, int maxDistance)
        {
            a ??= string.Empty;
            b ??= string.Empty;

            // Early-exit: chênh lệch độ dài đã vượt ngưỡng thì không cần tính chi tiết
            if (Math.Abs(a.Length - b.Length) > maxDistance) return int.MaxValue;
            if (a.Length == 0) return b.Length <= maxDistance ? b.Length : int.MaxValue;
            if (b.Length == 0) return a.Length <= maxDistance ? a.Length : int.MaxValue;

            var prev = new int[b.Length + 1];
            var curr = new int[b.Length + 1];
            for (int j = 0; j <= b.Length; j++) prev[j] = j;

            for (int i = 1; i <= a.Length; i++)
            {
                curr[0] = i;
                int rowMin = curr[0];
                for (int j = 1; j <= b.Length; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    curr[j] = Math.Min(Math.Min(curr[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
                    if (curr[j] < rowMin) rowMin = curr[j];
                }
                // Nếu cả hàng đều đã vượt ngưỡng, không thể nào hồi phục lại được nữa -> dừng sớm
                if (rowMin > maxDistance) return int.MaxValue;

                (prev, curr) = (curr, prev);
            }

            return prev[b.Length] <= maxDistance ? prev[b.Length] : int.MaxValue;
        }
    }
}
