using System;
using System.Collections.Generic;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace AiOcrService.Services.Concurrency
{
    /// <summary>
    /// Producer/Consumer pipeline cho xử lý nhiều trang PDF:
    /// - Producer: render (DocNet) + tiền xử lý ảnh (OpenCV/Magick) cho từng trang - KHÔNG cần lock,
    ///   có thể chạy trong lúc consumer đang OCR trang trước.
    /// - Consumer: gọi OCR (Paddle/Tesseract) - vẫn giữ nguyên lock hiện có bên trong actionOcr,
    ///   pipeline này KHÔNG cố gắng song song hoá bước OCR, chỉ tránh để CPU rảnh trong lúc chờ.
    ///
    /// Lợi ích: trong workload nhiều trang, tổng thời gian ≈ max(tổng thời gian OCR, tổng thời gian
    /// preprocess) thay vì tổng của cả hai cộng lại - không cần thêm engine instance nào, không tốn
    /// thêm RAM đáng kể (chỉ giữ 1-2 trang trong buffer tại 1 thời điểm).
    /// </summary>
    public sealed class PagePipelineProcessor<TPreprocessed, TResult>
    {
        private readonly Func<int, Task<TPreprocessed>> _preprocessPageAsync; // KHÔNG lock
        private readonly Func<TPreprocessed, Task<TResult>> _ocrPageAsync;    // giữ nguyên lock bên trong

        public PagePipelineProcessor(
            Func<int, Task<TPreprocessed>> preprocessPageAsync,
            Func<TPreprocessed, Task<TResult>> ocrPageAsync)
        {
            _preprocessPageAsync = preprocessPageAsync;
            _ocrPageAsync = ocrPageAsync;
        }

        /// <param name="pageCount">Tổng số trang cần xử lý.</param>
        /// <param name="bufferSize">
        /// Số trang được phép tiền xử lý "đón đầu" trước khi OCR kịp xử lý. Giữ nhỏ (1-2)
        /// để không tốn nhiều RAM decode ảnh trên máy yếu - đây không phải chỗ cần buffer lớn.
        /// </param>
        public async IAsyncEnumerable<TResult> ProcessAsync(int pageCount, int bufferSize = 2)
        {
            var channel = Channel.CreateBounded<TPreprocessed>(new BoundedChannelOptions(bufferSize)
            {
                SingleReader = true,
                SingleWriter = true,
                FullMode = BoundedChannelFullMode.Wait, // producer tự chờ nếu consumer xử lý chậm hơn
            });

            var producerTask = Task.Run(async () =>
            {
                try
                {
                    for (int i = 0; i < pageCount; i++)
                    {
                        var preprocessed = await _preprocessPageAsync(i).ConfigureAwait(false);
                        await channel.Writer.WriteAsync(preprocessed).ConfigureAwait(false);
                    }
                }
                finally
                {
                    channel.Writer.Complete();
                }
            });

            await foreach (var preprocessed in channel.Reader.ReadAllAsync())
            {
                yield return await _ocrPageAsync(preprocessed).ConfigureAwait(false);
            }

            await producerTask.ConfigureAwait(false); // đảm bảo lỗi trong producer được ném ra ngoài
        }
    }
}
