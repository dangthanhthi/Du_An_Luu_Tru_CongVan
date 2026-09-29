using System;
using System.Threading;
using System.Threading.Tasks;

namespace AiOcrService.Services.Concurrency
{
    public sealed class SemaphoreOcrConcurrencyGate : IOcrConcurrencyGate, IDisposable
    {
        private readonly SemaphoreSlim _semaphore;

        /// <param name="maxConcurrentFiles">
        /// Số file tối đa được xử lý song song. Trên phần cứng yếu, giá trị này nên được
        /// XÁC ĐỊNH BẰNG BENCHMARK THẬT (xem OcrConcurrencyBenchmark.cs), không đoán theo
        /// công thức ProcessorCount/2 - vì bản chất OCR đã bị serialize bởi _paddleLock,
        /// nên "song song" ở đây chỉ có tác dụng với phần preprocessing, không phải OCR.
        /// </param>
        public SemaphoreOcrConcurrencyGate(int maxConcurrentFiles)
        {
            var slots = Math.Max(1, maxConcurrentFiles);
            _semaphore = new SemaphoreSlim(slots, slots);
        }

        public int AvailableSlots => _semaphore.CurrentCount;

        public async Task<IDisposable> EnterAsync()
        {
            await _semaphore.WaitAsync().ConfigureAwait(false);
            return new Releaser(_semaphore);
        }

        public void Dispose() => _semaphore.Dispose();

        private sealed class Releaser : IDisposable
        {
            private readonly SemaphoreSlim _semaphore;
            private bool _released;
            public Releaser(SemaphoreSlim semaphore) => _semaphore = semaphore;

            public void Dispose()
            {
                if (_released) return;
                _released = true;
                _semaphore.Release();
            }
        }
    }
}
