'use client';
import React from 'react';
import Link from 'next/link';
import { useTheme } from '@/context/ThemeContext';

export default function WelcomePage() {
  const { theme, toggleTheme } = useTheme();

  return (
    <div className="min-h-screen text-zinc-900 dark:text-white transition-colors duration-300 flex flex-col relative overflow-hidden bg-white dark:bg-[#0d1117]">
      
      {/* Background Image Container */}
      <div 
        className="absolute inset-0 z-0 bg-cover bg-center transition-all duration-300"
        style={{ backgroundImage: "url('/bg-crystals.png')" }}
      />
      
      {/* Dynamic theme-aware overlay for crystal clear text readability */}
      <div className="absolute inset-0 z-0 bg-white/75 dark:bg-[#0d1117]/88 backdrop-blur-[1.5px] transition-all duration-300" />

      {/* Subtle monochrome & emerald aura shapes for depth */}
      <div className="absolute top-[-10%] left-[-10%] w-[45vw] h-[45vw] bg-emerald-500/[0.04] rounded-full blur-[140px] pointer-events-none z-0"></div>
      <div className="absolute bottom-[-10%] right-[-10%] w-[50vw] h-[50vw] bg-muted-foreground/[0.02] rounded-full blur-[140px] pointer-events-none z-0"></div>

      {/* FLOATING HEADER CONTROLS (No rigid header bar) */}
      <div className="absolute top-6 left-8 z-20 flex items-center">
        <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-emerald-600 to-teal-500 text-white flex items-center justify-center shadow-sm transition-transform hover:scale-105 duration-200">
          <svg className="w-5.5 h-5.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5} strokeLinecap="round" strokeLinejoin="round">
            <path d="M14.5 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V7.5L14.5 2z" />
            <polyline points="14 2 14 8 20 8" />
            <line x1="16" y1="13" x2="8" y2="13" />
            <line x1="16" y1="17" x2="8" y2="17" />
            <line x1="10" y1="9" x2="8" y2="9" />
          </svg>
        </div>
      </div>

      <div className="absolute top-6 right-8 z-20 flex items-center gap-4">
        <button
          onClick={toggleTheme}
          className="p-2 border border-zinc-800 bg-zinc-900/60 backdrop-blur-md rounded-lg hover:bg-zinc-800 transition-all duration-200 cursor-pointer shadow-sm active:scale-95 text-white"
          aria-label="Toggle Theme"
        >
          {theme === 'light' ? (
            <svg className="w-4.5 h-4.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M20.354 15.354A9 9 0 018.646 3.646 9.003 9.003 0 0012 21a9.003 9.003 0 008.354-5.646z" />
            </svg>
          ) : (
            <svg className="w-4.5 h-4.5 text-amber-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M12 3v1m0 16v1m9-9h-1M4 12H3m15.364-6.364l-.707.707M6.343 17.657l-.707.707m12.728 0l-.707-.707M6.343 6.343l-.707-.707M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
            </svg>
          )}
        </button>
        
        <Link
          href="/login"
          className="text-xs font-semibold text-zinc-400 hover:text-white transition-colors cursor-pointer"
        >
          Đăng nhập
        </Link>
        
        <Link
          href="/login"
          className="px-3.5 py-1.5 bg-white text-black hover:bg-zinc-200 font-bold rounded-md text-xs transition-all duration-150 cursor-pointer shadow-sm active:scale-95 shrink-0"
        >
          Đăng ký
        </Link>
      </div>

      {/* Main Content */}
      <main className="flex-1 flex flex-col justify-center items-center max-w-5xl mx-auto px-8 pt-36 pb-16 z-10 relative text-center">
        
        {/* Subtle Pill Badge */}
        <div className="mb-6 px-4.5 py-1.5 bg-zinc-900/60 backdrop-blur-md border border-zinc-800 text-zinc-400 rounded-full text-[10px] font-bold uppercase tracking-wider animate-fade-in shadow-sm select-none">
          Document Archive System
        </div>

        {/* Hero Slogan */}
        <h1 className="text-4xl md:text-6xl font-extrabold tracking-tight leading-[1.15] max-w-4xl text-zinc-900 dark:text-white transition-all duration-300">
          Hệ thống lưu trữ & điều hành <br />
          <span className="text-accent">công văn</span>
        </h1>

        {/* Primary & Secondary CTA Buttons */}
        <div className="mt-10 animate-fade-in flex flex-row items-center gap-4 justify-center">
          <Link
            href="/login"
            className="group px-6 py-3 bg-white text-black font-semibold rounded-full hover:bg-zinc-200 transition-all duration-200 flex items-center gap-2.5 shadow-md active:scale-95 cursor-pointer text-sm shrink-0"
          >
            <span>Bắt đầu ngay</span>
            <svg className="w-4 h-4 group-hover:translate-x-1 transition-transform duration-200" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5} strokeLinecap="round" strokeLinejoin="round">
              <path d="M5 12h14M12 5l7 7-7 7" />
            </svg>
          </Link>
          
          <Link
            href="/login"
            className="px-6 py-3 bg-transparent text-white font-semibold rounded-full border border-zinc-800 hover:bg-zinc-900 transition-all duration-200 cursor-pointer text-sm shrink-0 active:scale-95"
          >
            Liên hệ hỗ trợ
          </Link>
        </div>

        {/* Bento Grid - 3 Column Feature Cards */}
        <div className="grid grid-cols-1 md:grid-cols-3 gap-8 mt-20 w-full text-left">
          {[
            {
              icon: (
                <svg className="w-6 h-6 text-accent" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                  <path strokeLinecap="round" strokeLinejoin="round" d="M9.75 17L9 20l-1 1h8l-1-1-.75-3M3 13h18M5 17h14a2 2 0 002-2V5a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z" />
                </svg>
              ),
              title: 'AI OCR Thông Minh',
              desc: 'Tự động quét và đọc nội dung văn bản scan PDF, tự động nhận diện đối tác gửi và đề xuất điền trường dữ liệu.'
            },
            {
              icon: (
                <svg className="w-6 h-6 text-accent" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                  <path strokeLinecap="round" strokeLinejoin="round" d="M8 7h12m0 0l-4-4m4 4l-4 4m0 6H4m0 0l4 4m-4-4l4-4" />
                </svg>
              ),
              title: 'Phân Phối Phòng Ban',
              desc: 'Thư ký dễ dàng phân phối công văn trực tiếp đến nhiều phòng ban. Cấp quyền truy cập xem chi tiết tài liệu theo phòng ban.'
            },
            {
              icon: (
                <svg className="w-6 h-6 text-accent" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                  <path strokeLinecap="round" strokeLinejoin="round" d="M12 15v2m-6 4h12a2 2 0 002-2v-6a2 2 0 00-2-2H6a2 2 0 00-2 2v6a2 2 0 002 2zm10-10V7a4 4 0 00-8 0v4h8z" />
                </svg>
              ),
              title: 'Lưu Trữ Bảo Mật',
              desc: 'Quản lý toàn bộ lịch sử công văn trao đổi với cơ chế phân quyền chặt chẽ, an toàn dữ liệu và tối ưu tốc độ tra cứu.'
            }
          ].map((feat, idx) => (
            <div
              key={idx}
              className="p-8 bg-zinc-50/70 dark:bg-zinc-900/60 backdrop-blur-md border border-zinc-200 dark:border-zinc-800/80 rounded-2xl shadow-[0_4px_30px_rgba(0,0,0,0.02)] hover:shadow-[0_8px_40px_rgba(0,0,0,0.06)] hover:border-accent/40 hover:scale-[1.02] transition-all duration-300"
            >
              <div className="w-12 h-12 bg-white dark:bg-zinc-900 border border-zinc-200 dark:border-zinc-800 rounded-xl flex items-center justify-center mb-6 shadow-sm">
                {feat.icon}
              </div>
              <h3 className="font-bold text-zinc-900 dark:text-white text-base">{feat.title}</h3>
              <p className="text-zinc-600 dark:text-zinc-400 text-xs mt-3 leading-relaxed font-semibold">{feat.desc}</p>
            </div>
          ))}
        </div>

      </main>

      {/* Footer */}
      <footer className="w-full text-center py-8 border-t border-zinc-200 dark:border-zinc-800 z-10 relative">
        <p className="text-xs text-muted-foreground font-semibold">
          CV © 2026 — Document Archive System.
        </p>
      </footer>

      {/* Embedded Animations CSS */}
      <style jsx global>{`
        @keyframes fadeIn {
          from { opacity: 0; transform: translateY(12px); }
          to { opacity: 1; transform: translateY(0); }
        }
        .animate-fade-in {
          animation: fadeIn 0.8s cubic-bezier(0.16, 1, 0.3, 1) forwards;
        }
        @keyframes bounceSubtle {
          0%, 100% { transform: translateY(0); }
          50% { transform: translateY(-4px); }
        }
        .animate-bounce-subtle {
          animation: bounceSubtle 3s ease-in-out infinite;
        }
      `}</style>

    </div>
  );
}
