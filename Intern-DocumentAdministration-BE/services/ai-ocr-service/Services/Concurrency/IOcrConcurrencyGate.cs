using System;
using System.Threading.Tasks;

namespace AiOcrService.Services.Concurrency
{
    /// <summary>
    /// Giới hạn số FILE được phép xử lý đồng thời (nhận việc), KHÔNG PHẢI số lời gọi OCR
    /// đồng thời (việc đó vẫn do _paddleLock/_tessLock quyết định, giữ nguyên = 1).
    /// Mục đích: khi có N request tới cùng lúc, tránh nhận hết N task vào RAM/CPU cùng lúc
    /// gây tràn tài nguyên trên máy yếu - xếp hàng có kiểm soát thay vì vô hạn.
    /// </summary>
    public interface IOcrConcurrencyGate
    {
        /// <summary>
        /// Chờ đến khi có "chỗ trống" để bắt đầu xử lý 1 file. Trả về IDisposable -
        /// dispose (hoặc dùng `using`) để trả lại chỗ trống khi xử lý xong.
        /// </summary>
        Task<IDisposable> EnterAsync();

        /// <summary>Số chỗ trống hiện tại, phục vụ logging/giám sát tải hệ thống.</summary>
        int AvailableSlots { get; }
    }
}
