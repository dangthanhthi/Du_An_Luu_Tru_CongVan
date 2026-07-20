'use client';
import Header from '@/components/Header';
import React, { useState } from 'react';
import { useRouter } from 'next/navigation';

export default function NewOutgoingDoc() {
  const router = useRouter();
  const [subject, setSubject] = useState('');
  const [content, setContent] = useState('');
  const [receiver, setReceiver] = useState('');
  const [receiverName, setReceiverName] = useState('');
  const [priority, setPriority] = useState('Normal');
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
      setSubject('V/v phúc đáp đề nghị hợp tác triển khai hạ tầng lưu trữ số năm 2026');
      setReceiver('vnpt');
      setReceiverName('Tập đoàn VNPT');
      setPriority('Urgent');
      setContent('Kính gửi Tập đoàn VNPT,\n\nThực hiện công văn số 1025/VNPT-VP ngày 15/07/2026, Công ty chúng tôi xin trân trọng thông báo đồng ý phương án phối hợp kết nối hạ tầng số. Chi tiết kế hoạch triển khai được đính kèm trong phụ lục bản thảo này.\n\nTrân trọng.');
      setFileName('Bản_thảo_Công_văn_đi_VNPT_2026.pdf');
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
      docNo: `CV-DI-2026-000${Math.floor(90 + Math.random() * 10)}`,
      subject: subject,
      receiver: receiverName || (receiver === 'vnpt' ? 'Tập đoàn VNPT' : receiver === 'kstthc' ? 'Cục KSTTHC' : receiver === 'vietcombank' ? 'Ngân hàng Vietcombank' : receiver),
      date: dateStr,
      priority: priority === 'Urgent' ? 'Khẩn' : priority === 'Secret' ? 'Mật' : priority === 'TopSecret' ? 'Hỏa tốc' : 'Thường',
      status: 'Đang duyệt',
      content: content,
      fileName: fileName || 'Document_Draft.pdf'
    };

    // Save to localStorage
    try {
      const existing = localStorage.getItem('custom_outgoing_docs');
      const list = existing ? JSON.parse(existing) : [];
      list.unshift(newDoc);
      localStorage.setItem('custom_outgoing_docs', JSON.stringify(list));
    } catch (err) {
      console.error(err);
    }

    setTimeout(() => {
      setSubmitting(false);
      setSubmitSuccess(true);
      setTimeout(() => {
        router.push('/documents/outgoing');
      }, 1000);
    }, 600);
  };

  return (
    <>
      <Header title="Tạo công văn đi (Hỗ trợ AI Trợ lý Soạn thảo)" />
      <main className="flex-1 p-8 max-w-4xl mx-auto w-full space-y-6">
        
        {/* AI Draft Assistant Banner */}
        <div className="p-6 bg-zinc-950 border border-zinc-800 rounded-2xl space-y-4 shadow-sm relative overflow-hidden">
          <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 rounded-xl bg-emerald-950/60 border border-emerald-800/60 text-emerald-400 flex items-center justify-center text-lg">
                ✨
              </div>
              <div>
                <h3 className="font-bold text-sm text-white">AI Trợ lý Soạn thảo Công văn đi</h3>
                <p className="text-xs text-zinc-400">Tự động gợi ý trích yếu, thể thức văn bản chuẩn và mẫu nội dung phúc đáp doanh nghiệp.</p>
              </div>
            </div>

            <button
              type="button"
              onClick={runAiDraft}
              disabled={generating}
              className="px-4 py-2 bg-emerald-600 hover:bg-emerald-500 text-white rounded-xl text-xs font-bold transition-all cursor-pointer shadow-md flex items-center gap-2 shrink-0"
            >
              {generating ? '⚡ Đang khởi tạo...' : '⚡ Sinh bản thảo AI mẫu'}
            </button>
          </div>

          {generating && (
            <div className="p-3 bg-emerald-950/30 border border-emerald-800/40 rounded-xl text-xs text-emerald-300 flex items-center gap-3 animate-pulse">
              <span className="w-4 h-4 rounded-full border-2 border-emerald-400 border-t-transparent animate-spin"></span>
              <span>Đang phân tích dữ liệu đối tác & tạo nội dung bản thảo công văn...</span>
            </div>
          )}

          {generatedSuccess && (
            <div className="p-3 bg-emerald-950/40 border border-emerald-800/50 rounded-xl text-xs text-emerald-400 font-bold flex items-center justify-between">
              <span className="flex items-center gap-2">
                ✓ Đã tự động sinh nội dung bản thảo AI mẫu! Bạn có thể chỉnh sửa lại bên dưới.
              </span>
            </div>
          )}
        </div>

        {/* Success toast */}
        {submitSuccess && (
          <div className="p-4 bg-emerald-950/50 border border-emerald-800 rounded-xl text-emerald-400 text-xs font-bold flex items-center gap-2 animate-fade-in">
            <span className="text-base">✓</span>
            <span>Tạo bản thảo công văn đi thành công! Đang chuyển đến danh sách công văn...</span>
          </div>
        )}

        {/* Create Outgoing Document Form */}
        <form onSubmit={handleSubmit} className="p-8 bg-zinc-950 border border-zinc-800 rounded-2xl space-y-6 shadow-sm">
          
          <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
            
            <div className="md:col-span-2">
              <label className="block text-xs font-bold text-zinc-400 mb-2 uppercase tracking-wide">Trích yếu nội dung công văn đi *</label>
              <input
                type="text"
                value={subject}
                onChange={(e) => setSubject(e.target.value)}
                required
                placeholder="Ví dụ: V/v phúc đáp đề nghị cung cấp hồ sơ năng lực hạ tầng số..."
                className="w-full px-4 py-2.5 border border-zinc-800 bg-zinc-900 rounded-xl text-xs focus:outline-none focus:border-zinc-500 text-white font-medium"
              />
            </div>

            <div>
              <label className="block text-xs font-bold text-zinc-400 mb-2 uppercase tracking-wide">Đối tác nhận (Nơi nhận công văn) *</label>
              <div className="space-y-2">
                <select
                  value={receiver}
                  onChange={(e) => {
                    setReceiver(e.target.value);
                    if (e.target.value === 'vnpt') setReceiverName('Tập đoàn VNPT');
                    else if (e.target.value === 'kstthc') setReceiverName('Cục KSTTHC');
                    else if (e.target.value === 'vietcombank') setReceiverName('Ngân hàng Vietcombank');
                    else setReceiverName('');
                  }}
                  className="w-full px-4 py-2.5 border border-zinc-800 bg-zinc-900 rounded-xl text-xs focus:outline-none focus:border-zinc-500 text-white font-medium"
                >
                  <option value="">-- Chọn đối tác khả dụng --</option>
                  <option value="vnpt">Tập đoàn VNPT</option>
                  <option value="kstthc">Cục KSTTHC - Bộ Nội Vụ</option>
                  <option value="vietcombank">Ngân hàng Vietcombank</option>
                  <option value="custom">Đơn vị nhận khác...</option>
                </select>

                {receiver === 'custom' && (
                  <input
                    type="text"
                    value={receiverName}
                    onChange={(e) => setReceiverName(e.target.value)}
                    required
                    placeholder="Nhập tên cơ quan / đơn vị nhận công văn..."
                    className="w-full px-4 py-2 border border-zinc-800 bg-zinc-900 rounded-xl text-xs focus:outline-none focus:border-zinc-500 text-white font-medium"
                  />
                )}
              </div>
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
              <label className="block text-xs font-bold text-zinc-400 mb-2 uppercase tracking-wide">Bản thảo tệp PDF đính kèm (Scan / PDF)</label>
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
              <label className="block text-xs font-bold text-zinc-400 mb-2 uppercase tracking-wide">Mô tả chi tiết / Nội dung công văn *</label>
              <textarea
                rows={5}
                value={content}
                onChange={(e) => setContent(e.target.value)}
                required
                placeholder="Nhập nội dung chi tiết công văn đi hoặc nhấn '⚡ Sinh bản thảo AI mẫu'..."
                className="w-full px-4 py-2.5 border border-zinc-800 bg-zinc-900 rounded-xl text-xs focus:outline-none focus:border-zinc-500 text-white leading-relaxed font-sans"
              />
            </div>

          </div>

          <div className="flex justify-end gap-3 border-t border-zinc-800 pt-6">
            <button
              type="button"
              onClick={() => router.push('/documents/outgoing')}
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
                  <span>Đang tạo bản thảo...</span>
                </>
              ) : (
                <span>Tạo bản thảo công văn đi</span>
              )}
            </button>
          </div>
        </form>

      </main>
    </>
  );
}
