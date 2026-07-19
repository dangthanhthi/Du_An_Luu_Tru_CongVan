'use client';
import Header from '@/components/Header';
import Link from 'next/link';

export default function Dashboard() {
  const stats = [
    { 
      name: 'Công văn đến', 
      value: '157', 
      icon: (
        <svg className="w-6 h-6 text-accent" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
          <path strokeLinecap="round" strokeLinejoin="round" d="M19 13h-6v6h-2v-6H5v-2h6V5h2v6h6v2z" />
        </svg>
      ),
      color: 'bg-emerald-50 dark:bg-emerald-950/20' 
    },
    { 
      name: 'Công văn đi', 
      value: '89', 
      icon: (
        <svg className="w-6 h-6 text-muted-foreground" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
          <path strokeLinecap="round" strokeLinejoin="round" d="M12 19l9 2-9-18-9 18 9-2zm0 0v-8" />
        </svg>
      ), 
      color: 'bg-muted' 
    },
    { 
      name: 'Công văn nội bộ', 
      value: '34', 
      icon: (
        <svg className="w-6 h-6 text-muted-foreground" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
          <path strokeLinecap="round" strokeLinejoin="round" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
        </svg>
      ), 
      color: 'bg-muted' 
    },
    { 
      name: 'Chờ xử lý', 
      value: '18', 
      icon: (
        <svg className="w-6 h-6 text-amber-600 dark:text-amber-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
          <path strokeLinecap="round" strokeLinejoin="round" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
        </svg>
      ), 
      color: 'bg-amber-50 dark:bg-amber-950/20' 
    },
  ];

  const recentDocs = [
    { id: '1', docNo: 'CV-DEN-2026-00157', type: 'Đến', subject: 'V/v hướng dẫn công tác báo cáo công văn lưu trữ quý II', status: 'Chờ xử lý', date: '19/07/2026' },
    { id: '2', docNo: 'CV-DI-2026-00089', type: 'Đi', subject: 'Quyết định bổ nhiệm nhân sự phòng kế toán', status: 'Đã phát hành', date: '18/07/2026' },
    { id: '3', docNo: 'CV-NB-2026-00034', type: 'Nội bộ', subject: 'Thông báo lịch nghỉ phép tập thể năm 2026', status: 'Đã phân phối', date: '17/07/2026' },
  ];

  return (
    <>
      <Header title="Dashboard thống kê" />
      <main className="flex-1 p-8 space-y-8 max-w-7xl mx-auto w-full">
        
        {/* Stat Cards */}
        <div className="grid grid-cols-1 md:grid-cols-4 gap-6">
          {stats.map((s, idx) => (
            <div key={idx} className="p-6 bg-card border border-border rounded-2xl flex items-center justify-between shadow-[0_4px_20px_-4px_rgba(0,0,0,0.02)] transition-all duration-200">
              <div>
                <p className="text-muted-foreground text-xs font-bold uppercase tracking-wider">{s.name}</p>
                <h3 className="text-3xl font-extrabold mt-2 text-foreground">{s.value}</h3>
              </div>
              <div className={`w-12 h-12 rounded-xl flex items-center justify-center ${s.color}`}>
                {s.icon}
              </div>
            </div>
          ))}
        </div>

        {/* Charts & System health */}
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-8">
          
          {/* Custom chart */}
          <div className="p-6 bg-card border border-border rounded-2xl lg:col-span-2 space-y-6 transition-all duration-200">
            <h4 className="font-bold text-sm text-foreground uppercase tracking-wide">Thống kê lượng công văn 6 tháng gần nhất</h4>
            <div className="h-64 flex items-end justify-between px-4 pt-8">
              {[
                { month: 'T2', val: '75%', val2: '40%' },
                { month: 'T3', val: '85%', val2: '50%' },
                { month: 'T4', val: '60%', val2: '35%' },
                { month: 'T5', val: '95%', val2: '65%' },
                { month: 'T6', val: '70%', val2: '45%' },
                { month: 'T7', val: '110%', val2: '75%' }
              ].map((bar, idx) => (
                <div key={idx} className="flex flex-col items-center gap-2 w-1/6">
                  <div className="w-full flex justify-center gap-2 h-44 items-end">
                    <div className="w-4 bg-foreground rounded-t" style={{ height: bar.val }}></div>
                    <div className="w-4 bg-accent rounded-t" style={{ height: bar.val2 }}></div>
                  </div>
                  <span className="text-[11px] text-muted-foreground font-bold">{bar.month}</span>
                </div>
              ))}
            </div>
            <div className="flex justify-center gap-6 text-[11px] font-bold border-t border-border pt-4">
              <span className="flex items-center gap-2"><span className="w-3 h-3 bg-foreground rounded"></span>Công văn đến</span>
              <span className="flex items-center gap-2"><span className="w-3 h-3 bg-accent rounded"></span>Công văn đi</span>
            </div>
          </div>

          {/* System Health Check & Resource Monitor */}
          <div className="p-6 bg-card border border-border rounded-2xl space-y-6 transition-all duration-200">
            <h4 className="font-bold text-sm text-foreground uppercase tracking-wide">Giám sát tài nguyên hệ thống</h4>
            
            <div className="space-y-5 text-xs font-semibold">
              <div>
                <div className="flex justify-between mb-1.5">
                  <span className="text-muted-foreground">Dung lượng Database (das.db)</span>
                  <span className="font-bold text-foreground">14.2 MB / 500 MB</span>
                </div>
                <div className="w-full h-1.5 bg-muted rounded-full overflow-hidden">
                  <div className="h-full bg-accent rounded-full" style={{ width: '3%' }}></div>
                </div>
              </div>

              <div>
                <div className="flex justify-between mb-1.5">
                  <span className="text-muted-foreground">Sử dụng CPU (API Service)</span>
                  <span className="font-bold text-foreground">12%</span>
                </div>
                <div className="w-full h-1.5 bg-muted rounded-full overflow-hidden">
                  <div className="h-full bg-accent rounded-full" style={{ width: '12%' }}></div>
                </div>
              </div>

              <div>
                <div className="flex justify-between mb-1.5">
                  <span className="text-muted-foreground">Sử dụng RAM (API Service)</span>
                  <span className="font-bold text-foreground">158 MB / 1024 MB</span>
                </div>
                <div className="w-full h-1.5 bg-muted rounded-full overflow-hidden">
                  <div className="h-full bg-accent rounded-full" style={{ width: '15%' }}></div>
                </div>
              </div>

              <div className="pt-3 border-t border-border space-y-2">
                <div className="flex justify-between">
                  <span className="text-muted-foreground font-semibold">Tình trạng Email Watcher:</span>
                  <span className="px-2 py-0.5 bg-emerald-100 text-emerald-800 dark:bg-emerald-950/20 dark:text-emerald-450 font-bold rounded">Hoạt động</span>
                </div>
                <div className="flex justify-between">
                  <span className="text-muted-foreground font-semibold">Lần quét email cuối:</span>
                  <span className="font-bold text-foreground">11:00 (Cách đây 15 phút)</span>
                </div>
              </div>
            </div>
          </div>
        </div>

        {/* Recent Documents Table */}
        <div className="p-6 bg-card border border-border rounded-2xl space-y-4 transition-all duration-200">
          <div className="flex justify-between items-center">
            <h4 className="font-bold text-sm text-foreground uppercase tracking-wide">Công văn mới cập nhật</h4>
            <Link href="/documents/incoming" className="text-xs font-bold text-accent hover:underline">Xem tất cả</Link>
          </div>
          <div className="overflow-x-auto">
            <table className="w-full text-sm text-left">
              <thead>
                <tr className="border-b border-border text-muted-foreground text-[10px] uppercase font-bold tracking-wider">
                  <th className="py-3">Số công văn</th>
                  <th className="py-3">Loại</th>
                  <th className="py-3">Trích yếu</th>
                  <th className="py-3">Ngày</th>
                  <th className="py-3">Trạng thái</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {recentDocs.map((doc) => (
                  <tr key={doc.id} className="hover:bg-muted/30">
                    <td className="py-4 font-bold text-foreground hover:underline">
                      <Link href={`/documents/incoming/${doc.id}`}>{doc.docNo}</Link>
                    </td>
                    <td className="py-4 text-xs font-semibold text-muted-foreground">{doc.type}</td>
                    <td className="py-4 text-foreground font-medium">{doc.subject}</td>
                    <td className="py-4 text-muted-foreground">{doc.date}</td>
                    <td className="py-4">
                      <span className={`px-2.5 py-1 text-xs rounded-full font-bold ${
                        doc.status === 'Chờ xử lý' 
                          ? 'bg-amber-100 text-amber-800 dark:bg-amber-950/20 dark:text-amber-400' 
                          : 'bg-emerald-100 text-emerald-800 dark:bg-emerald-950/20 dark:text-emerald-450'
                      }`}>
                        {doc.status}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>

      </main>
    </>
  );
}
