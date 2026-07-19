'use client';
import Header from '@/components/Header';

export default function Notifications() {
  const notis = [
    { id: '1', type: 'Khẩn', title: 'Công văn đến mới cần xử lý', text: 'Công văn CV-DEN-2026-00157 vừa được quét tự động từ máy Fax.', date: '19/07/2026 09:15' },
    { id: '2', type: 'Thông tin', title: 'Phê duyệt công văn đi', text: 'Giám đốc đã duyệt công văn đi CV-DI-2026-00089.', date: '18/07/2026 10:15' },
    { id: '3', type: 'Cảnh báo', title: 'Công văn sắp trễ hạn xử lý', text: 'Công văn CV-DEN-2026-00155 giao phòng Kế toán sắp hết hạn xử lý.', date: '17/07/2026 16:30' }
  ];

  return (
    <>
      <Header title="Trung tâm thông báo" />
      <main className="flex-1 p-8 max-w-4xl mx-auto w-full space-y-4">
        
        {notis.map((n) => (
          <div key={n.id} className="p-5 bg-card border border-border rounded-2xl flex items-start gap-4 shadow-[0_4px_20px_-4px_rgba(0,0,0,0.02)] transition-all duration-200">
            <span className={`w-10 h-10 rounded-xl flex items-center justify-center text-lg ${
              n.type === 'Khẩn' ? 'bg-red-50 text-red-600 dark:bg-red-950/20' :
              n.type === 'Cảnh báo' ? 'bg-amber-50 text-amber-600 dark:bg-amber-950/20' :
              'bg-emerald-50 text-emerald-600 dark:bg-emerald-950/20'
            }`}>
              {n.type === 'Khẩn' ? (
                <svg className="w-5 h-5 text-red-600 dark:text-red-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                  <path strokeLinecap="round" strokeLinejoin="round" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
                </svg>
              ) : n.type === 'Cảnh báo' ? (
                <svg className="w-5 h-5 text-amber-600 dark:text-amber-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                  <path strokeLinecap="round" strokeLinejoin="round" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
                </svg>
              ) : (
                <svg className="w-5 h-5 text-emerald-600 dark:text-emerald-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                  <path strokeLinecap="round" strokeLinejoin="round" d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                </svg>
              )}
            </span>
            <div className="flex-1">
              <h4 className="font-bold text-foreground text-sm">{n.title}</h4>
              <p className="text-muted-foreground text-xs mt-1.5 leading-relaxed font-medium">{n.text}</p>
              <span className="text-[9px] text-muted-foreground mt-2 block font-bold uppercase tracking-wider">{n.date}</span>
            </div>
          </div>
        ))}

      </main>
    </>
  );
}
