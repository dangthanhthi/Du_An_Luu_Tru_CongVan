'use client';
import React, { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';

interface UserProfile {
  name: string;
  email: string;
  avatar: string;
  department?: string;
  role?: string;
}

export default function LoginPage() {
  const router = useRouter();
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const [loadingMsg, setLoadingMsg] = useState('');
  const [mounted, setMounted] = useState(false);

  useEffect(() => {
    setMounted(true);
  }, []);

  const handleLoginSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setError('');

    const lowerUser = username.trim().toLowerCase();
    const pass = password.trim();

    let userProfile: UserProfile | null = null;
    let token = 'mock-user-token';

    if (lowerUser === 'admin' && pass === '123456') {
      userProfile = {
        name: 'Quản trị viên Hệ thống',
        email: 'admin@taskmanager.com',
        avatar: 'AD',
        department: 'Phòng CNTT',
        role: 'Admin'
      };
      token = 'mock-admin-token';
    } else if (lowerUser === 'giamdoc' && pass === '123456') {
      userProfile = {
        name: 'Trần Văn Giám Đốc',
        email: 'giamdoc@taskmanager.com',
        avatar: 'GD',
        department: 'Ban Giám đốc',
        role: 'Giám đốc'
      };
      token = 'mock-giamdoc-token';
    } else if (lowerUser === 'truongphong' && pass === '123456') {
      userProfile = {
        name: 'Nguyễn Văn Trưởng Phòng',
        email: 'truongphong.hc@taskmanager.com',
        avatar: 'TP',
        department: 'Phòng Hành chính',
        role: 'Trưởng phòng'
      };
      token = 'mock-manager-token';
    } else if (lowerUser === 'nhanvien' && pass === '123456') {
      userProfile = {
        name: 'Lê Thị Văn Thư',
        email: 'vanthu.hc@taskmanager.com',
        avatar: 'VT',
        department: 'Phòng Hành chính',
        role: 'Chuyên viên'
      };
      token = 'mock-staff-token';
    }

    if (userProfile) {
      setLoading(true);
      setLoadingMsg(`Đang xác thực thông tin đăng nhập...`);
      setTimeout(() => {
        localStorage.setItem('das-token', token);
        localStorage.setItem('current-user', JSON.stringify(userProfile));
        router.push('/dashboard');
      }, 1000);
    } else {
      setError('Sai tài khoản hoặc mật khẩu. Vui lòng sử dụng tài khoản doanh nghiệp gợi ý bên dưới.');
    }
  };

  const triggerSSOLogin = (provider: string) => {
    let loginUrl = '';
    let userProfile: UserProfile = {
      name: 'Trần Văn Giám Đốc',
      email: 'dangthanhthi213@gmail.com',
      avatar: 'GD',
      department: 'Ban Giám đốc',
      role: 'Giám đốc'
    };

    if (provider === 'Google') {
      loginUrl = 'https://accounts.google.com/InteractiveLogin';
      userProfile = {
        name: 'Trần Văn Giám Đốc',
        email: 'dangthanhthi213@gmail.com',
        avatar: 'GD',
        department: 'Ban Giám đốc',
        role: 'Giám đốc'
      };
    } else if (provider === 'GitHub') {
      loginUrl = 'https://github.com/login';
      userProfile = {
        name: 'Quản trị viên Hệ thống',
        email: 'dangthanhthi@github.com',
        avatar: 'AD',
        department: 'Phòng CNTT',
        role: 'Admin'
      };
    } else {
      loginUrl = 'https://appleid.apple.com/auth/authorize';
    }

    const width = 500;
    const height = 600;
    const left = typeof window !== 'undefined' ? window.screen.width / 2 - width / 2 : 100;
    const top = typeof window !== 'undefined' ? window.screen.height / 2 - height / 2 : 100;
    
    let popup: Window | null = null;
    try {
      popup = window.open(
        loginUrl,
        `SSO_POPUP_${provider}`,
        `width=${width},height=${height},left=${left},top=${top},resizable=yes,scrollbars=yes`
      );
    } catch (e) {}

    setTimeout(() => {
      try {
        if (popup && !popup.closed) {
          popup.close();
        }
      } catch (e) {}
    }, 1200);

    setLoading(true);
    setLoadingMsg(`Kết nối xác thực với ${provider} thành công!`);
    setTimeout(() => {
      localStorage.setItem('das-token', `mock-${provider.toLowerCase()}-token`);
      localStorage.setItem('current-user', JSON.stringify(userProfile));
      router.push('/dashboard');
    }, 1500);
  };

  if (!mounted) return null;

  return (
    <div className="min-h-screen bg-black flex flex-col justify-center items-center px-4 relative overflow-hidden select-none">
      {/* Background gradients */}
      <div className="absolute top-[-10%] left-[-10%] w-[40%] h-[40%] rounded-full bg-zinc-900/40 blur-[120px] pointer-events-none"></div>
      <div className="absolute bottom-[-10%] right-[-10%] w-[45%] h-[45%] rounded-full bg-zinc-900/30 blur-[130px] pointer-events-none"></div>

      {loading && (
        <div className="absolute inset-0 bg-black/95 z-50 flex flex-col items-center justify-center space-y-4 backdrop-blur-sm">
          <div className="relative w-12 h-12 flex items-center justify-center">
            <span className="absolute w-12 h-12 rounded-full border-2 border-zinc-800 border-t-white animate-spin"></span>
            <span className="text-[10px] font-bold text-zinc-400">CV</span>
          </div>
          <p className="text-xs text-zinc-400 font-bold uppercase tracking-wider animate-pulse">{loadingMsg}</p>
        </div>
      )}

      {/* Main Login Card */}
      <div className="w-full max-w-[390px] p-8 bg-zinc-950 border border-zinc-900 rounded-2xl shadow-2xl relative z-10 transition-all duration-300">
        <div className="text-center space-y-3 mb-8">
          <div className="inline-flex items-center gap-1 px-3 py-1 bg-zinc-900 border border-zinc-800 rounded-full">
            <span className="w-2 h-2 rounded-full bg-emerald-500 animate-pulse"></span>
            <span className="text-[9px] font-bold text-zinc-400 uppercase tracking-widest">Enterprise Edition</span>
          </div>
          <h1 className="text-2xl font-extrabold text-white tracking-tight">Hệ thống Lưu trữ Công văn</h1>
          <p className="text-xs text-zinc-500 font-medium">Đăng nhập tài khoản nội bộ doanh nghiệp</p>
        </div>

        <form onSubmit={handleLoginSubmit} className="space-y-4">
          {error && (
            <div className="p-3 bg-red-950/20 border border-red-900/40 text-red-400 text-[11px] rounded-lg font-semibold leading-relaxed">
              ⚠️ {error}
            </div>
          )}

          <div>
            <input
              type="text"
              value={username}
              onChange={(e) => {
                setUsername(e.target.value);
                setError('');
              }}
              required
              placeholder="Tên đăng nhập nội bộ"
              className="w-full px-4 py-2.5 bg-black border border-zinc-800 focus:border-zinc-500 rounded-lg text-xs text-white placeholder-zinc-650 transition-colors font-semibold focus:outline-none"
            />
          </div>

          <div className="relative">
            <input
              type="password"
              value={password}
              onChange={(e) => {
                setPassword(e.target.value);
                setError('');
              }}
              required
              placeholder="Mật khẩu"
              className="w-full px-4 py-2.5 bg-black border border-zinc-800 focus:border-zinc-500 rounded-lg text-xs text-white placeholder-zinc-650 transition-colors font-semibold focus:outline-none"
            />
          </div>

          <button
            type="submit"
            className="w-full py-2.5 bg-white hover:bg-zinc-200 text-black font-extrabold rounded-lg text-xs transition-all cursor-pointer shadow-sm active:scale-[0.98] transform"
          >
            ĐĂNG NHẬP HỆ THỐNG
          </button>
        </form>

        {/* SSO Area */}
        <div className="mt-6 space-y-4">
          <div className="relative flex items-center justify-center my-4">
            <div className="absolute inset-0 flex items-center">
              <div className="w-full border-t border-zinc-900"></div>
            </div>
            <span className="relative px-3 bg-zinc-950 text-[10px] font-bold text-zinc-555 uppercase tracking-widest">
              Hoặc đăng nhập nhanh bằng
            </span>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <button
              onClick={() => triggerSSOLogin('Google')}
              className="flex items-center justify-center gap-2 py-2 bg-black border border-zinc-900 hover:bg-zinc-900/50 hover:border-zinc-800 text-xs font-semibold text-zinc-300 rounded-lg cursor-pointer transition-colors"
            >
              <svg className="w-4 h-4" viewBox="0 0 24 24" fill="currentColor">
                <path d="M12.24 10.285V13.4h6.887c-.275 1.565-1.88 4.604-6.887 4.604-4.33 0-7.866-3.577-7.866-8s3.536-8 7.866-8c2.46 0 4.105 1.025 5.047 1.926l2.427-2.334C17.955 2.192 15.34 1 12.24 1 5.258 1 0 6.258 0 12.24s5.258 11.24 12.24 11.24c7.29 0 12.134-5.118 12.134-12.36 0-.832-.089-1.467-.2-1.835H12.24z"/>
              </svg>
              <span>Google</span>
            </button>

            <button
              onClick={() => triggerSSOLogin('GitHub')}
              className="flex items-center justify-center gap-2 py-2 bg-black border border-zinc-900 hover:bg-zinc-900/50 hover:border-zinc-800 text-xs font-semibold text-zinc-300 rounded-lg cursor-pointer transition-colors"
            >
              <svg className="w-4 h-4" viewBox="0 0 24 24" fill="currentColor">
                <path d="M12 0C5.37 0 0 5.37 0 12c0 5.3 3.438 9.8 8.205 11.385.6.11.82-.26.82-.577v-2.234c-3.338.724-4.042-1.61-4.042-1.61C4.422 18.07 3.633 17.7 3.633 17.7c-1.087-.744.084-.729.084-.729 1.205.084 1.838 1.236 1.838 1.236 1.07 1.835 2.809 1.305 3.495.998.108-.776.417-1.305.76-1.605-2.665-.3-5.466-1.332-5.466-5.93 0-1.31.465-2.38 1.235-3.22-.135-.303-.54-1.523.105-3.176 0 0 1.005-.322 3.3 1.23.96-.267 1.98-.399 3-.405 1.02.006 2.04.138 3 .405 2.28-1.552 3.285-1.23 3.285-1.23.645 1.653.24 2.873.12 3.176.765.84 1.23 1.91 1.23 3.22 0 4.61-2.805 5.625-5.475 5.92.43.372.82 1.102.82 2.222v3.293c0 .319.22.694.825.576C20.565 21.795 24 17.3 24 12c0-6.63-5.37-12-12-12z"/>
              </svg>
              <span>GitHub</span>
            </button>
          </div>
        </div>

        {/* Enterprise Accounts Quick Helper Panel */}
        <div className="mt-6 p-4 bg-zinc-950 border border-zinc-900 rounded-xl space-y-3">
          <p className="text-[10px] font-bold text-zinc-500 uppercase tracking-widest text-center">Tài khoản thử nghiệm Doanh nghiệp</p>
          <div className="grid grid-cols-2 gap-2 text-[10px]">
            <button
              type="button"
              onClick={() => { setUsername('admin'); setPassword('123456'); }}
              className="p-2 border border-zinc-900 bg-zinc-900/40 hover:bg-zinc-900/80 hover:border-zinc-800 rounded-lg text-left text-zinc-300 font-semibold cursor-pointer transition-all active:scale-[0.97]"
            >
              <div className="text-white font-bold">🛠️ Admin Hệ thống</div>
              <div className="text-zinc-500 font-mono mt-0.5">admin / 123456</div>
            </button>
            <button
              type="button"
              onClick={() => { setUsername('giamdoc'); setPassword('123456'); }}
              className="p-2 border border-zinc-900 bg-zinc-900/40 hover:bg-zinc-900/80 hover:border-zinc-800 rounded-lg text-left text-zinc-300 font-semibold cursor-pointer transition-all active:scale-[0.97]"
            >
              <div className="text-white font-bold">👔 Giám đốc (Director)</div>
              <div className="text-zinc-500 font-mono mt-0.5">giamdoc / 123456</div>
            </button>
            <button
              type="button"
              onClick={() => { setUsername('truongphong'); setPassword('123456'); }}
              className="p-2 border border-zinc-900 bg-zinc-900/40 hover:bg-zinc-900/80 hover:border-zinc-800 rounded-lg text-left text-zinc-300 font-semibold cursor-pointer transition-all active:scale-[0.97]"
            >
              <div className="text-white font-bold">💼 Trưởng phòng HC</div>
              <div className="text-zinc-500 font-mono mt-0.5">truongphong / 123456</div>
            </button>
            <button
              type="button"
              onClick={() => { setUsername('nhanvien'); setPassword('123456'); }}
              className="p-2 border border-zinc-900 bg-zinc-900/40 hover:bg-zinc-900/80 hover:border-zinc-800 rounded-lg text-left text-zinc-300 font-semibold cursor-pointer transition-all active:scale-[0.97]"
            >
              <div className="text-white font-bold">📄 Văn thư / Nhân viên</div>
              <div className="text-zinc-500 font-mono mt-0.5">nhanvien / 123456</div>
            </button>
          </div>
        </div>
      </div>

      {/* Vercel styled terms disclaimer */}
      <div className="text-center mt-6">
        <p className="text-[10px] text-zinc-650 font-semibold leading-relaxed max-w-[340px] mx-auto">
          Hệ thống lưu trữ công văn nội bộ doanh nghiệp. Mọi hành vi truy cập trái phép sẽ bị xử lý theo quy định của pháp luật.
        </p>
      </div>
    </div>
  );
}
