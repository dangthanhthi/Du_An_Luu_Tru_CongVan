'use client';
import React from 'react';
import Link from 'next/link';

export default function WelcomePage() {
  return (
    <div className="min-h-screen text-zinc-100 flex flex-col relative overflow-hidden bg-black vercel-bg-pattern font-sans antialiased selection:bg-white selection:text-black">
      
      {/* Vercel Ambient Glow */}
      <div className="absolute top-0 left-1/2 -translate-x-1/2 w-[1000px] h-[500px] bg-[radial-gradient(ellipse_at_top,rgba(255,255,255,0.08)_0%,transparent_65%)] pointer-events-none z-0"></div>

      {/* Top Floating Header Bar */}
      <header className="fixed top-0 left-0 right-0 z-30 h-16 border-b border-zinc-800/80 bg-black/60 backdrop-blur-xl flex items-center justify-between px-8">
        
        {/* Brand Logo */}
        <div className="flex items-center gap-3">
          <div className="w-8 h-8 rounded-lg bg-white text-black flex items-center justify-center font-extrabold shadow-sm transition-transform hover:scale-105">
            <svg className="w-4.5 h-4.5 fill-current" viewBox="0 0 24 24">
              <path d="M12 1L24 22H0L12 1Z" />
            </svg>
          </div>
          <span className="font-extrabold text-sm text-white tracking-tight">CV</span>
        </div>

        {/* Action links */}
        <div className="flex items-center gap-4">
          <Link
            href="/login"
            className="text-xs font-semibold text-zinc-400 hover:text-white transition-colors cursor-pointer"
          >
            Đăng nhập
          </Link>
          
          <Link
            href="/login"
            className="px-3.5 py-1.5 bg-white text-black hover:bg-zinc-200 font-semibold rounded-lg text-xs transition-all duration-150 cursor-pointer shadow-sm active:scale-95 shrink-0"
          >
            Đăng ký
          </Link>
        </div>
      </header>

      {/* Main Hero Section */}
      <main className="flex-1 flex flex-col justify-center items-center max-w-5xl mx-auto px-6 pt-36 pb-20 z-10 relative text-center">
        
        {/* Vercel Pill Badge */}
        <div className="mb-8 px-4 py-1 bg-zinc-950 border border-zinc-800 text-zinc-300 rounded-full text-xs font-medium inline-flex items-center gap-2 shadow-sm animate-fade-in">
          <span className="w-2 h-2 rounded-full bg-emerald-500 animate-pulse"></span>
          <span>Hệ thống Quản lý & Lưu trữ Công văn Enterprise</span>
        </div>

        {/* Hero Slogan */}
        <h1 className="text-4xl md:text-6xl font-extrabold tracking-tighter leading-[1.1] max-w-4xl text-white">
          Hệ thống lưu trữ & điều hành <br />
          <span className="bg-gradient-to-r from-white via-zinc-200 to-zinc-500 bg-clip-text text-transparent">
            công văn doanh nghiệp
          </span>
        </h1>

        {/* Primary & Secondary CTA Buttons */}
        <div className="mt-10 animate-fade-in flex flex-row items-center gap-4 justify-center">
          <Link
            href="/login"
            className="group px-6 py-3 bg-white text-black font-semibold rounded-lg hover:bg-zinc-200 transition-all duration-200 flex items-center gap-2 shadow-md active:scale-95 cursor-pointer text-sm shrink-0"
          >
            <span>Bắt đầu ngay</span>
            <svg className="w-4 h-4 group-hover:translate-x-1 transition-transform duration-200" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M5 12h14M12 5l7 7-7 7" />
            </svg>
          </Link>
          
          <Link
            href="/login"
            className="px-6 py-3 bg-zinc-950 text-zinc-300 font-semibold rounded-lg border border-zinc-800 hover:border-zinc-700 hover:text-white transition-all duration-200 cursor-pointer text-sm shrink-0 active:scale-95 shadow-xs"
          >
            Liên hệ hỗ trợ
          </Link>
        </div>

        {/* Vercel Bento Feature Cards */}
        <div className="grid grid-cols-1 md:grid-cols-3 gap-6 mt-20 w-full text-left">
          {[
            {
              icon: (
                <svg className="w-5 h-5 text-emerald-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                  <path strokeLinecap="round" strokeLinejoin="round" d="M9.75 17L9 20l-1 1h8l-1-1-.75-3M3 13h18M5 17h14a2 2 0 002-2V5a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z" />
                </svg>
              ),
              title: 'AI OCR Thông Minh',
              desc: 'Tự động quét và đọc nội dung văn bản scan PDF, tự động nhận diện đối tác gửi và đề xuất điền trường dữ liệu.'
            },
            {
              icon: (
                <svg className="w-5 h-5 text-emerald-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                  <path strokeLinecap="round" strokeLinejoin="round" d="M8 7h12m0 0l-4-4m4 4l-4 4m0 6H4m0 0l4 4m-4-4l4-4" />
                </svg>
              ),
              title: 'Phân Phối Phòng Ban',
              desc: 'Thư ký dễ dàng phân phối công văn trực tiếp đến nhiều phòng ban. Cấp quyền truy cập xem chi tiết tài liệu theo phòng ban.'
            },
            {
              icon: (
                <svg className="w-5 h-5 text-emerald-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                  <path strokeLinecap="round" strokeLinejoin="round" d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z" />
                </svg>
              ),
              title: 'Lưu Trữ Bảo Mật',
              desc: 'Quản lý toàn bộ lịch sử công văn trao đổi với cơ chế phân quyền chặt chẽ, an toàn dữ liệu và tối ưu tốc độ tra cứu.'
            }
          ].map((feat, idx) => (
            <div
              key={idx}
              className="p-6 bg-zinc-950/80 border border-zinc-800 rounded-xl hover:border-zinc-700 transition-all duration-300 shadow-sm"
            >
              <div className="w-10 h-10 bg-zinc-900 border border-zinc-800 rounded-lg flex items-center justify-center mb-5">
                {feat.icon}
              </div>
              <h3 className="font-bold text-white text-sm tracking-tight">{feat.title}</h3>
              <p className="text-zinc-400 text-xs mt-2.5 leading-relaxed font-medium">{feat.desc}</p>
            </div>
          ))}
        </div>

      </main>

      {/* Footer */}
      <footer className="w-full text-center py-6 border-t border-zinc-900 z-10 relative bg-black">
        <p className="text-xs text-zinc-500 font-medium">
          CV © 2026 — Document Archive System. Built with Next.js & Vercel Design System.
        </p>
      </footer>

    </div>
  );
}
