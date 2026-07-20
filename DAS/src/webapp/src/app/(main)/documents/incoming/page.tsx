'use client';
import Header from '@/components/Header';
import Link from 'next/link';
import { useState, useEffect } from 'react';

export default function IncomingDocs() {
  const [searchQuery, setSearchQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState('Tất cả trạng thái');
  const [priorityFilter, setPriorityFilter] = useState('Tất cả độ khẩn');
  const [customDocs, setCustomDocs] = useState<any[]>([]);

  useEffect(() => {
    try {
      const saved = localStorage.getItem('custom_incoming_docs');
      if (saved) {
        setCustomDocs(JSON.parse(saved));
      }
    } catch (e) {
      console.error(e);
    }
  }, []);

  const defaultDocs = [
    { id: '1', docNo: 'CV-DEN-2026-00157', subject: 'V/v hướng dẫn công tác báo cáo công văn lưu trữ quý II', sender: 'VNPT', date: '19/07/2026', priority: 'Mật', status: 'Chờ xử lý' },
    { id: '2', docNo: 'CV-DEN-2026-00156', subject: 'Hợp đồng dịch vụ bảo trì hạ tầng hệ thống máy chủ', sender: 'Vietcombank', date: '18/07/2026', priority: 'Thường', status: 'Đã phân phối' },
    { id: '3', docNo: 'CV-DEN-2026-00155', subject: 'Thông báo thanh tra về việc thực hiện thủ tục hành chính', sender: 'KSTTHC', date: '15/07/2026', priority: 'Hỏa tốc', status: 'Đã phân phối' },
  ];

  const docs = customDocs.length > 0 ? customDocs : defaultDocs;

  const filteredDocs = docs.filter((doc) => {
    const matchesSearch = 
      doc.docNo.toLowerCase().includes(searchQuery.toLowerCase()) ||
      doc.subject.toLowerCase().includes(searchQuery.toLowerCase()) ||
      doc.sender.toLowerCase().includes(searchQuery.toLowerCase());
    
    const matchesStatus = statusFilter === 'Tất cả trạng thái' ? true : doc.status === statusFilter;
    const matchesPriority = priorityFilter === 'Tất cả độ khẩn' ? true : doc.priority === priorityFilter;

    return matchesSearch && matchesStatus && matchesPriority;
  });

  return (
    <>
      <Header title="Danh sách công văn đến" />
      <main className="flex-1 p-8 space-y-6 max-w-7xl mx-auto w-full">
        
        {/* Controls Bar */}
        <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 bg-white dark:bg-zinc-950/80 p-4 border border-zinc-200 dark:border-zinc-800 rounded-xl transition-all duration-200 shadow-xs dark:shadow-sm">
          <div className="flex flex-wrap items-center gap-3">
            
            {/* Search Input */}
            <div className="relative flex items-center w-full md:w-64">
              <input
                type="text"
                placeholder="Tìm kiếm công văn đến..."
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                className="w-full pl-9 pr-8 py-2 bg-zinc-50 dark:bg-black border border-zinc-200 dark:border-zinc-800 rounded-lg text-xs focus:outline-none focus:border-zinc-400 dark:focus:border-zinc-500 text-zinc-900 dark:text-white placeholder-zinc-400 dark:placeholder-zinc-500 font-medium transition-colors"
              />
              <span className="absolute left-3 text-zinc-400 dark:text-zinc-500 pointer-events-none">
                <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
                  <path strokeLinecap="round" strokeLinejoin="round" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
                </svg>
              </span>
            </div>

            <select 
              value={statusFilter}
              onChange={(e) => setStatusFilter(e.target.value)}
              className="px-3 py-2 bg-zinc-50 dark:bg-black border border-zinc-200 dark:border-zinc-800 rounded-lg text-xs font-semibold text-zinc-700 dark:text-zinc-300 focus:outline-none"
            >
              <option value="Tất cả trạng thái">Tất cả trạng thái</option>
              <option value="Chờ xử lý">Chờ xử lý</option>
              <option value="Đã phân phối">Đã phân phối</option>
            </select>

            <select 
              value={priorityFilter}
              onChange={(e) => setPriorityFilter(e.target.value)}
              className="px-3 py-2 bg-zinc-50 dark:bg-black border border-zinc-200 dark:border-zinc-800 rounded-lg text-xs font-semibold text-zinc-700 dark:text-zinc-300 focus:outline-none"
            >
              <option value="Tất cả độ khẩn">Tất cả độ khẩn</option>
              <option value="Thường">Thường</option>
              <option value="Khẩn">Khẩn</option>
              <option value="Mật">Mật</option>
              <option value="Hỏa tốc">Hỏa tốc</option>
            </select>
          </div>
          
          <Link
            href="/documents/incoming/new"
            className="px-4 py-2 bg-zinc-900 dark:bg-white text-white dark:text-black hover:bg-zinc-800 dark:hover:bg-zinc-200 rounded-lg text-xs font-bold transition-all active:scale-95 duration-150 flex items-center gap-2 self-start md:self-auto shadow-xs cursor-pointer"
          >
            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M12 4v16m8-8H4" />
            </svg>
            Đăng ký công văn
          </Link>
        </div>

        {/* Table */}
        <div className="bg-white dark:bg-zinc-950/80 border border-zinc-200 dark:border-zinc-800 rounded-xl overflow-hidden shadow-xs dark:shadow-sm transition-all duration-200">
          <div className="overflow-x-auto">
            <table className="w-full text-sm text-left">
              <thead>
                <tr className="border-b border-zinc-100 dark:border-zinc-900 text-zinc-500 text-[10px] uppercase font-bold tracking-wider bg-zinc-50/50 dark:bg-black/40">
                  <th className="py-3.5 px-6">Số công văn</th>
                  <th className="py-3.5 px-6">Trích yếu</th>
                  <th className="py-3.5 px-6">Nơi gửi</th>
                  <th className="py-3.5 px-6">Ngày nhận</th>
                  <th className="py-3.5 px-6">Độ khẩn</th>
                  <th className="py-3.5 px-6">Trạng thái</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-zinc-100 dark:divide-zinc-900">
                {filteredDocs.length > 0 ? (
                  filteredDocs.map((doc) => (
                    <tr key={doc.id} className="hover:bg-zinc-50/80 dark:hover:bg-zinc-900/40 transition-colors">
                      <td className="py-4 px-6 font-mono font-bold text-zinc-900 dark:text-white">{doc.docNo}</td>
                      <td className="py-4 px-6 text-zinc-800 dark:text-zinc-200 font-medium hover:underline">
                        <Link href={`/documents/incoming/${doc.id}`}>{doc.subject}</Link>
                      </td>
                      <td className="py-4 px-6 text-zinc-500 dark:text-zinc-400 font-semibold">{doc.sender}</td>
                      <td className="py-4 px-6 text-zinc-500 font-mono text-xs">{doc.date}</td>
                      <td className="py-4 px-6">
                        <span className="px-2.5 py-0.5 text-[10px] rounded-full bg-zinc-100 dark:bg-zinc-900 border border-zinc-200 dark:border-zinc-800 text-zinc-700 dark:text-zinc-300 font-mono font-bold uppercase tracking-wider">
                          {doc.priority}
                        </span>
                      </td>
                      <td className="py-4 px-6">
                        <span className={`inline-flex items-center gap-1.5 px-2.5 py-0.5 text-xs rounded-full font-semibold border ${
                          doc.status === 'Chờ xử lý' 
                            ? 'bg-amber-50 border-amber-200 text-amber-800 dark:bg-amber-950/30 dark:border-amber-800/40 dark:text-amber-300' 
                            : 'bg-emerald-50 border-emerald-200 text-emerald-800 dark:bg-emerald-950/30 dark:border-emerald-800/40 dark:text-emerald-300'
                        }`}>
                          <span className={`w-1.5 h-1.5 rounded-full ${doc.status === 'Chờ xử lý' ? 'bg-amber-500' : 'bg-emerald-500'}`}></span>
                          {doc.status}
                        </span>
                      </td>
                    </tr>
                  ))
                ) : (
                  <tr>
                    <td colSpan={6} className="py-12 text-center text-xs text-zinc-500 font-medium">
                      Không tìm thấy công văn đến nào phù hợp.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </div>

      </main>
    </>
  );
}
