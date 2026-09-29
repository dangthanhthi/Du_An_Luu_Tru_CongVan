using System.Collections.Generic;

namespace AiOcrService.Services.FuzzyCorrection
{
    /// <summary>
    /// Trie cho việc tra cứu "từ này đã có trong từ điển hành chính đúng chưa" với chi phí
    /// O(độ dài từ), không phụ thuộc kích thước từ điển. Dùng làm bước lọc NHANH trước khi
    /// gọi BK-Tree (vốn tốn kém hơn) - nếu từ đã đúng thì bỏ qua fuzzy match hoàn toàn.
    /// </summary>
    public sealed class WordTrie
    {
        private sealed class Node
        {
            public readonly Dictionary<char, Node> Children = new();
            public bool IsWordEnd;
        }

        private readonly Node _root = new();

        public void Add(string word)
        {
            if (string.IsNullOrEmpty(word)) return;
            var node = _root;
            foreach (var c in word)
            {
                if (!node.Children.TryGetValue(c, out var child))
                {
                    child = new Node();
                    node.Children[c] = child;
                }
                node = child;
            }
            node.IsWordEnd = true;
        }

        public void AddRange(IEnumerable<string> words)
        {
            foreach (var w in words) Add(w);
        }

        public bool Contains(string word)
        {
            if (string.IsNullOrEmpty(word)) return false;
            var node = _root;
            foreach (var c in word)
            {
                if (!node.Children.TryGetValue(c, out var child)) return false;
                node = child;
            }
            return node.IsWordEnd;
        }
    }
}
