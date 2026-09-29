using System.Collections.Generic;

namespace AiOcrService.Services.FuzzyCorrection
{
    /// <summary>
    /// BK-Tree: cấu trúc cây cho phép tra cứu "từ nào trong từ điển gần giống chuỗi X nhất,
    /// trong phạm vi khoảng cách edit ≤ k" mà KHÔNG cần so sánh tuyến tính với toàn bộ từ điển.
    /// Đây là thứ thay thế cho việc liệt kê từng cặp lỗi->đúng thủ công trong bảng auto-correct cũ.
    /// Xây 1 lần khi khởi động service (từ điển hành chính không đổi giữa các request),
    /// sau đó tra cứu nhiều lần với chi phí thấp.
    /// </summary>
    public sealed class BKTree
    {
        private sealed class Node
        {
            public readonly string Word;
            public readonly Dictionary<int, Node> Children = new();
            public Node(string word) => Word = word;
        }

        private Node? _root;

        public void Add(string word)
        {
            if (string.IsNullOrWhiteSpace(word)) return;
            if (_root is null)
            {
                _root = new Node(word);
                return;
            }

            var current = _root;
            while (true)
            {
                // maxDistance lớn ở đây chỉ để XÂY cây (không phải tra cứu), nên không cần bound chặt
                int dist = LevenshteinDistance.BoundedDistance(word, current.Word, int.MaxValue - 1);
                if (dist == 0) return; // từ đã tồn tại, bỏ qua trùng lặp

                if (current.Children.TryGetValue(dist, out var child))
                {
                    current = child;
                }
                else
                {
                    current.Children[dist] = new Node(word);
                    return;
                }
            }
        }

        public void AddRange(IEnumerable<string> words)
        {
            foreach (var w in words) Add(w);
        }

        /// <summary>
        /// Trả về tất cả từ trong cây có khoảng cách edit ≤ maxDistance so với query,
        /// kèm khoảng cách cụ thể để caller tự chọn ứng viên tốt nhất (gần nhất, hoặc
        /// nhiều ứng viên cùng khoảng cách -> caller quyết định có nên sửa hay không).
        /// </summary>
        public List<(string Word, int Distance)> FindWithinDistance(string query, int maxDistance)
        {
            var results = new List<(string, int)>();
            if (_root is null || string.IsNullOrWhiteSpace(query)) return results;

            SearchRecursive(_root, query, maxDistance, results);
            return results;
        }

        private static void SearchRecursive(Node node, string query, int maxDistance, List<(string, int)> results)
        {
            int dist = LevenshteinDistance.BoundedDistance(query, node.Word, int.MaxValue - 1);
            if (dist <= maxDistance)
            {
                results.Add((node.Word, dist));
            }

            // Bất đẳng thức tam giác của BK-Tree: chỉ cần duyệt các nhánh con có khoảng cách cạnh
            // thỏa mãn: dist - maxDistance <= kvp.Key <= dist + maxDistance
            int lower = dist - maxDistance;
            int upper = dist + maxDistance;
            foreach (var kvp in node.Children)
            {
                if (kvp.Key >= lower && kvp.Key <= upper)
                {
                    SearchRecursive(kvp.Value, query, maxDistance, results);
                }
            }
        }
    }
}
