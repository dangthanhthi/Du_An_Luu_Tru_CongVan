'use client';
import Header from '@/components/Header';
import React, { useState } from 'react';
import { useRouter } from 'next/navigation';

export default function NewInternalDoc() {
  const router = useRouter();
  const [subject, setSubject] = useState('');
  const [content, setContent] = useState('');
  const [priority, setPriority] = useState('Normal');
  const [department, setDepartment] = useState('Toàn thể công ty');
  const [fileName, setFileName] = useState('');
  
  // AI Draft State
  const [generating, setGenerating] = useState(false);
  const [generatedSuccess, setGeneratedSuccess] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [submitSuccess, setSubmitSuccess] = useState(false);

  const handleFileUpload = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      setFileName(file.name);
    }
  };

  const runAiDraft = () => {
    setGenerating(true);
    setGeneratedSuccess(false);

    setTimeout(() => {
      setSubject('Quy chế làm việc từ xa và bảo mật thông tin tài liệu lưu trữ nội bộ năm 2026');
      setDepartment('Phòng Kế toán & Ban Giám đốc');
      setPriority('Urgent');
      setContent('Kính gửi Toàn thể Cán bộ Nhân viên,\n\nBan Giám đốc xin thông báo áp dụng Quy chế làm việc từ xa kết hợp bảo mật thông tin văn bản công văn. Tất cả cán bộ nhân viên có trách nhiệm tuân thủ việc số hóa và ký số theo Nghị định 30/2020/NĐ-CP.\n\nChi tiết đính kèm.');
      setFileName('Quy_che_Bao_mat_Noi_bo_2026.pdf');
      setGenerating(false);
      setGeneratedSuccess(true);
    }, 1000);
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setSubmitting(true);

    const now = new Date();
    const dateStr = now.toLocaleDateString('vi-VN');
    
    // Generate unique ID & Code
    const docId = `doc-${Date.now()}`;
    const newDoc = {
      id: docId,
      docNo: `CV-NB-2026-000${Math.floor(35 + Math.random() * 10)}`,
      subject: subject,
      department: department,
      date: dateStr,
      priority: priority === 'Urgent' ? 'Khẩn' : priority === 'Secret' ? 'Mật' : priority === 'TopSecret' ? 'Hỏa tốc' : 'Thường',
      status: 'Đã phân phối',
      content: content,
      fileName: fileName || 'Internal_Doc.pdf'
    };

    // Save to localStorage
    try {
      const existing = localStorage.getItem('custom_internal_docs');
      const list = existing ? JSON.parse(existing) : [];
      list.unshift(newDoc);
      localStorage.setItem('custom_internal_docs', JSON.stringify(list));
    } catch (err) {
      console.error(err);
    }

    setTimeout(() => {
      setSubmitting(false);
      setSubmitSuccess(true);
      setTimeout(() => {
        router.push('/documents/internal');
      }, 1000);
    }, 600);
  };

  return (
    <>
      <Header title="Tạo công văn nội bộ" />
      <main className="flex-1 p-8 max-w-4xl mx-auto w-full space-y-6">
        
        {/* AI Assistant Banner */}
        <div className="p-6 bg-zinc-950 border border-zinc-800 rounded-2xl space-y-4 shadow-sm relative overflow-hidden">
          <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 rounded-xl bg-blue-950/60 border border-blue-800/60 text-blue-400 flex items-center justify-center text-lg">
                📝
              </div>
              <div>
                <h3 className="font-bold text-sm text-white">AI Soạn thảo Thông báo / Công văn Nội bộ</h3>
                <p className="text-xs text-zinc-400">Khởi tạo nhanh quy chế, thông báo nội bộ và quyết định chỉ đạo nội bộ doanh nghiệp.</p>
              </div>
            </div>

            <button
              type="button"
              onClick={runAiDraft}
              disabled={generating}
              className="px-4 py-2 bg-blue-600 hover:bg-blue-500 text-white rounded-xl text-xs font-bold transition-all cursor-pointer shadow-md flex items-center gap-2 shrink-0"
            >
              {generating ? '⚡ Đang tạo...' : '⚡ Sinh mẫu công văn nội bộ AI'}
            </button>
          </div>

          {generating && (
            <div className="p-3 bg-blue-950/30 border border-blue-800/40 rounded-xl text-xs text-blue-300 flex items-center gap-3 animate-pulse">
              <span className="w-4 h-4 rounded-full border-2 border-blue-400 border-t-transparent animate-spin"></span>
              <span>Đang xây dựng thể thức và trích yếu công văn nội bộ...</span>
            </div>
          )}

          {generatedSuccess && (
            <div className="p-3 bg-emerald-950/40 border border-emerald-800/50 rounded-xl text-xs text-emerald-400 font-bold flex items-center justify-between">
              <span className="flex items-center gap-2">
                ✓ Đã sinh công văn nội bộ thành công! Bạn có thể chỉnh sửa lại bên dưới.
              </span>
            </div>
          )}
        </div>

        {/* Success toast */}
        {submitSuccess && (
          <div className="p-4 bg-emerald-950/50 border border-emerald-800 rounded-xl text-emerald-400 text-xs font-bold flex items-center gap-2 animate-fade-in">
            <span className="text-base">✓</span>
            <span>Đã tạo công văn nội bộ thành công! Đang chuyển đến danh sách...</span>
          </div>
        )}

        {/* Create Form */}
        <form onSubmit={handleSubmit} className="p-8 bg-zinc-950 border border-zinc-800 rounded-2xl space-y-6 shadow-sm">
          
          <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
            
            <div className="md:col-span-2">
              <label className="block text-xs font-bold text-zinc-400 mb-2 uppercase tracking-wide">Trích yếu nội dung công văn nội bộ *</label>
              <input
                type="text"
                value={subject}
                onChange={(e) => setSubject(e.target.value)}
                required
                placeholder="Ví dụ: Quy chế làm việc từ xa và bảo mật thông tin nội bộ..."
                className="w-full px-4 py-2.5 border border-zinc-800 bg-zinc-900 rounded-xl text-xs focus:outline-none focus:border-zinc-500 text-white font-medium"
              />
            </div>

            <div>
              <label className="block text-xs font-bold text-zinc-400 mb-2 uppercase tracking-wide">Phòng ban nhận / Áp dụng</label>
              <input
                type="text"
                value={department}
                onChange={(e) => setDepartment(e.target.value)}
                required
                placeholder="Ví dụ: Toàn thể công ty / Phòng Kế toán..."
                className="w-full px-4 py-2.5 border border-zinc-800 bg-zinc-900 rounded-xl text-xs focus:outline-none focus:border-zinc-500 text-white font-medium"
              />
            </div>

            <div>
              <label className="block text-xs font-bold text-zinc-400 mb-2 uppercase tracking-wide">Độ khẩn / Bảo mật</label>
              <select
                value={priority}
                onChange={(e) => setPriority(e.target.value)}
                className="w-full px-4 py-2.5 border border-zinc-800 bg-zinc-900 rounded-xl text-xs focus:outline-none focus:border-zinc-500 text-white font-medium"
              >
                <option value="Normal">Thường</option>
                <option value="Urgent">Khẩn</option>
                <option value="Secret">Mật</option>
                <option value="TopSecret">Hỏa tốc / Tối mật</option>
              </select>
            </div>

            <div className="md:col-span-2">
              <label className="block text-xs font-bold text-zinc-400 mb-2 uppercase tracking-wide">Bản thảo PDF đính kèm</label>
              <input
                type="file"
                accept="application/pdf"
                onChange={handleFileUpload}
                className="w-full text-xs text-zinc-400 file:mr-4 file:py-2 file:px-4 file:rounded-lg file:border file:border-zinc-800 file:text-xs file:font-semibold file:bg-zinc-900 file:text-white hover:file:bg-zinc-800 cursor-pointer"
              />
              {fileName && (
                <p className="text-[11px] text-emerald-400 font-mono mt-1.5 flex items-center gap-1.5">
                  <span>📎 Tệp đính kèm:</span>
                  <span className="underline">{fileName}</span>
                </p>
              )}
            </div>

            <div className="md:col-span-2">
              <label className="block text-xs font-bold text-zinc-400 mb-2 uppercase tracking-wide">Nội dung chi tiết *</label>
              <textarea
                rows={5}
                value={content}
                onChange={(e) => setContent(e.target.value)}
                required
                placeholder="Nhập ghi chú tóm tắt nội dung chính..."
                className="w-full px-4 py-2.5 border border-zinc-800 bg-zinc-900 rounded-xl text-xs focus:outline-none focus:border-zinc-500 text-white leading-relaxed font-sans"
              />
            </div>

          </div>

          <div className="flex justify-end gap-3 border-t border-zinc-800 pt-6">
            <button
              type="button"
              onClick={() => router.push('/documents/internal')}
              className="px-5 py-2.5 border border-zinc-800 text-zinc-300 hover:text-white rounded-xl text-xs font-bold transition-colors cursor-pointer"
            >
              Hủy
            </button>
            <button
              type="submit"
              disabled={submitting || submitSuccess}
              className="px-5 py-2.5 bg-white text-black hover:bg-zinc-200 rounded-xl text-xs font-bold transition-colors shadow-sm cursor-pointer disabled:opacity-50 flex items-center gap-2"
            >
              {submitting ? (
                <>
                  <span className="w-3.5 h-3.5 rounded-full border-2 border-black border-t-transparent animate-spin"></span>
                  <span>Đang đăng ký...</span>
                </>
              ) : (
                <span>Đăng ký công văn nội bộ</span>
              )}
            </button>
          </div>
        </form>

      </main>
    </>
  );
}
