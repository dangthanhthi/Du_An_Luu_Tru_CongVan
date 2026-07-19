'use client';
import Header from '@/components/Header';
import Link from 'next/link';
import { useState } from 'react';

export default function Dashboard() {
  // Email Watcher config state
  const [watchEmail, setWatchEmail] = useState('congvan.den@das-enterprise.vn');
  const [editEmailModal, setEditEmailModal] = useState(false);
  const [editPassword, setEditPassword] = useState('');
  const [editNewEmail, setEditNewEmail] = useState('');
  const [editError, setEditError] = useState('');
  const [editSuccess, setEditSuccess] = useState(false);

  const handleSaveEmail = () => {
    setEditError('');
    if (editPassword !== '123456') {
      setEditError('Mật khẩu quản trị không đúng! (Mật khẩu thử nghiệm: 123456)');
      return;
    }
    if (!editNewEmail || !editNewEmail.includes('@')) {
      setEditError('Vui lòng nhập địa chỉ email hợp lệ.');
      return;
    }
    setWatchEmail(editNewEmail);
    setEditSuccess(true);
    setTimeout(() => {
      setEditEmailModal(false);
      setEditSuccess(false);
      setEditPassword('');
      setEditNewEmail('');
    }, 1200);
  };

  const stats = [
    { 
      name: 'Công văn đến', 
      value: '157', 
      trend: '+12% so với tháng trước',
      icon: (
        <svg className="w-4.5 h-4.5 text-emerald-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.75}>
          <path strokeLinecap="round" strokeLinejoin="round" d="M19 13h-6v6h-2v-6H5v-2h6V5h2v6h6v2z" />
        </svg>
      )
    },
    { 
      name: 'Công văn đi', 
      value: '89', 
      trend: '+5% so với tháng trước',
      icon: (
        <svg className="w-4.5 h-4.5 text-zinc-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.75}>
          <path strokeLinecap="round" strokeLinejoin="round" d="M12 19l9 2-9-18-9 18 9-2zm0 0v-8" />
        </svg>
      )
    },
    { 
      name: 'Công văn nội bộ', 
      value: '34', 
      trend: 'Ổn định',
      icon: (
        <svg className="w-4.5 h-4.5 text-zinc-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.75}>
          <path strokeLinecap="round" strokeLinejoin="round" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
        </svg>
      )
    },
    { 
      name: 'Chờ xử lý', 
      value: '18', 
      trend: 'Cần giải quyết sớm',
      icon: (
        <svg className="w-4.5 h-4.5 text-amber-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.75}>
          <path strokeLinecap="round" strokeLinejoin="round" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
        </svg>
      )
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
        
        {/* Action Bar */}
        <div className="flex items-center justify-between bg-zinc-950 border border-zinc-800 p-4 rounded-xl shadow-sm">
          <div className="flex items-center gap-3">
            <h2 className="text-base font-bold text-white tracking-tight">Tổng quan hệ thống</h2>
            <span className="px-2 py-0.5 bg-zinc-900 border border-zinc-800 text-[10px] font-mono font-bold text-emerald-400 rounded-full flex items-center gap-1.5">
              <span className="w-1.5 h-1.5 rounded-full bg-emerald-500 animate-pulse"></span>
              Live Sync
            </span>
          </div>

          <div className="flex items-center gap-2">
            <button className="px-3 py-1.5 bg-zinc-900 hover:bg-zinc-800 border border-zinc-800 text-white rounded-lg text-xs font-medium flex items-center gap-2 transition-colors cursor-pointer">
              <svg className="w-3.5 h-3.5 text-zinc-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M3 4a1 1 0 011-1h16a1 1 0 011 1v2.586a1 1 0 01-.293.707l-6.414 6.414a1 1 0 00-.293.707V17l-4 4v-6.586a1 1 0 00-.293-.707L3.293 7.293A1 1 0 013 6.586V4z" />
              </svg>
              Lọc thời gian
            </button>
          </div>
        </div>
        
        {/* Stat Cards */}
        <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
          {stats.map((s, idx) => (
            <div key={idx} className="p-5 bg-zinc-950/80 border border-zinc-800 hover:border-zinc-700 rounded-xl transition-all duration-200 flex flex-col justify-between shadow-sm">
              <div className="flex items-center justify-between">
                <p className="text-zinc-500 text-[10px] font-bold uppercase tracking-wider">{s.name}</p>
                <div className="w-8 h-8 rounded-lg bg-zinc-900 border border-zinc-800 flex items-center justify-center">
                  {s.icon}
                </div>
              </div>
              <div className="mt-4">
                <h3 className="text-3xl font-extrabold text-white tracking-tight">{s.value}</h3>
                <p className="text-[11px] text-zinc-400 font-medium mt-1">{s.trend}</p>
              </div>
            </div>
          ))}
        </div>

        {/* Charts & System health */}
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
          
          {/* Custom chart */}
          <div className="p-6 bg-zinc-950/80 border border-zinc-800 hover:border-zinc-700 rounded-xl lg:col-span-2 space-y-6 transition-all duration-200 shadow-sm">
            <div className="flex items-center justify-between">
              <h4 className="font-bold text-xs text-white uppercase tracking-wider">Thống kê lượng công văn 6 tháng gần nhất</h4>
              <span className="text-[10px] text-zinc-500 font-mono">Cập nhật: 18:00</span>
            </div>
            
            <div className="h-64 flex items-end justify-between px-4 pt-8 border-b border-zinc-900 pb-4">
              {[
                { month: 'T2', val: '75%', val2: '40%' },
                { month: 'T3', val: '85%', val2: '50%' },
                { month: 'T4', val: '60%', val2: '35%' },
                { month: 'T5', val: '95%', val2: '65%' },
                { month: 'T6', val: '70%', val2: '45%' },
                { month: 'T7', val: '110%', val2: '75%' }
              ].map((bar, idx) => (
                <div key={idx} className="flex flex-col items-center gap-2 w-1/6">
                  <div className="w-full flex justify-center gap-1.5 h-44 items-end">
                    <div className="w-3.5 bg-white rounded-t transition-all duration-300 hover:bg-zinc-200" style={{ height: bar.val }} title="Công văn đến"></div>
                    <div className="w-3.5 bg-emerald-500 rounded-t transition-all duration-300 hover:bg-emerald-400" style={{ height: bar.val2 }} title="Công văn đi"></div>
                  </div>
                  <span className="text-[11px] text-zinc-400 font-bold mt-2">{bar.month}</span>
                </div>
              ))}
            </div>
            
            <div className="flex justify-center gap-6 text-[11px] font-medium text-zinc-400">
              <span className="flex items-center gap-2"><span className="w-2.5 h-2.5 bg-white rounded-sm"></span>Công văn đến</span>
              <span className="flex items-center gap-2"><span className="w-2.5 h-2.5 bg-emerald-500 rounded-sm"></span>Công văn đi</span>
            </div>
          </div>

          {/* System Health Check & Email Watcher */}
          <div className="p-6 bg-zinc-950/80 border border-zinc-800 hover:border-zinc-700 rounded-xl space-y-6 transition-all duration-200 shadow-sm">
            <h4 className="font-bold text-xs text-white uppercase tracking-wider">Giám sát tài nguyên hệ thống</h4>
            
            <div className="space-y-5 text-xs font-semibold">
              <div>
                <div className="flex justify-between mb-1.5">
                  <span className="text-zinc-400">Dung lượng Database (das.db)</span>
                  <span className="font-bold text-white">14.2 MB / 500 MB</span>
                </div>
                <div className="w-full h-1.5 bg-zinc-900 rounded-full overflow-hidden border border-zinc-800">
                  <div className="h-full bg-emerald-500 rounded-full" style={{ width: '3%' }}></div>
                </div>
              </div>

              <div>
                <div className="flex justify-between mb-1.5">
                  <span className="text-zinc-400">Sử dụng CPU (API Service)</span>
                  <span className="font-bold text-white">12%</span>
                </div>
                <div className="w-full h-1.5 bg-zinc-900 rounded-full overflow-hidden border border-zinc-800">
                  <div className="h-full bg-emerald-500 rounded-full" style={{ width: '12%' }}></div>
                </div>
              </div>

              <div>
                <div className="flex justify-between mb-1.5">
                  <span className="text-zinc-400">Sử dụng RAM (API Service)</span>
                  <span className="font-bold text-white">158 MB / 1024 MB</span>
                </div>
                <div className="w-full h-1.5 bg-zinc-900 rounded-full overflow-hidden border border-zinc-800">
                  <div className="h-full bg-emerald-500 rounded-full" style={{ width: '15%' }}></div>
                </div>
              </div>

              {/* Email Watcher Section - shows which email is being scanned */}
              <div className="pt-4 border-t border-zinc-900 space-y-3">
                <div className="flex justify-between items-center">
                  <span className="text-zinc-400 font-medium">Tình trạng Email Watcher:</span>
                  <span className="inline-flex items-center gap-1.5 px-2 py-0.5 bg-emerald-950/40 border border-emerald-800/50 text-emerald-400 font-bold text-[10px] rounded-full">
                    <span className="w-1.5 h-1.5 rounded-full bg-emerald-500 animate-pulse"></span>
                    Hoạt động
                  </span>
                </div>

                <div className="flex justify-between items-center">
                  <span className="text-zinc-400 font-medium">Quét email từ:</span>
                  <span className="font-mono text-emerald-400 text-[10px] truncate max-w-[140px]" title={watchEmail}>{watchEmail}</span>
                </div>

                <div className="flex justify-between items-center">
                  <span className="text-zinc-400 font-medium">Lần quét cuối:</span>
                  <span className="font-mono text-zinc-300 text-[11px]">11:00 (15 phút trước)</span>
                </div>

                <button
                  onClick={() => {
                    setEditNewEmail(watchEmail);
                    setEditPassword('');
                    setEditError('');
                    setEditSuccess(false);
                    setEditEmailModal(true);
                  }}
                  className="w-full mt-1 py-2 bg-zinc-900 hover:bg-zinc-800 border border-zinc-800 hover:border-zinc-700 rounded-lg text-zinc-400 hover:text-white text-[11px] font-bold transition-colors cursor-pointer flex items-center justify-center gap-2"
                >
                  <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                    <path strokeLinecap="round" strokeLinejoin="round" d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
                  </svg>
                  Chỉnh sửa Email Watcher
                </button>
              </div>
            </div>
          </div>
        </div>

        {/* Recent Documents Table */}
        <div className="p-6 bg-zinc-950/80 border border-zinc-800 hover:border-zinc-700 rounded-xl space-y-4 transition-all duration-200 shadow-sm">
          <div className="flex justify-between items-center border-b border-zinc-900 pb-4">
            <h4 className="font-bold text-xs text-white uppercase tracking-wider">Công văn mới cập nhật</h4>
            <Link href="/documents/incoming" className="text-xs font-semibold text-zinc-400 hover:text-white hover:underline transition-colors">Xem tất cả →</Link>
          </div>
          
          <div className="overflow-x-auto">
            <table className="w-full text-sm text-left">
              <thead>
                <tr className="border-b border-zinc-900 text-zinc-500 text-[10px] uppercase font-bold tracking-wider bg-black/40">
                  <th className="py-3.5 px-2">Số công văn</th>
                  <th className="py-3.5 px-2">Loại</th>
                  <th className="py-3.5 px-2">Trích yếu</th>
                  <th className="py-3.5 px-2">Ngày</th>
                  <th className="py-3.5 px-2">Trạng thái</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-zinc-900">
                {recentDocs.map((doc) => (
                  <tr key={doc.id} className="hover:bg-zinc-900/40 transition-colors">
                    <td className="py-3.5 px-2 font-mono font-bold text-white hover:underline">
                      <Link href={`/documents/incoming/${doc.id}`}>{doc.docNo}</Link>
                    </td>
                    <td className="py-3.5 px-2 text-xs font-medium text-zinc-400">{doc.type}</td>
                    <td className="py-3.5 px-2 text-zinc-200 font-medium">{doc.subject}</td>
                    <td className="py-3.5 px-2 text-zinc-500 font-mono text-xs">{doc.date}</td>
                    <td className="py-3.5 px-2">
                      <span className={`inline-flex items-center gap-1.5 px-2.5 py-0.5 text-xs rounded-full font-semibold border ${
                        doc.status === 'Chờ xử lý' 
                          ? 'bg-amber-950/30 border-amber-800/40 text-amber-300' 
                          : 'bg-emerald-950/30 border-emerald-800/40 text-emerald-300'
                      }`}>
                        <span className={`w-1.5 h-1.5 rounded-full ${doc.status === 'Chờ xử lý' ? 'bg-amber-400' : 'bg-emerald-400'}`}></span>
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

      {/* Edit Email Watcher Modal - Password Protected */}
      {editEmailModal && (
        <div className="fixed inset-0 bg-black/80 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-zinc-950 border border-zinc-800 w-full max-w-md rounded-2xl shadow-2xl overflow-hidden p-6 space-y-5">
            <div className="text-center space-y-2">
              <div className="w-12 h-12 rounded-xl bg-zinc-900 border border-zinc-800 text-zinc-300 flex items-center justify-center mx-auto">
                <svg className="w-6 h-6" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.75}>
                  <path strokeLinecap="round" strokeLinejoin="round" d="M3 8l7.89 5.26a2 2 0 002.22 0L21 8M5 19h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z" />
                </svg>
              </div>
              <h3 className="font-extrabold text-white text-base">Chỉnh sửa Email Watcher</h3>
              <p className="text-xs text-zinc-400">Thay đổi địa chỉ email mà hệ thống tự động quét để tiếp nhận công văn đến.</p>
            </div>

            {editSuccess ? (
              <div className="p-4 bg-emerald-950/40 border border-emerald-800/50 rounded-xl text-center space-y-2">
                <span className="text-2xl">✓</span>
                <p className="text-emerald-400 font-bold text-sm">Đã cập nhật thành công!</p>
                <p className="text-[11px] text-zinc-400">Email Watcher sẽ quét từ địa chỉ mới trong lần quét tiếp theo.</p>
              </div>
            ) : (
              <div className="space-y-4">
                <div>
                  <label className="block text-[10px] font-bold text-zinc-500 mb-1.5 uppercase tracking-wide">Email hiện tại đang quét</label>
                  <div className="px-3 py-2 border border-zinc-800 bg-zinc-900/50 rounded-lg text-xs text-emerald-400 font-mono">
                    {watchEmail}
                  </div>
                </div>

                <div>
                  <label className="block text-[10px] font-bold text-zinc-500 mb-1.5 uppercase tracking-wide">Email mới</label>
                  <input
                    type="email"
                    value={editNewEmail}
                    onChange={(e) => setEditNewEmail(e.target.value)}
                    placeholder="example@company.vn"
                    className="w-full px-3 py-2 border border-zinc-800 bg-zinc-900 rounded-lg text-xs text-white font-medium focus:outline-none focus:border-zinc-500"
                  />
                </div>

                <div>
                  <label className="block text-[10px] font-bold text-zinc-500 mb-1.5 uppercase tracking-wide">
                    Mật khẩu quản trị viên (bắt buộc)
                  </label>
                  <input
                    type="password"
                    value={editPassword}
                    onChange={(e) => setEditPassword(e.target.value)}
                    placeholder="Nhập mật khẩu để xác nhận thay đổi"
                    className="w-full px-3 py-2 border border-zinc-800 bg-zinc-900 rounded-lg text-xs text-white font-medium focus:outline-none focus:border-zinc-500"
                  />
                  <p className="text-[10px] text-zinc-600 mt-1">Mật khẩu thử nghiệm: 123456</p>
                </div>

                {editError && (
                  <div className="p-2.5 bg-red-950/40 border border-red-800/50 rounded-lg text-xs text-red-400 font-semibold">
                    {editError}
                  </div>
                )}
              </div>
            )}

            {!editSuccess && (
              <div className="pt-2 flex justify-end gap-3 text-xs font-bold">
                <button
                  onClick={() => setEditEmailModal(false)}
                  className="px-4 py-2 border border-zinc-800 bg-zinc-900 text-zinc-300 hover:text-white rounded-lg cursor-pointer"
                >
                  Hủy bỏ
                </button>
                <button
                  onClick={handleSaveEmail}
                  className="px-5 py-2 bg-white text-black hover:bg-zinc-200 rounded-lg cursor-pointer shadow-md font-bold"
                >
                  Xác nhận thay đổi
                </button>
              </div>
            )}
          </div>
        </div>
      )}
    </>
  );
}
