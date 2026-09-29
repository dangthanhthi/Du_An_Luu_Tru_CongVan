using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace AiOcrService.Services.Concurrency
{
    /// <summary>
    /// Đo thời gian xử lý thật của 1 tập file mẫu với các mức giới hạn concurrency khác nhau,
    /// TRÊN CHÍNH PHẦN CỨNG BẠN ĐANG DÙNG. Không dùng con số suy đoán (ProcessorCount/2) -
    /// đo xong mới quyết định. Chạy 1 lần khi deploy lên máy mới, hoặc khi đổi phần cứng.
    ///
    /// Cách dùng:
    /// var results = await OcrConcurrencyBenchmark.RunAsync(
    ///     sampleFilePaths: Directory.GetFiles("sample-pdfs", "*.pdf"),
    ///     processOneFileAsync: path => myOcrService.ProcessAsync(path),
    ///     concurrencyLevelsToTry: new[] { 1, 2, Environment.ProcessorCount / 2 });
    /// Console.WriteLine(results.ToReportString());
    /// </summary>
    public static class OcrConcurrencyBenchmark
    {
        public sealed record BenchmarkResult(int ConcurrencyLevel, TimeSpan TotalElapsed, int FileCount)
        {
            public double AvgSecondsPerFile => FileCount == 0 ? 0 : TotalElapsed.TotalSeconds / FileCount;
        }

        public static async Task<List<BenchmarkResult>> RunAsync(
            IReadOnlyList<string> sampleFilePaths,
            Func<string, Task> processOneFileAsync,
            IEnumerable<int> concurrencyLevelsToTry)
        {
            var results = new List<BenchmarkResult>();

            foreach (var level in concurrencyLevelsToTry)
            {
                using var gate = new SemaphoreOcrConcurrencyGate(level);
                var sw = Stopwatch.StartNew();

                var tasks = sampleFilePaths.Select(async path =>
                {
                    using var _ = await gate.EnterAsync();
                    await processOneFileAsync(path);
                });
                await Task.WhenAll(tasks);

                sw.Stop();
                results.Add(new BenchmarkResult(level, sw.Elapsed, sampleFilePaths.Count));
            }

            return results;
        }

        public static string ToReportString(this List<BenchmarkResult> results)
        {
            var lines = new List<string> { "=== Kết quả Benchmark Concurrency (thấp hơn = nhanh hơn) ===" };
            foreach (var r in results.OrderBy(r => r.TotalElapsed))
            {
                lines.Add($"- Mức {r.ConcurrencyLevel} file song song: {r.TotalElapsed.TotalSeconds:0.00}s tổng " +
                          $"({r.AvgSecondsPerFile:0.00}s/file, {r.FileCount} file)");
            }
            var best = results.OrderBy(r => r.TotalElapsed).First();
            lines.Add($"=> Nên dùng maxConcurrentFiles = {best.ConcurrencyLevel} trên phần cứng này.");
            return string.Join("\n", lines);
        }
    }
}
