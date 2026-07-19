'use client';
import { useTheme } from '@/context/ThemeContext';
import Link from 'next/link';

interface HeaderProps {
  title: string;
}

export default function Header({ title }: HeaderProps) {
  const { theme, toggleTheme } = useTheme();

  return (
    <header className="h-16 border-b border-border bg-card flex items-center justify-between px-8 transition-colors duration-200">
      <h1 className="text-lg font-bold text-foreground">{title}</h1>
      
      <div className="flex items-center gap-5">
        <div className="relative flex items-center">
          <input
            type="text"
            placeholder="Tìm kiếm công văn, tài liệu..."
            className="w-64 pl-10 pr-10 py-2 border border-border bg-muted rounded-full text-xs focus:outline-none focus:border-accent focus:bg-card text-foreground transition-all duration-200 font-semibold"
          />
          <span className="absolute left-3.5 text-muted-foreground pointer-events-none">
            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.25}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
            </svg>
          </span>
          <span className="absolute right-3.5 text-muted-foreground hover:text-foreground cursor-pointer transition-colors" title="Tìm kiếm bằng hình ảnh / OCR">
            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.25}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M3 9V6a3 3 0 013-3h3m6 0h3a3 3 0 013 3v3m0 6v3a3 3 0 01-3 3h-3m-6 0H6a3 3 0 01-3-3v-3" />
              <circle cx="12" cy="12" r="3.2" />
            </svg>
          </span>
        </div>

        <button
          onClick={toggleTheme}
          className="p-2 border border-border rounded-lg hover:bg-muted transition-all active:scale-95 duration-150 cursor-pointer"
        >
          {theme === 'light' ? (
            <svg className="w-4 h-4 text-zinc-700" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M20.354 15.354A9 9 0 018.646 3.646 9.003 9.003 0 0012 21a9.003 9.003 0 008.354-5.646z" />
            </svg>
          ) : (
            <svg className="w-4 h-4 text-amber-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M12 3v1m0 16v1m9-9h-1M4 12H3m15.364-6.364l-.707.707M6.343 17.657l-.707.707m12.728 0l-.707-.707M6.343 6.343l-.707-.707M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
            </svg>
          )}
        </button>

        <Link
          href="/notifications"
          className="relative p-2 border border-border rounded-lg hover:bg-muted transition-all active:scale-95 duration-150"
        >
          <svg className="w-4 h-4 text-foreground" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9" />
          </svg>
          <span className="absolute -top-1 -right-1 w-4.5 h-4.5 bg-accent rounded-full text-[9px] text-white flex items-center justify-center font-bold">
            3
          </span>
        </Link>
      </div>
    </header>
  );
}
