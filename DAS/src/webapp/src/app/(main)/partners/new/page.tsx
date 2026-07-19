'use client';
import Header from '@/components/Header';
import React, { useState } from 'react';
import { useRouter } from 'next/navigation';

export default function NewPartner() {
  const router = useRouter();
  const [fullName, setFullName] = useState('');
  const [shortName, setShortName] = useState('');
  const [taxCode, setTaxCode] = useState('');
  const [contactPerson, setContactPerson] = useState('');
  const [phone, setPhone] = useState('');
  const [email, setEmail] = useState('');

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    router.push('/partners');
  };

  return (
    <>
      <Header title="Thêm đối tác mới" />
      <main className="flex-1 p-8 max-w-4xl mx-auto w-full">
        
        <form onSubmit={handleSubmit} className="p-8 bg-card border border-border rounded-2xl space-y-6 shadow-sm transition-all duration-200">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
            
            <div className="md:col-span-2">
              <label className="block text-xs font-bold text-muted-foreground mb-2 uppercase tracking-wide">Tên đầy đủ của tổ chức đối tác</label>
              <input
                type="text"
                value={fullName}
                onChange={(e) => setFullName(e.target.value)}
                required
                placeholder="Ví dụ: Tập đoàn Công nghệ Viễn thông VNPT..."
                className="w-full px-4 py-2.5 border border-border bg-muted rounded-xl text-sm focus:outline-none focus:border-emerald-500 text-foreground font-medium"
              />
            </div>

            <div>
              <label className="block text-xs font-bold text-muted-foreground mb-2 uppercase tracking-wide">Tên viết tắt</label>
              <input
                type="text"
                value={shortName}
                onChange={(e) => setShortName(e.target.value)}
                required
                placeholder="Ví dụ: VNPT"
                className="w-full px-4 py-2.5 border border-border bg-muted rounded-xl text-sm focus:outline-none focus:border-emerald-500 text-foreground font-medium"
              />
            </div>

            <div>
              <label className="block text-xs font-bold text-muted-foreground mb-2 uppercase tracking-wide">Mã số thuế</label>
              <input
                type="text"
                value={taxCode}
                onChange={(e) => setTaxCode(e.target.value)}
                required
                placeholder="Ví dụ: 0100684398"
                className="w-full px-4 py-2.5 border border-border bg-muted rounded-xl text-sm focus:outline-none focus:border-emerald-500 text-foreground font-medium"
              />
            </div>

            <div>
              <label className="block text-xs font-bold text-muted-foreground mb-2 uppercase tracking-wide">Người liên hệ</label>
              <input
                type="text"
                value={contactPerson}
                onChange={(e) => setContactPerson(e.target.value)}
                required
                placeholder="Ví dụ: Nguyễn Văn A"
                className="w-full px-4 py-2.5 border border-border bg-muted rounded-xl text-sm focus:outline-none focus:border-emerald-500 text-zinc-850 font-medium"
              />
            </div>

            <div>
              <label className="block text-xs font-bold text-muted-foreground mb-2 uppercase tracking-wide">Số điện thoại liên hệ</label>
              <input
                type="text"
                value={phone}
                onChange={(e) => setPhone(e.target.value)}
                required
                placeholder="Ví dụ: 0243789789"
                className="w-full px-4 py-2.5 border border-border bg-muted rounded-xl text-sm focus:outline-none focus:border-emerald-500 text-zinc-850 font-medium"
              />
            </div>

            <div className="md:col-span-2">
              <label className="block text-xs font-bold text-muted-foreground mb-2 uppercase tracking-wide">Email liên hệ</label>
              <input
                type="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                required
                placeholder="Ví dụ: contact@vnpt.vn"
                className="w-full px-4 py-2.5 border border-border bg-muted rounded-xl text-sm focus:outline-none focus:border-emerald-500 text-zinc-850 font-medium"
              />
            </div>

          </div>

          <div className="flex justify-end gap-3 border-t border-border pt-6">
            <button
              type="button"
              onClick={() => router.push('/partners')}
              className="px-5 py-2.5 border border-border text-foreground rounded-xl text-sm font-semibold hover:bg-muted dark:hover:bg-zinc-950 transition-colors cursor-pointer"
            >
              Hủy
            </button>
            <button
              type="submit"
              className="px-5 py-2.5 bg-primary text-primary-foreground hover:opacity-90 rounded-xl text-sm font-semibold transition-colors shadow-sm cursor-pointer"
            >
              Thêm đối tác
            </button>
          </div>
        </form>

      </main>
    </>
  );
}
