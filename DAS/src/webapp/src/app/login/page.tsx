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
  const [mode, setMode] = useState<'login' | 'signup' | 'oauth_linking'>('login');
  
  // Login / Signup Form state
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const [loadingMsg, setLoadingMsg] = useState('');

  // OAuth linking state (for new Google/GitHub accounts)
  const [oauthProvider, setOauthProvider] = useState('');
  const [fullName, setFullName] = useState('');
  const [oauthEmail, setOauthEmail] = useState('');
  const [dept, setDept] = useState('Phòng Hành chính');
  const [role, setRole] = useState('Chuyên viên');

  // Seed default admin and search helper
  const handleLoginSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setError('');

    // Fetch existing registered users from localStorage
    const savedUsersStr = localStorage.getItem('das-registered-users');
    let registeredUsers: Record<string, { pass: string; profile: UserProfile }> = {};
    if (savedUsersStr) {
      try {
        registeredUsers = JSON.parse(savedUsersStr);
      } catch (e) {}
    }

    // Default admin seed
    if (username === 'admin' && password === '123456') {
      setLoading(true);
      setLoadingMsg('Đang xác thực tài khoản quản trị...');
      setTimeout(() => {
        localStorage.setItem('das-token', 'mock-jwt-token');
        localStorage.setItem('current-user', JSON.stringify({
          name: 'Administrator',
          email: 'admin@cv.com',
          avatar: 'AD',
          department: 'Ban Giám đốc',
          role: 'Giám đốc'
        }));
        router.push('/dashboard');
      }, 1000);
      return;
    }

    // Check custom registered users
    const matchedUser = registeredUsers[username.toLowerCase()];
    if (matchedUser && matchedUser.pass === password) {
      setLoading(true);
      setLoadingMsg(`Đang đăng nhập dưới danh nghĩa ${matchedUser.profile.name}...`);
      setTimeout(() => {
        localStorage.setItem('das-token', 'mock-user-token');
        localStorage.setItem('current-user', JSON.stringify(matchedUser.profile));
        router.push('/dashboard');
      }, 1000);
    } else {
      setError('Sai tài khoản hoặc mật khẩu (Thử: admin / 123456 hoặc tạo tài khoản mới bên dưới)');
    }
  };

  const handleSignupSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setError('');

    if (username.toLowerCase() === 'admin') {
      setError('Tài khoản "admin" đã được đăng ký mặc định trên hệ thống.');
      return;
    }

    setLoading(true);
    setLoadingMsg('Đang khởi tạo tài khoản mới...');

    setTimeout(() => {
      // Save user to simulated DB in localStorage
      const savedUsersStr = localStorage.getItem('das-registered-users') || '{}';
      let registeredUsers = {};
      try {
        registeredUsers = JSON.parse(savedUsersStr);
      } catch (e) {}

      const cleanUsername = username.trim().toLowerCase();
      const initials = cleanUsername.substring(0, 2).toUpperCase() || 'US';
      const userProfile: UserProfile = {
        name: username.split('@')[0],
        email: cleanUsername.includes('@') ? cleanUsername : `${cleanUsername}@cv.com`,
        avatar: initials,
        department: 'Phòng Hành chính',
        role: 'Chuyên viên'
      };

      // Save user record
      (registeredUsers as any)[cleanUsername] = {
        pass: password,
        profile: userProfile
      };
      localStorage.setItem('das-registered-users', JSON.stringify(registeredUsers));

      // Auto log in as the newly created user
      localStorage.setItem('das-token', 'mock-user-token');
      localStorage.setItem('current-user', JSON.stringify(userProfile));
      
      setLoading(false);
      router.push('/dashboard');
    }, 1200);
  };

  const triggerSSOLogin = (provider: string) => {
    setLoading(true);
    setLoadingMsg(`Đang kết nối cổng xác thực ${provider}...`);

    let loginUrl = '';
    if (provider === 'Google') {
      loginUrl = 'https://accounts.google.com/InteractiveLogin';
    } else if (provider === 'GitHub') {
      loginUrl = 'https://github.com/login';
    } else if (provider === 'Apple') {
      loginUrl = 'https://appleid.apple.com/auth/authorize';
    } else {
      loginUrl = 'https://accounts.google.com';
    }

    const width = 500;
    const height = 600;
    const left = window.screen.width / 2 - width / 2;
    const top = window.screen.height / 2 - height / 2;
    
    const popup = window.open(
      loginUrl,
      `SSO_${provider}`,
      `width=${width},height=${height},left=${left},top=${top},resizable=yes,scrollbars=yes`
    );

    // Watch popup closure
    const checkClosed = setInterval(() => {
      if (!popup || popup.closed) {
        clearInterval(checkClosed);
        
        // Popup closed! Check if this provider was already linked before
        const linkedUserStr = localStorage.getItem(`das-sso-${provider.toLowerCase()}`);
        if (linkedUserStr) {
          // User already exists! Log in directly
          setLoadingMsg(`Đã nhận diện tài khoản liên kết! Đang đăng nhập...`);
          setTimeout(() => {
            localStorage.setItem('das-token', `mock-${provider.toLowerCase()}-token`);
            localStorage.setItem('current-user', linkedUserStr);
            router.push('/dashboard');
          }, 800);
        } else {
          // User does not have an account! We force registration/linking.
          setLoading(false);
          setOauthProvider(provider);
          setFullName(provider === 'Google' ? 'Nguyễn Văn Google' : 'Trần Văn GitHub');
          setOauthEmail(provider === 'Google' ? 'google.user@gmail.com' : 'github.user@github.com');
          setMode('oauth_linking');
        }
      }
    }, 500);
  };

  const handleOAuthLinkingSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setLoadingMsg('Đang tạo hồ sơ và liên kết tài khoản...');

    setTimeout(() => {
      const initials = fullName.split(' ').map(w => w[0]).join('').substring(0, 2).toUpperCase() || 'SS';
      const userProfile: UserProfile = {
        name: fullName,
        email: oauthEmail,
        avatar: initials,
        department: dept,
        role: role
      };

      // Save linkage so next time they log in directly
      localStorage.setItem(`das-sso-${oauthProvider.toLowerCase()}`, JSON.stringify(userProfile));
      
      // Add to registered users
      const savedUsersStr = localStorage.getItem('das-registered-users') || '{}';
      let registeredUsers = {};
      try {
        registeredUsers = JSON.parse(savedUsersStr);
      } catch (e) {}
      
      const cleanUsername = oauthEmail.trim().toLowerCase();
      (registeredUsers as any)[cleanUsername] = {
        pass: 'sso-account-no-pass',
        profile: userProfile
      };
      localStorage.setItem('das-registered-users', JSON.stringify(registeredUsers));

      // Log in as new user
      localStorage.setItem('das-token', `mock-${oauthProvider.toLowerCase()}-token`);
      localStorage.setItem('current-user', JSON.stringify(userProfile));

      setLoading(false);
      router.push('/dashboard');
    }, 1200);
  };

  return (
    <div className="w-full max-w-[400px] transition-all duration-300 relative">
      
      {/* Loading Overlay */}
      {loading && (
        <div className="absolute inset-0 bg-black/90 backdrop-blur-sm z-50 flex flex-col items-center justify-center rounded-2xl border border-zinc-800 p-8 text-center">
          <div className="w-10 h-10 border-2 border-zinc-700 border-t-white rounded-full animate-spin mb-4"></div>
          <p className="text-sm font-semibold text-zinc-300">{loadingMsg}</p>
        </div>
      )}

      {/* Main card body */}
      <div className="p-8 bg-zinc-950 border border-zinc-900 rounded-2xl shadow-[0_8px_30px_rgba(0,0,0,0.5)]">
        
        {/* CV Vector Logo */}
        <div className="flex justify-center mb-6">
          <div className="w-11 h-11 rounded-xl bg-gradient-to-br from-emerald-600 to-teal-500 text-white flex items-center justify-center shadow-md shadow-emerald-950/30 transition-transform hover:scale-105 duration-200">
            <svg className="w-6 h-6" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5} strokeLinecap="round" strokeLinejoin="round">
              <path d="M14.5 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V7.5L14.5 2z" />
              <polyline points="14 2 14 8 20 8" />
              <line x1="16" y1="13" x2="8" y2="13" />
              <line x1="16" y1="17" x2="8" y2="17" />
              <line x1="10" y1="9" x2="8" y2="9" />
            </svg>
          </div>
        </div>

        {/* View switching logic */}
        {mode === 'login' && (
          <div>
            <h2 className="text-2xl font-semibold text-center text-white tracking-tight mb-6">
              Đăng nhập vào CV
            </h2>

            <form onSubmit={handleLoginSubmit} className="space-y-4">
              {error && (
                <div className="p-3 bg-red-950/20 border border-red-900/40 text-red-400 text-xs rounded-lg font-semibold">
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
                  placeholder="Địa chỉ Email hoặc tài khoản"
                  className="w-full px-4 py-2.5 bg-black border border-zinc-800 rounded-lg text-sm focus:outline-none focus:border-zinc-500 focus:ring-1 focus:ring-zinc-500 text-white placeholder-zinc-500 transition-colors font-medium"
                />
              </div>

              <div>
                <input
                  type="password"
                  value={password}
                  onChange={(e) => {
                    setPassword(e.target.value);
                    setError('');
                  }}
                  required
                  placeholder="Mật khẩu"
                  className="w-full px-4 py-2.5 bg-black border border-zinc-800 rounded-lg text-sm focus:outline-none focus:border-zinc-500 focus:ring-1 focus:ring-zinc-500 text-white placeholder-zinc-500 transition-colors font-semibold"
                />
              </div>

              <button
                type="submit"
                className="w-full py-2.5 bg-white hover:bg-zinc-200 text-black font-semibold rounded-lg text-sm transition-all cursor-pointer shadow-sm active:scale-[0.98] transform"
              >
                Đăng nhập
              </button>
            </form>

            {/* Divider */}
            <div className="flex items-center my-6">
              <div className="flex-1 border-t border-zinc-900"></div>
              <span className="px-3 text-xs text-zinc-500 font-bold uppercase tracking-widest">Hoặc</span>
              <div className="flex-1 border-t border-zinc-900"></div>
            </div>

            {/* SSO / OAuth Options */}
            <div className="space-y-2">
              <button
                type="button"
                onClick={() => triggerSSOLogin('Google')}
                className="w-full py-2.5 bg-black border border-zinc-900 rounded-lg text-sm text-zinc-300 font-medium hover:bg-zinc-900/50 hover:border-zinc-800 hover:text-white transition-all cursor-pointer flex items-center justify-center gap-3 active:scale-[0.99]"
              >
                <svg className="w-4 h-4 shrink-0" viewBox="0 0 24 24">
                  <path fill="#4285F4" d="M23.745 12.27c0-.7-.06-1.4-.19-2.07H12v3.92h6.69a5.74 5.74 0 0 1-2.48 3.77v3.13h3.92c2.29-2.11 3.61-5.21 3.61-8.75z"/>
                  <path fill="#34A853" d="M12 24c3.24 0 5.97-1.08 7.96-2.91l-3.92-3.13c-1.08.72-2.48 1.15-4.04 1.15-3.11 0-5.74-2.11-6.68-4.96H1.21v3.23C3.18 21.3 7.27 24 12 24z"/>
                  <path fill="#FBBC05" d="M5.32 14.15A7.16 7.16 0 0 1 5 12c0-.75.13-1.48.32-2.15V6.62H1.21A11.96 11.96 0 0 0 0 12c0 1.92.45 3.74 1.21 5.38l4.11-3.23z"/>
                  <path fill="#EA4335" d="M12 4.75c1.77 0 3.35.61 4.6 1.8l3.42-3.42C17.96 1.19 15.24 0 12 0 7.27 0 3.18 2.7 1.21 6.62l4.11 3.23c.94-2.85 3.57-4.96 6.68-4.96z"/>
                </svg>
                <span>Tiếp tục với Google</span>
              </button>

              <button
                type="button"
                onClick={() => triggerSSOLogin('GitHub')}
                className="w-full py-2.5 bg-black border border-zinc-900 rounded-lg text-sm text-zinc-300 font-medium hover:bg-zinc-900/50 hover:border-zinc-800 hover:text-white transition-all cursor-pointer flex items-center justify-center gap-3 active:scale-[0.99]"
              >
                <svg className="w-4 h-4 shrink-0" fill="currentColor" viewBox="0 0 24 24">
                  <path fillRule="evenodd" clipRule="evenodd" d="M12 2C6.477 2 2 6.484 2 12.017c0 4.425 2.865 8.18 6.839 9.504.5.092.682-.217.682-.483 0-.237-.008-.868-.013-1.703-2.782.605-3.369-1.343-3.369-1.343-.454-1.158-1.11-1.466-1.11-1.466-.908-.62.069-.608.069-.608 1.003.07 1.531 1.032 1.531 1.032.892 1.53 2.341 1.088 2.91.832.092-.647.35-1.088.636-1.338-2.22-.253-4.555-1.113-4.555-4.951 0-1.093.39-1.988 1.029-2.688-.103-.253-.446-1.272.098-2.65 0 0 .84-.27 2.75 1.026A9.564 9.564 0 0112 6.844c.85.004 1.705.115 2.504.337 1.909-1.296 2.747-1.027 2.747-1.027.546 1.379.202 2.398.1 2.651.64.7 1.028 1.595 1.028 2.688 0 3.848-2.339 4.695-4.566 4.943.359.309.678.92.678 1.855 0 1.338-.012 2.419-.012 2.747 0 .268.18.58.688.482C19.138 20.193 22 16.44 22 12.017 22 6.484 17.522 2 12 2z" />
                </svg>
                <span>Tiếp tục với GitHub</span>
              </button>

              <button
                type="button"
                onClick={() => triggerSSOLogin('Apple')}
                className="w-full py-2.5 bg-black border border-zinc-900 rounded-lg text-sm text-zinc-300 font-medium hover:bg-zinc-900/50 hover:border-zinc-800 hover:text-white transition-all cursor-pointer flex items-center justify-center gap-3 active:scale-[0.99]"
              >
                <svg className="w-4 h-4 shrink-0" fill="currentColor" viewBox="0 0 24 24">
                  <path d="M18.71 19.5c-.83 1.24-1.71 2.45-3.05 2.47-1.34.03-1.77-.79-3.29-.79-1.53 0-2 .77-3.27.82-1.31.05-2.3-1.32-3.14-2.53C4.25 17 2.94 12.45 4.7 9.39c.87-1.52 2.43-2.48 4.12-2.51 1.28-.02 2.5.87 3.29.87.78 0 2.26-1.07 3.81-.91.65.03 2.47.26 3.64 1.98-.09.06-2.17 1.28-2.15 3.81.03 3.02 2.65 4.03 2.68 4.04-.03.07-.42 1.44-1.38 2.83M15.97 4.17c.66-.81 1.11-1.93.99-3.06-1 .04-2.22.67-2.94 1.5-.64.74-1.2 1.88-1.08 2.99 1.12.09 2.27-.58 3.03-1.43z"/>
                </svg>
                <span>Tiếp tục với Apple</span>
              </button>
            </div>

            {/* Bottom Redirect */}
            <div className="text-center mt-8 pt-4 border-t border-zinc-900">
              <p className="text-xs text-zinc-400 font-medium">
                Bạn chưa có tài khoản?{' '}
                <button
                  type="button"
                  onClick={() => setMode('signup')}
                  className="text-white hover:underline font-bold cursor-pointer transition-colors"
                >
                  Đăng ký
                </button>
              </p>
            </div>
          </div>
        )}

        {mode === 'signup' && (
          <div>
            <h2 className="text-2xl font-semibold text-center text-white tracking-tight mb-4">
              Tạo tài khoản mới
            </h2>
            <p className="text-[11px] text-zinc-500 font-semibold text-center uppercase tracking-wider mb-6">
              Đăng ký tài khoản CV thủ công
            </p>

            <form onSubmit={handleSignupSubmit} className="space-y-4">
              {error && (
                <div className="p-3 bg-red-950/20 border border-red-900/40 text-red-400 text-xs rounded-lg font-semibold">
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
                  placeholder="Tên đăng nhập mới (Ví dụ: nguyenvana)"
                  className="w-full px-4 py-2.5 bg-black border border-zinc-800 rounded-lg text-sm focus:outline-none focus:border-zinc-500 focus:ring-1 focus:ring-zinc-500 text-white placeholder-zinc-500 transition-colors font-medium"
                />
              </div>

              <div>
                <input
                  type="password"
                  value={password}
                  onChange={(e) => {
                    setPassword(e.target.value);
                    setError('');
                  }}
                  required
                  placeholder="Mật khẩu mới"
                  className="w-full px-4 py-2.5 bg-black border border-zinc-800 rounded-lg text-sm focus:outline-none focus:border-zinc-500 focus:ring-1 focus:ring-zinc-500 text-white placeholder-zinc-500 transition-colors font-semibold"
                />
              </div>

              <button
                type="submit"
                className="w-full py-2.5 bg-white hover:bg-zinc-200 text-black font-semibold rounded-lg text-sm transition-all cursor-pointer shadow-sm active:scale-[0.98] transform"
              >
                Đăng ký tài khoản
              </button>
            </form>

            {/* Bottom Redirect */}
            <div className="text-center mt-8 pt-4 border-t border-zinc-900">
              <p className="text-xs text-zinc-400 font-medium">
                Đã có tài khoản?{' '}
                <button
                  type="button"
                  onClick={() => setMode('login')}
                  className="text-white hover:underline font-bold cursor-pointer transition-colors"
                >
                  Đăng nhập
                </button>
              </p>
            </div>
          </div>
        )}

        {mode === 'oauth_linking' && (
          <div>
            <h2 className="text-xl font-bold text-center text-white tracking-tight mb-2">
              Liên kết với {oauthProvider}
            </h2>
            <p className="text-xs text-zinc-400 text-center mb-6">
              Không tìm thấy hồ sơ của bạn. Hãy tạo tài khoản mới để liên kết!
            </p>

            <form onSubmit={handleOAuthLinkingSubmit} className="space-y-4">
              <div>
                <label className="block text-[10px] font-bold text-zinc-500 mb-1 uppercase tracking-wide">Họ và tên</label>
                <input
                  type="text"
                  value={fullName}
                  onChange={(e) => setFullName(e.target.value)}
                  required
                  placeholder="Ví dụ: Nguyễn Văn A"
                  className="w-full px-4 py-2 bg-black border border-zinc-800 rounded-lg text-sm focus:outline-none focus:border-zinc-500 text-white transition-colors"
                />
              </div>

              <div>
                <label className="block text-[10px] font-bold text-zinc-500 mb-1 uppercase tracking-wide">Email</label>
                <input
                  type="email"
                  value={oauthEmail}
                  onChange={(e) => setOauthEmail(e.target.value)}
                  required
                  className="w-full px-4 py-2 bg-black border border-zinc-800 rounded-lg text-sm focus:outline-none focus:border-zinc-500 text-white transition-colors"
                />
              </div>

              <div>
                <label className="block text-[10px] font-bold text-zinc-500 mb-1 uppercase tracking-wide">Phòng ban</label>
                <select
                  value={dept}
                  onChange={(e) => setDept(e.target.value)}
                  className="w-full px-4 py-2 bg-black border border-zinc-800 rounded-lg text-sm focus:outline-none focus:border-zinc-500 text-white"
                >
                  <option value="Ban Giám đốc">Ban Giám đốc</option>
                  <option value="Phòng Hành chính">Phòng Hành chính</option>
                  <option value="Phòng Kế hoạch">Phòng Kế hoạch</option>
                  <option value="Phòng Tài chính">Phòng Tài chính</option>
                </select>
              </div>

              <div>
                <label className="block text-[10px] font-bold text-zinc-500 mb-1 uppercase tracking-wide">Chức danh</label>
                <select
                  value={role}
                  onChange={(e) => setRole(e.target.value)}
                  className="w-full px-4 py-2 bg-black border border-zinc-800 rounded-lg text-sm focus:outline-none focus:border-zinc-500 text-white"
                >
                  <option value="Giám đốc">Giám đốc</option>
                  <option value="Trưởng phòng">Trưởng phòng</option>
                  <option value="Chuyên viên">Chuyên viên</option>
                  <option value="Thư ký">Thư ký</option>
                </select>
              </div>

              <div className="pt-2 flex gap-3">
                <button
                  type="button"
                  onClick={() => setMode('login')}
                  className="flex-1 py-2 bg-transparent border border-zinc-800 text-zinc-400 hover:text-white rounded-lg text-xs font-semibold cursor-pointer"
                >
                  Hủy bỏ
                </button>
                <button
                  type="submit"
                  className="flex-1 py-2 bg-white text-black hover:bg-zinc-200 rounded-lg text-xs font-bold cursor-pointer"
                >
                  Tạo tài khoản
                </button>
              </div>
            </form>
          </div>
        )}
      </div>

      {/* Vercel styled terms disclaimer */}
      <div className="text-center mt-6">
        <p className="text-[10px] text-zinc-650 font-semibold leading-relaxed max-w-[340px] mx-auto">
          Bằng việc tham gia, bạn đồng ý với các{' '}
          <a href="#" className="text-zinc-400 hover:underline">Điều khoản dịch vụ</a> và{' '}
          <a href="#" className="text-zinc-400 hover:underline">Chính sách bảo mật</a> của chúng tôi.
        </p>
      </div>
    </div>
  );
}
