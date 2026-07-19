'use client';
import Header from '@/components/Header';
import Link from 'next/link';
import { useState } from 'react';

export default function IncomingDocs() {
  const [searchQuery, setSearchQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState('Tất cả trạng thái');
  const [priorityFilter, setPriorityFilter] = useState('Tất cả độ khẩn');

  const docs = [
    { id: '1', docNo: 'CV-DEN-2026-00157', subject: 'V/v hướng dẫn công tác báo cáo công văn lưu trữ quý II', sender: 'VNPT', date: '19/07/2026', priority: 'Mật', status: 'Chờ xử lý' },
    { id: '2', docNo: 'CV-DEN-2026-00156', subject: 'Hợp đồng dịch vụ bảo trì hạ tầng hệ thống máy chủ', sender: 'Vietcombank', date: '18/07/2026', priority: 'Thường', status: 'Đã phân phối' },
    { id: '3', docNo: 'CV-DEN-2026-00155', subject: 'Thông báo thanh tra về việc thực hiện thủ tục hành chính', sender: 'KSTTHC', date: '15/07/2026', priority: 'Hỏa tốc', status: 'Đã phân phối' },
  ];

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
        
        {/* Controls */}
        <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 bg-card p-4 border border-border rounded-xl transition-all duration-200">
          <div className="flex flex-wrap items-center gap-4">
            
            {/* Unsplash style search bar */}
            <div className="relative flex items-center w-full md:w-64">
              <input
                type="text"
                placeholder="Tìm kiếm công văn đến..."
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                className="w-full pl-9 pr-9 py-2 border border-border bg-muted rounded-full text-xs focus:outline-none focus:border-accent focus:bg-card text-foreground transition-all duration-200 font-semibold"
              />
              <span className="absolute left-3 text-muted-foreground pointer-events-none">
                <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
                  <path strokeLinecap="round" strokeLinejoin="round" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
                </svg>
              </span>
              <span className="absolute right-3 text-muted-foreground hover:text-foreground cursor-pointer transition-colors" title="Tìm kiếm OCR">
                <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
                  <path strokeLinecap="round" strokeLinejoin="round" d="M3 9V6a3 3 0 013-3h3m6 0h3a3 3 0 013 3v3m0 6v3a3 3 0 01-3 3h-3m-6 0H6a3 3 0 01-3-3v-3" />
                  <circle cx="12" cy="12" r="3.2" />
                </svg>
              </span>
            </div>

            <select 
              value={statusFilter}
              onChange={(e) => setStatusFilter(e.target.value)}
              className="px-3 py-2 border border-border bg-muted rounded-lg text-xs font-semibold text-foreground focus:outline-none"
            >
              <option value="Tất cả trạng thái">Tất cả trạng thái</option>
              <option value="Chờ xử lý">Chờ xử lý</option>
              <option value="Đã phân phối">Đã phân phối</option>
            </select>

            <select 
              value={priorityFilter}
              onChange={(e) => setPriorityFilter(e.target.value)}
              className="px-3 py-2 border border-border bg-muted rounded-lg text-xs font-semibold text-foreground focus:outline-none"
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
            className="px-4 py-2.5 bg-white text-black hover:bg-zinc-200 rounded-lg text-sm font-semibold transition-all active:scale-[0.98] duration-150 flex items-center gap-2 self-start md:self-auto shadow-sm cursor-pointer"
          >
            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M12 4v16m8-8H4" />
            </svg>
            Đăng ký công văn
          </Link>
        </div>

        {/* Table */}
        <div className="bg-card border border-border rounded-2xl overflow-hidden shadow-[0_4px_20px_-4px_rgba(0,0,0,0.02)] transition-all duration-200">
          <div className="overflow-x-auto">
            <table className="w-full text-sm text-left">
              <thead>
                <tr className="border-b border-border text-muted-foreground text-[10px] uppercase font-bold tracking-wider bg-muted/50">
                  <th className="py-4 px-6">Số công văn</th>
                  <th className="py-4 px-6">Trích yếu</th>
                  <th className="py-4 px-6">Nơi gửi</th>
                  <th className="py-4 px-6">Ngày nhận</th>
                  <th className="py-4 px-6">Độ khẩn</th>
                  <th className="py-4 px-6">Trạng thái</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {filteredDocs.length > 0 ? (
                  filteredDocs.map((doc) => (
                    <tr key={doc.id} className="hover:bg-muted/40">
                      <td className="py-4 px-6 font-bold text-foreground">{doc.docNo}</td>
                      <td className="py-4 px-6 text-foreground font-medium hover:underline">
                        <Link href={`/documents/incoming/${doc.id}`}>{doc.subject}</Link>
                      </td>
                      <td className="py-4 px-6 text-muted-foreground font-semibold">{doc.sender}</td>
                      <td className="py-4 px-6 text-muted-foreground">{doc.date}</td>
                      <td className="py-4 px-6">
                        <span className="px-2.5 py-1 text-[10px] rounded-full bg-muted text-foreground font-bold uppercase tracking-wider">
                          {doc.priority}
                        </span>
                      </td>
                      <td className="py-4 px-6">
                        <span className={`px-2.5 py-1 text-xs rounded-full font-semibold ${
                          doc.status === 'Chờ xử lý' 
                            ? 'bg-amber-100 text-amber-800 dark:bg-amber-950/20 dark:text-amber-450' 
                            : 'bg-emerald-100 text-emerald-800 dark:bg-emerald-950/20 dark:text-emerald-450'
                        }`}>
                          {doc.status}
                        </span>
                      </td>
                    </tr>
                  ))
                ) : (
                  <tr>
                    <td colSpan={6} className="py-12 text-center text-sm text-muted-foreground font-medium">
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
