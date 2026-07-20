'use client';
import Header from '@/components/Header';
import Link from 'next/link';
import { useState, useEffect } from 'react';

export default function OutgoingDocs() {
  const [searchQuery, setSearchQuery] = useState('');
  const [statusFilter, setStatusFilter] = useState('Tất cả trạng thái');
  const [customDocs, setCustomDocs] = useState<any[]>([]);

  useEffect(() => {
    try {
      const saved = localStorage.getItem('custom_outgoing_docs');
      if (saved) {
        setCustomDocs(JSON.parse(saved));
      }
    } catch (e) {
      console.error(e);
    }
  }, []);

  const defaultDocs = [
    { id: '1', docNo: 'CV-DI-2026-00089', subject: 'Quyết định bổ nhiệm nhân sự phòng kế toán', receiver: 'Nội bộ / Phòng Kế toán', date: '18/07/2026', priority: 'Thường', status: 'Đã phát hành' },
    { id: '2', docNo: 'CV-DI-2026-00088', subject: 'Công văn trả lời v/v đề nghị cung cấp thông tin hạ tầng', receiver: 'VNPT', date: '12/07/2026', priority: 'Thường', status: 'Đã phát hành' },
  ];

  const docs = [...customDocs, ...defaultDocs];

  const filteredDocs = docs.filter((doc) => {
    const matchesSearch = 
      doc.docNo.toLowerCase().includes(searchQuery.toLowerCase()) ||
      doc.subject.toLowerCase().includes(searchQuery.toLowerCase()) ||
      doc.receiver.toLowerCase().includes(searchQuery.toLowerCase());
    
    const matchesStatus = statusFilter === 'Tất cả trạng thái' ? true : doc.status === statusFilter;

    return matchesSearch && matchesStatus;
  });

  return (
    <>
      <Header title="Danh sách công văn đi" />
      <main className="flex-1 p-8 space-y-6 max-w-7xl mx-auto w-full">
        
        {/* Controls */}
        <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 bg-zinc-950 p-4 border border-zinc-800 rounded-xl transition-all duration-200 shadow-sm">
          <div className="flex flex-wrap items-center gap-3">
            
            {/* Search input */}
            <div className="relative flex items-center w-full md:w-64">
              <input
                type="text"
                placeholder="Tìm kiếm công văn đi..."
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                className="w-full pl-9 pr-8 py-2 bg-black border border-zinc-800 rounded-lg text-xs focus:outline-none focus:border-zinc-500 text-white placeholder-zinc-500 font-medium transition-colors"
              />
              <span className="absolute left-3 text-zinc-500 pointer-events-none">
                <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
                  <path strokeLinecap="round" strokeLinejoin="round" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
                </svg>
              </span>
            </div>

            <select 
              value={statusFilter}
              onChange={(e) => setStatusFilter(e.target.value)}
              className="px-3 py-2 bg-black border border-zinc-800 rounded-lg text-xs font-semibold text-zinc-300 focus:outline-none"
            >
              <option value="Tất cả trạng thái">Tất cả trạng thái</option>
              <option value="Đang duyệt">Đang duyệt</option>
              <option value="Đã phát hành">Đã phát hành</option>
              <option value="Đã ký số & Phát hành">Đã ký số & Phát hành</option>
            </select>
          </div>
          
          <Link
            href="/documents/outgoing/new"
            className="px-4 py-2.5 bg-white text-black hover:bg-zinc-200 rounded-lg text-xs font-bold transition-all active:scale-[0.98] duration-150 flex items-center gap-2 self-start md:self-auto shadow-sm cursor-pointer"
          >
            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M12 4v16m8-8H4" />
            </svg>
            Tạo công văn đi
          </Link>
        </div>

        {/* Table */}
        <div className="bg-zinc-950/80 border border-zinc-800 rounded-2xl overflow-hidden shadow-sm transition-all duration-200">
          <div className="overflow-x-auto">
            <table className="w-full text-sm text-left">
              <thead>
                <tr className="border-b border-zinc-900 text-zinc-500 text-[10px] uppercase font-bold tracking-wider bg-black/40">
                  <th className="py-4 px-6">Số công văn</th>
                  <th className="py-4 px-6">Trích yếu</th>
                  <th className="py-4 px-6">Nơi nhận</th>
                  <th className="py-4 px-6">Ngày phát hành</th>
                  <th className="py-4 px-6">Độ khẩn</th>
                  <th className="py-4 px-6">Trạng thái</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-zinc-900">
                {filteredDocs.length > 0 ? (
                  filteredDocs.map((doc) => (
                    <tr key={doc.id} className="hover:bg-zinc-900/40 transition-colors">
                      <td className="py-4 px-6 font-mono font-bold text-white">{doc.docNo}</td>
                      <td className="py-4 px-6 text-zinc-200 font-medium hover:underline">
                        <Link href={`/documents/outgoing/${doc.id}`}>{doc.subject}</Link>
                      </td>
                      <td className="py-4 px-6 text-zinc-400 font-semibold">{doc.receiver}</td>
                      <td className="py-4 px-6 text-zinc-500 font-mono text-xs">{doc.date}</td>
                      <td className="py-4 px-6">
                        <span className="px-2.5 py-0.5 text-[10px] rounded-full bg-zinc-900 border border-zinc-800 text-zinc-300 font-mono font-bold uppercase tracking-wider">
                          {doc.priority}
                        </span>
                      </td>
                      <td className="py-4 px-6">
                        <span className={`inline-flex items-center gap-1.5 px-2.5 py-0.5 text-xs rounded-full font-semibold border ${
                          doc.status === 'Đang duyệt'
                            ? 'bg-amber-950/30 border-amber-800/40 text-amber-300'
                            : 'bg-emerald-950/30 border-emerald-800/40 text-emerald-300'
                        }`}>
                          <span className={`w-1.5 h-1.5 rounded-full ${doc.status === 'Đang duyệt' ? 'bg-amber-400' : 'bg-emerald-400'}`}></span>
                          {doc.status}
                        </span>
                      </td>
                    </tr>
                  ))
                ) : (
                  <tr>
                    <td colSpan={6} className="py-12 text-center text-xs text-zinc-500 font-medium">
                      Không tìm thấy công văn đi nào phù hợp.
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
