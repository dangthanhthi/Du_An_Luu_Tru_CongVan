'use client';
import Header from '@/components/Header';
import React, { useState } from 'react';
import { useRouter } from 'next/navigation';

export default function NewInternalDoc() {
  const router = useRouter();
  const [subject, setSubject] = useState('');
  const [content, setContent] = useState('');
  const [priority, setPriority] = useState('Normal');

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    router.push('/documents/internal');
  };

  return (
    <>
      <Header title="Tạo công văn nội bộ" />
      <main className="flex-1 p-8 max-w-4xl mx-auto w-full">
        
        <form onSubmit={handleSubmit} className="p-8 bg-card border border-border rounded-2xl space-y-6 shadow-sm transition-all duration-200">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
            
            <div className="md:col-span-2">
              <label className="block text-xs font-bold text-muted-foreground mb-2 uppercase tracking-wide">Trích yếu nội dung</label>
              <input
                type="text"
                value={subject}
                onChange={(e) => setSubject(e.target.value)}
                required
                placeholder="Ví dụ: Quy chế làm việc từ xa và bảo mật thông tin nội bộ..."
                className="w-full px-4 py-2.5 border border-border bg-muted rounded-xl text-sm focus:outline-none focus:border-emerald-500 text-foreground font-medium"
              />
            </div>

            <div>
              <label className="block text-xs font-bold text-muted-foreground mb-2 uppercase tracking-wide">Độ khẩn / Bảo mật</label>
              <select
                value={priority}
                onChange={(e) => setPriority(e.target.value)}
                className="w-full px-4 py-2.5 border border-border bg-muted rounded-xl text-sm focus:outline-none focus:border-emerald-500 text-foreground font-medium"
              >
                <option value="Normal">Thường</option>
                <option value="Urgent">Khẩn</option>
                <option value="Secret">Mật</option>
                <option value="TopSecret">Hỏa tốc / Tối mật</option>
              </select>
            </div>

            <div>
              <label className="block text-xs font-bold text-muted-foreground mb-2 uppercase tracking-wide">Bản thảo PDF đính kèm</label>
              <input
                type="file"
                accept="application/pdf"
                className="w-full text-sm text-muted-foreground file:mr-4 file:py-2 file:px-4 file:rounded-xl file:border-0 file:text-xs file:font-semibold file:bg-zinc-100 file:text-zinc-800 hover:file:bg-zinc-200"
              />
            </div>

            <div className="md:col-span-2">
              <label className="block text-xs font-bold text-muted-foreground mb-2 uppercase tracking-wide">Nội dung chi tiết</label>
              <textarea
                rows={4}
                value={content}
                onChange={(e) => setContent(e.target.value)}
                placeholder="Nhập ghi chú tóm tắt nội dung chính..."
                className="w-full px-4 py-2.5 border border-border bg-muted rounded-xl text-sm focus:outline-none focus:border-emerald-500 text-foreground"
              />
            </div>

          </div>

          <div className="flex justify-end gap-3 border-t border-border pt-6">
            <button
              type="button"
              onClick={() => router.push('/documents/internal')}
              className="px-5 py-2.5 border border-border text-foreground dark:text-zinc-355 rounded-xl text-sm font-semibold hover:bg-muted dark:hover:bg-zinc-950 transition-colors cursor-pointer"
            >
              Hủy
            </button>
            <button
              type="submit"
              className="px-5 py-2.5 bg-primary text-primary-foreground hover:opacity-90 rounded-xl text-sm font-semibold transition-colors shadow-sm cursor-pointer"
            >
              Đăng ký công văn nội bộ
            </button>
          </div>
        </form>

      </main>
    </>
  );
}
