'use client';
import Header from '@/components/Header';
import { useState } from 'react';

export default function Settings() {
  const [incFormat, setIncFormat] = useState('CV-DEN-{YYYY}-{NUMBER}');
  const [outFormat, setOutFormat] = useState('CV-DI-{YYYY}-{NUMBER}');
  const [activeTab, setActiveTab] = useState<'config' | 'notify' | 'logs'>('config');

  const logs = [
    { time: '11:00:05', status: 'Success', msg: 'Kết nối IMAP server thành công. Tìm thấy 0 bản fax mới.' },
    { time: '10:00:12', status: 'Success', msg: 'Tìm thấy 1 email mới. Tải tệp CV_DEN_2026_00157.pdf (1.2 MB).' },
    { time: '10:00:15', status: 'Success', msg: 'AI OCR: Tự động trích xuất thông tin đối tác VNPT thành công.' },
    { time: '09:00:03', status: 'Success', msg: 'Kết nối IMAP server thành công. Tìm thấy 0 bản fax mới.' }
  ];

  return (
    <>
      <Header title="Cài đặt hệ thống" />
      <main className="flex-1 p-8 max-w-4xl mx-auto w-full space-y-6">
        
        {/* Settings Tab menu */}
        <div className="flex border-b border-border gap-6 text-sm font-semibold select-none">
          {[
            { id: 'config', name: 'Cấu hình tham số' },
            { id: 'notify', name: 'Ma trận Thông báo' },
            { id: 'logs', name: 'Nhật ký quét fax tự động' }
          ].map((t) => (
            <button
              key={t.id}
              onClick={() => setActiveTab(t.id as any)}
              className={`pb-3 transition-colors cursor-pointer ${
                activeTab === t.id 
                  ? 'border-b-2 border-emerald-500 text-emerald-600 dark:text-emerald-400 font-bold' 
                  : 'text-muted-foreground hover:text-muted-foreground'
              }`}
            >
              {t.name}
            </button>
          ))}
        </div>

        {/* TAB 1: GENERAL CONFIG */}
        {activeTab === 'config' && (
          <div className="bg-card border border-border rounded-2xl shadow-sm overflow-hidden">
            <div className="p-6 border-b border-border">
              <h3 className="font-bold text-base text-foreground">Cấu hình định dạng mã số</h3>
              <p className="text-xs text-muted-foreground mt-1 font-semibold">Thiết lập định dạng sinh tự động số công văn trên hệ thống</p>
            </div>

            <div className="p-6 space-y-6">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                <div>
                  <label className="block text-xs font-bold text-muted-foreground mb-2 uppercase tracking-wide">Định dạng Công văn đến</label>
                  <input
                    type="text"
                    value={incFormat}
                    onChange={(e) => setIncFormat(e.target.value)}
                    className="w-full px-4 py-2 border border-border bg-muted rounded-xl text-sm focus:outline-none focus:border-emerald-500"
                  />
                </div>
                <div>
                  <label className="block text-xs font-bold text-muted-foreground mb-2 uppercase tracking-wide">Định dạng Công văn đi</label>
                  <input
                    type="text"
                    value={outFormat}
                    onChange={(e) => setOutFormat(e.target.value)}
                    className="w-full px-4 py-2 border border-border bg-muted rounded-xl text-sm focus:outline-none focus:border-emerald-500"
                  />
                </div>
              </div>
            </div>

            <div className="p-4 bg-muted/60 border-t border-border flex justify-end">
              <button className="px-5 py-2 bg-primary text-primary-foreground hover:opacity-90 font-bold rounded-xl text-xs transition-colors cursor-pointer">
                Lưu cấu hình
              </button>
            </div>
          </div>
        )}

        {/* TAB 2: NOTIFICATION MATRIX */}
        {activeTab === 'notify' && (
          <div className="bg-card border border-border rounded-2xl shadow-sm overflow-hidden">
            <div className="p-6 border-b border-border">
              <h3 className="font-bold text-base text-foreground">Ma trận cấu hình nhận thông báo</h3>
              <p className="text-xs text-muted-foreground mt-1 font-semibold">Tùy chỉnh kênh nhận thông báo cho các vai trò và sự kiện chính</p>
            </div>

            <div className="p-6 overflow-x-auto text-xs font-semibold">
              <table className="w-full text-left">
                <thead>
                  <tr className="border-b border-border text-muted-foreground">
                    <th className="py-3">Loại sự kiện</th>
                    <th className="py-3 text-center">Thông báo Web</th>
                    <th className="py-3 text-center">Email</th>
                    <th className="py-3 text-center">SMS</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border/65 font-medium text-foreground">
                  {[
                    { event: 'Có công văn đến mới (AI quét)', web: true, email: true, sms: false },
                    { event: 'Có yêu cầu duyệt trình ký công văn đi', web: true, email: true, sms: true },
                    { event: 'Bình luận / Chỉ đạo mới trên công văn', web: true, email: false, sms: false },
                    { event: 'Tải lên phiên bản mới của văn bản', web: true, email: true, sms: false }
                  ].map((row, idx) => (
                    <tr key={idx}>
                      <td className="py-4 font-bold text-foreground">{row.event}</td>
                      <td className="py-4 text-center">
                        <input type="checkbox" defaultChecked={row.web} className="w-4 h-4 rounded accent-emerald-600 cursor-pointer" />
                      </td>
                      <td className="py-4 text-center">
                        <input type="checkbox" defaultChecked={row.email} className="w-4 h-4 rounded accent-emerald-600 cursor-pointer" />
                      </td>
                      <td className="py-4 text-center">
                        <input type="checkbox" defaultChecked={row.sms} className="w-4 h-4 rounded accent-emerald-600 cursor-pointer" />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        )}

        {/* TAB 3: IMAP FAX MONITOR LOGS */}
        {activeTab === 'logs' && (
          <div className="bg-card border border-border rounded-2xl shadow-sm overflow-hidden">
            <div className="p-6 border-b border-border flex justify-between items-center">
              <div>
                <h3 className="font-bold text-base text-foreground">Nhật ký hoạt động Fax Watcher</h3>
                <p className="text-xs text-muted-foreground mt-1 font-semibold">Theo dõi tiến trình chạy nền quét email từ máy Fax tự động (Tần suất 1 giờ/lần)</p>
              </div>
              <span className="w-2.5 h-2.5 rounded-full bg-emerald-500 animate-ping"></span>
            </div>

            <div className="p-6 space-y-4 font-mono text-[11px] max-h-96 overflow-y-auto bg-zinc-950 text-emerald-400 p-4 rounded-b-2xl">
              {logs.map((log, i) => (
                <div key={i} className="flex gap-4">
                  <span className="text-muted-foreground">[{log.time}]</span>
                  <span className="text-white">[{log.status}]</span>
                  <span className="text-emerald-350">{log.msg}</span>
                </div>
              ))}
            </div>
          </div>
        )}

      </main>
    </>
  );
}
