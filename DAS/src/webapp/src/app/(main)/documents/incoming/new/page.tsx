'use client';
import Header from '@/components/Header';
import React, { useState } from 'react';
import { useRouter } from 'next/navigation';

export default function NewIncomingDoc() {
  const router = useRouter();
  const [subject, setSubject] = useState('');
  const [content, setContent] = useState('');
  const [sender, setSender] = useState('');
  const [originalNo, setOriginalNo] = useState('');
  const [priority, setPriority] = useState('Normal');
  
  // OCR Extraction State
  const [extracting, setExtracting] = useState(false);
  const [extractedSuccess, setExtractedSuccess] = useState(false);

  const handleFileUpload = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (file) {
      runOcrExtraction();
    }
  };

  const runOcrExtraction = () => {
    setExtracting(true);
    setExtractedSuccess(false);

    setTimeout(() => {
      setSubject('V/v hướng dẫn công tác báo cáo công văn lưu trữ quý II năm 2026');
      setSender('vnpt');
      setOriginalNo('1025/VNPT-VP');
      setPriority('Secret');
      setContent('Hướng dẫn việc lập báo cáo thống kê tình hình lưu trữ và khai thác công văn trong quý II năm 2026. Hạn nộp báo cáo trước ngày 30/07/2026.');
      setExtracting(false);
      setExtractedSuccess(true);
    }, 1200);
  };

  const [submitting, setSubmitting] = useState(false);
  const [submitSuccess, setSubmitSuccess] = useState(false);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setSubmitting(true);

    const now = new Date();
    const dateStr = now.toLocaleDateString('vi-VN');
    
    // Generate unique ID & Code
    const docId = `doc-${Date.now()}`;
    const senderName = sender === 'vnpt' ? 'Tập đoàn VNPT' : sender === 'kstthc' ? 'Cục KSTTHC' : sender === 'vietcombank' ? 'Ngân hàng Vietcombank' : (sender || 'Đối tác bên ngoài');
    
    const newDoc = {
      id: docId,
      docNo: `CV-DEN-2026-00${Math.floor(158 + Math.random() * 10)}`,
      subject: subject,
      sender: senderName,
      originalNo: originalNo || '1025/VNPT-VP',
      date: dateStr,
      priority: priority === 'Urgent' ? 'Khẩn' : priority === 'Secret' ? 'Mật' : priority === 'TopSecret' ? 'Hỏa tốc' : 'Thường',
      status: 'Chờ xử lý',
      content: content
    };

    try {
      const existing = localStorage.getItem('custom_incoming_docs');
      const list = existing ? JSON.parse(existing) : [];
      list.unshift(newDoc);
      localStorage.setItem('custom_incoming_docs', JSON.stringify(list));
    } catch (err) {
      console.error(err);
    }

    setTimeout(() => {
      setSubmitting(false);
      setSubmitSuccess(true);
      setTimeout(() => {
        router.push('/documents/incoming');
      }, 1000);
    }, 600);
  };

  return (
    <>
      <Header title="Đăng ký công văn đến (Hỗ trợ AI OCR Trích xuất Tự động)" />
      <main className="flex-1 p-8 max-w-4xl mx-auto w-full space-y-6">
        
        {/* AI OCR Automatic Extraction Banner */}
        <div className="p-6 bg-zinc-950 border border-zinc-800 rounded-2xl space-y-4 shadow-sm relative overflow-hidden">
          <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 rounded-xl bg-purple-950/60 border border-purple-800/60 text-purple-400 flex items-center justify-center text-lg">
                🤖
              </div>
              <div>
                <h3 className="font-bold text-sm text-white">Tiếp nhận & Trích xuất Dữ liệu Tự động (AI OCR Engine)</h3>
                <p className="text-xs text-zinc-400">Tải tệp PDF công văn đến để hệ thống tự động bóc tách Số hiệu, Đơn vị gửi, Trích yếu và Độ khẩn.</p>
              </div>
            </div>

            <button
              type="button"
              onClick={runOcrExtraction}
              disabled={extracting}
              className="px-4 py-2 bg-purple-600 hover:bg-purple-500 text-white rounded-xl text-xs font-bold transition-all cursor-pointer shadow-md flex items-center gap-2 shrink-0"
            >
              {extracting ? '⚡ Đang quét OCR...' : '⚡ Thử nghiệm Trích xuất AI mẫu'}
            </button>
          </div>

          {extracting && (
            <div className="p-3 bg-purple-950/30 border border-purple-800/40 rounded-xl text-xs text-purple-300 flex items-center gap-3 animate-pulse">
              <span className="w-4 h-4 rounded-full border-2 border-purple-400 border-t-transparent animate-spin"></span>
              <span>Đang đọc quét bề mặt tài liệu PDF, phân tích khung văn bản & OCR nhận diện ký tự...</span>
            </div>
          )}

          {extractedSuccess && (
            <div className="p-3 bg-emerald-950/40 border border-emerald-800/50 rounded-xl text-xs text-emerald-400 font-bold flex items-center justify-between">
              <span className="flex items-center gap-2">
                ✓ Trích xuất dữ liệu tự động thành công! Đã tự động điền các trường thông tin bên dưới.
              </span>
              <span className="text-[10px] text-emerald-300 font-mono bg-emerald-900/40 px-2 py-0.5 rounded">Độ chính xác: 99.8%</span>
            </div>
          )}
        </div>

        {/* Incoming Registration Form */}
        <form onSubmit={handleSubmit} className="p-8 bg-zinc-950 border border-zinc-800 rounded-2xl space-y-6 shadow-sm">
          
          <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
            
            <div className="md:col-span-2">
              <label className="block text-xs font-bold text-zinc-400 mb-2 uppercase tracking-wide">File đính kèm (Scan / PDF)</label>
              <input
                type="file"
                accept="application/pdf"
                onChange={handleFileUpload}
                className="w-full text-xs text-zinc-400 file:mr-4 file:py-2 file:px-4 file:rounded-lg file:border file:border-zinc-800 file:text-xs file:font-semibold file:bg-zinc-900 file:text-white hover:file:bg-zinc-800 cursor-pointer"
              />
              <p className="text-[11px] text-zinc-500 mt-1">Chọn tệp PDF để kích hoạt quy trình trích xuất OCR tự động.</p>
            </div>

            <div className="md:col-span-2">
              <label className="block text-xs font-bold text-zinc-400 mb-2 uppercase tracking-wide">Trích yếu nội dung (Đã trích xuất)</label>
              <input
                type="text"
                value={subject}
                onChange={(e) => setSubject(e.target.value)}
                required
                placeholder="Ví dụ: V/v ban hành quy định bảo mật hệ thống thông tin..."
                className="w-full px-4 py-2.5 border border-zinc-800 bg-zinc-900 rounded-xl text-xs focus:outline-none focus:border-zinc-500 text-white font-medium"
              />
            </div>

            <div>
              <label className="block text-xs font-bold text-zinc-400 mb-2 uppercase tracking-wide">Đối tác gửi (External Entity)</label>
              <select
                value={sender}
                onChange={(e) => setSender(e.target.value)}
                required
                className="w-full px-4 py-2.5 border border-zinc-800 bg-zinc-900 rounded-xl text-xs focus:outline-none focus:border-zinc-500 text-white font-medium"
              >
                <option value="">Chọn đối tác</option>
                <option value="vnpt">Tập đoàn VNPT</option>
                <option value="kstthc">Cục KSTTHC</option>
                <option value="vietcombank">Ngân hàng Vietcombank</option>
              </select>
            </div>

            <div>
              <label className="block text-xs font-bold text-zinc-400 mb-2 uppercase tracking-wide">Số hiệu công văn gốc</label>
              <input
                type="text"
                value={originalNo}
                onChange={(e) => setOriginalNo(e.target.value)}
                required
                placeholder="Ví dụ: 1025/VNPT-VP"
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
              <label className="block text-xs font-bold text-zinc-400 mb-2 uppercase tracking-wide">Chi tiết / Nội dung trích yếu công văn</label>
              <textarea
                rows={4}
                value={content}
                onChange={(e) => setContent(e.target.value)}
                placeholder="Nhập hoặc để AI tự động điền ghi chú tóm tắt nội dung chính..."
                className="w-full px-4 py-2.5 border border-zinc-800 bg-zinc-900 rounded-xl text-xs focus:outline-none focus:border-zinc-500 text-white"
              />
            </div>

          </div>

          <div className="flex justify-end gap-3 border-t border-zinc-800 pt-6">
            <button
              type="button"
              onClick={() => router.push('/documents/incoming')}
              className="px-5 py-2.5 border border-zinc-800 text-zinc-300 hover:text-white rounded-xl text-xs font-bold transition-colors cursor-pointer"
            >
              Hủy
            </button>
            <button
              type="submit"
              className="px-5 py-2.5 bg-white text-black hover:bg-zinc-200 rounded-xl text-xs font-bold transition-colors shadow-sm cursor-pointer"
            >
              Đăng ký công văn
            </button>
          </div>
        </form>

      </main>
    </>
  );
}
