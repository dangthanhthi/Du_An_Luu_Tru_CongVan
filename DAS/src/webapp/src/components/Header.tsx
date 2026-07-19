'use client';
import Link from 'next/link';

interface HeaderProps {
  title: string;
}

export default function Header({ title }: HeaderProps) {
  return (
    <header className="h-14 border-b border-zinc-800 bg-black/90 backdrop-blur-md sticky top-0 z-30 flex items-center justify-between px-6 transition-colors duration-200">
      
      {/* Breadcrumb Path */}
      <div className="flex items-center gap-2 text-sm">
        <span className="font-extrabold text-white">CV</span>
        <span className="text-zinc-600 font-light">/</span>
        <span className="text-zinc-400 font-medium text-xs bg-zinc-900 border border-zinc-800 px-2 py-0.5 rounded-full flex items-center gap-1.5">
          <span className="w-1.5 h-1.5 rounded-full bg-emerald-500 animate-pulse"></span>
          Production
        </span>
        <span className="text-zinc-600 font-light">/</span>
        <h1 className="font-bold text-white truncate max-w-[200px] sm:max-w-none">{title}</h1>
      </div>

      {/* Right controls */}
      <div className="flex items-center gap-2.5">

        {/* Search input with Ctrl+K badge */}
        <div className="relative hidden lg:flex items-center">
          <input
            type="text"
            placeholder="Tìm kiếm tài liệu..."
            className="w-56 pl-8 pr-10 py-1.5 bg-zinc-950 border border-zinc-800 rounded-lg text-xs focus:outline-none focus:border-zinc-500 text-white placeholder-zinc-500 transition-colors font-medium"
          />
          <span className="absolute left-2.5 text-zinc-500 pointer-events-none">
            <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.25}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
            </svg>
          </span>
          <span className="absolute right-2 text-[10px] text-zinc-400 bg-zinc-900 border border-zinc-800 px-1.5 py-0.5 rounded font-mono font-bold pointer-events-none">
            ⌘K
          </span>
        </div>

        {/* Notifications Button */}
        <Link
          href="/notifications"
          className="relative p-2 bg-zinc-950 border border-zinc-800 rounded-lg hover:border-zinc-700 text-zinc-400 hover:text-white transition-all active:scale-95"
          title="Thông báo hệ thống"
        >
          <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9" />
          </svg>
          <span className="absolute -top-1 -right-1 w-4 h-4 bg-emerald-500 rounded-full text-[9px] text-black flex items-center justify-center font-bold">
            3
          </span>
        </Link>

      </div>
    </header>
  );
}
