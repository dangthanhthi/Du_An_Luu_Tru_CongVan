'use client';
import { useState, useEffect } from 'react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';

export default function Sidebar() {
  const pathname = usePathname();
  // Sidebar defaults strictly to UNPINNED / COLLAPSED (false)
  const [isExpanded, setIsExpanded] = useState(false);
  const [isHovered, setIsHovered] = useState(false);

  const [currentUser, setCurrentUser] = useState({ name: 'Administrator', email: 'admin@cv.com', avatar: 'AD' });

  useEffect(() => {
    // Ensure default state is unpinned
    const saved = localStorage.getItem('sidebar-expanded');
    if (saved === 'true') {
      setIsExpanded(true);
    } else {
      setIsExpanded(false);
    }

    const userJson = localStorage.getItem('current-user');
    if (userJson) {
      try {
        setCurrentUser(JSON.parse(userJson));
      } catch (e) {
        // ignore
      }
    }
  }, []);

  const toggleSidebar = () => {
    const nextState = !isExpanded;
    setIsExpanded(nextState);
    localStorage.setItem('sidebar-expanded', String(nextState));
  };

  // Expand ONLY when mouse is hovered over the sidebar OR explicitly pinned by user
  const showExpanded = isExpanded || isHovered;

  const menuSections = [
    {
      title: 'OVERVIEW',
      items: [
        { 
          name: 'Dashboard', 
          path: '/dashboard', 
          icon: (
            <svg className="w-4.5 h-4.5 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.75}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M4 6a2 2 0 012-2h2a2 2 0 012 2v4a2 2 0 01-2 2H6a2 2 0 01-2-2V6zM14 6a2 2 0 012-2h2a2 2 0 012 2v4a2 2 0 01-2 2h-2a2 2 0 01-2-2V6zM4 16a2 2 0 012-2h2a2 2 0 012 2v2a2 2 0 01-2 2H6a2 2 0 01-2-2v-2zM14 16a2 2 0 012-2h2a2 2 0 012 2v2a2 2 0 01-2 2h-2a2 2 0 01-2-2v-2z" />
            </svg>
          )
        }
      ]
    },
    {
      title: 'DOCUMENTS',
      items: [
        { 
          name: 'Công văn đến', 
          path: '/documents/incoming', 
          icon: (
            <svg className="w-4.5 h-4.5 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.75}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M19 13h-6v6h-2v-6H5v-2h6V5h2v6h6v2z" />
            </svg>
          )
        },
        { 
          name: 'Công văn đi', 
          path: '/documents/outgoing', 
          icon: (
            <svg className="w-4.5 h-4.5 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.75}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M12 19l9 2-9-18-9 18 9-2zm0 0v-8" />
            </svg>
          )
        },
        { 
          name: 'Công văn nội bộ', 
          path: '/documents/internal', 
          icon: (
            <svg className="w-4.5 h-4.5 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.75}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
            </svg>
          )
        }
      ]
    },
    {
      title: 'ORGANIZATION',
      items: [
        { 
          name: 'Quản lý đối tác', 
          path: '/partners', 
          icon: (
            <svg className="w-4.5 h-4.5 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.75}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 v5m-4 0h4" />
            </svg>
          )
        },
        { 
          name: 'Phòng ban', 
          path: '/departments', 
          icon: (
            <svg className="w-4.5 h-4.5 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.75}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z" />
            </svg>
          )
        },
        { 
          name: 'Người dùng', 
          path: '/users', 
          icon: (
            <svg className="w-4.5 h-4.5 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.75}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M16 7a4 4 0 11-8 0 4 4 0 018 0zM12 14a7 7 0 00-7 7h14a7 7 0 00-7-7z" />
            </svg>
          )
        },
        { 
          name: 'Cài đặt hệ thống', 
          path: '/settings', 
          icon: (
            <svg className="w-4.5 h-4.5 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={1.75}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M10.325 4.317c.426-1.756 2.924-1.756 3.35 0a1.724 1.724 0 002.573 1.066c1.543-.94 3.31.826 2.37 2.37a1.724 1.724 0 001.065 2.572c1.756.426 1.756 2.924 0 3.35a1.724 1.724 0 00-1.066 2.573c.94 1.543-.826 3.31-2.37 2.37a1.724 1.724 0 00-2.572 1.065c-.426 1.756-2.924 1.756-3.35 0a1.724 1.724 0 00-2.573-1.066c-1.543.94-3.31-.826-2.37-2.37a1.724 1.724 0 00-1.065-2.572c-1.756-.426-1.756-2.924 0-3.35a1.724 1.724 0 001.066-2.573c-.94-1.543.826-3.31 2.37-2.37.996.608 2.296.07 2.572-1.065z" />
              <path strokeLinecap="round" strokeLinejoin="round" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
            </svg>
          )
        }
      ]
    }
  ];

  return (
    <aside
      onMouseEnter={() => setIsHovered(true)}
      onMouseLeave={() => setIsHovered(false)}
      className={`h-screen bg-black/95 backdrop-blur-xl border-r border-zinc-800 transition-all duration-300 ease-in-out flex flex-col fixed left-0 top-0 z-50 ${
        showExpanded ? 'w-64 shadow-2xl' : 'w-20'
      }`}
    >
      {/* Brand Logo Area */}
      <div className="p-4 border-b border-zinc-800 flex items-center gap-3 h-14 shrink-0 overflow-hidden">
        <div className="w-9 h-9 rounded-xl bg-zinc-950 border border-zinc-800 flex items-center justify-center shrink-0 shadow-sm transition-transform hover:scale-105 hover:border-zinc-700">
          <svg className="w-5 h-5 text-zinc-300" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={1.5} strokeLinecap="round" strokeLinejoin="round">
            <path d="M12 3.5L3.5 20.5L12 16.5L20.5 20.5L12 3.5Z" />
            <line x1="12" y1="16.5" x2="12" y2="8" />
          </svg>
        </div>
        {showExpanded && (
          <div className="flex flex-col min-w-0 flex-1 animate-fade-in">
            <div className="flex items-center gap-1.5">
              <span className="text-sm font-extrabold text-white tracking-tight truncate">CV OS</span>
              <span className="text-[9px] bg-zinc-900 border border-zinc-800 text-zinc-400 px-1.5 py-0.2 rounded font-mono font-bold">
                PRO
              </span>
            </div>
            <span className="text-[10px] text-zinc-500 font-medium truncate">Quản lý Công văn</span>
          </div>
        )}
      </div>

      {/* Navigation List */}
      <div className="flex-1 overflow-y-auto px-3 py-4 space-y-6">
        {menuSections.map((section, idx) => (
          <div key={idx} className="space-y-1">
            {showExpanded ? (
              <h4 className="px-3 text-[10px] font-bold text-zinc-500 tracking-widest uppercase mb-2 animate-fade-in">
                {section.title}
              </h4>
            ) : (
              <div className="h-2"></div>
            )}
            {section.items.map((item) => {
              const isActive = pathname === item.path || pathname.startsWith(item.path + '/');
              return (
                <Link
                  key={item.path}
                  href={item.path}
                  className={`flex items-center gap-3 px-3 py-2.5 rounded-lg text-xs font-medium transition-all duration-150 relative group ${
                    showExpanded ? '' : 'justify-center'
                  } ${
                    isActive
                      ? 'bg-zinc-900 border border-zinc-800 text-white font-semibold shadow-sm'
                      : 'text-zinc-400 hover:text-white hover:bg-zinc-900/50'
                  }`}
                  title={!showExpanded ? item.name : undefined}
                >
                  <span className={`${isActive ? 'text-white' : 'text-zinc-500 group-hover:text-zinc-300'}`}>
                    {item.icon}
                  </span>
                  {showExpanded && <span className="truncate animate-fade-in">{item.name}</span>}
                  {isActive && !showExpanded && (
                    <span className="absolute left-0 top-1/2 -translate-y-1/2 w-1 h-5 bg-white rounded-r"></span>
                  )}
                </Link>
              );
            })}
          </div>
        ))}
      </div>

      {/* Profile & Footer */}
      <div className="p-3 border-t border-zinc-800 flex flex-col gap-2 shrink-0 bg-black">
        <div className={`flex items-center justify-between min-w-0 ${showExpanded ? '' : 'justify-center'}`}>
          <div className={`flex items-center gap-2.5 min-w-0 ${showExpanded ? 'flex-1' : ''}`}>
            <button 
              onClick={() => {
                localStorage.removeItem('das-token');
                localStorage.removeItem('current-user');
                window.location.href = '/login';
              }}
              className="w-8 h-8 rounded-lg bg-zinc-900 border border-zinc-800 text-white flex items-center justify-center font-bold text-xs shrink-0 cursor-pointer hover:border-zinc-700 transition-colors"
              title="Click để Đăng xuất"
            >
              {currentUser.avatar}
            </button>
            {showExpanded && (
              <div className="min-w-0 flex-1 animate-fade-in">
                <h4 className="text-xs font-bold text-white truncate">{currentUser.name}</h4>
                <p className="text-[10px] text-zinc-500 font-medium truncate">{currentUser.email}</p>
              </div>
            )}
          </div>
          {showExpanded && (
            <button 
              onClick={() => {
                localStorage.removeItem('das-token');
                localStorage.removeItem('current-user');
                window.location.href = '/login';
              }}
              className="p-1.5 text-zinc-500 hover:text-red-400 rounded-lg hover:bg-zinc-900 transition-colors cursor-pointer shrink-0 animate-fade-in"
              title="Đăng xuất khỏi hệ thống"
            >
              <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M17 16l4-4m0 0l-4-4m4 4H7m6 4v1a3 3 0 01-3 3H6a3 3 0 01-3-3V7a3 3 0 013-3h4a3 3 0 013 3v1" />
              </svg>
            </button>
          )}
        </div>

        {/* Sidebar Pin Button */}
        {showExpanded && (
          <button
            onClick={toggleSidebar}
            className="w-full mt-1 py-1.5 bg-zinc-950 hover:bg-zinc-900 border border-zinc-800 rounded-lg text-zinc-500 hover:text-zinc-300 flex items-center justify-center transition-colors cursor-pointer text-xs gap-2 animate-fade-in"
            title={isExpanded ? "Bỏ ghim Sidebar (Tự động ẩn)" : "Cố định ghim Sidebar mở rộng"}
          >
            <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              {isExpanded ? (
                <path strokeLinecap="round" strokeLinejoin="round" d="M11 19l-7-7 7-7M17 19l-7-7 7-7" />
              ) : (
                <path strokeLinecap="round" strokeLinejoin="round" d="M13 5l7 7-7 7M7 5l7 7-7 7" />
              )}
            </svg>
            <span className="text-[10px] font-medium">{isExpanded ? 'Bỏ cố định' : 'Cố định Sidebar'}</span>
          </button>
        )}
      </div>
    </aside>
  );
}
