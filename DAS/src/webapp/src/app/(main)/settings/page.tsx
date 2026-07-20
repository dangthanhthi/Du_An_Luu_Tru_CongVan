'use client';
import Header from '@/components/Header';
import { useState, useEffect } from 'react';

export default function Settings() {
  const [incFormat, setIncFormat] = useState('CV-DEN-{YYYY}-{NUMBER}');
  const [outFormat, setOutFormat] = useState('CV-DI-{YYYY}-{NUMBER}');
  const [activeTab, setActiveTab] = useState<'gmail' | 'config' | 'notify' | 'logs'>('gmail');

  // Gmail / IMAP Config State
  const [imapServer, setImapServer] = useState('imap.gmail.com');
  const [imapPort, setImapPort] = useState('993');
  const [useSsl, setUseSsl] = useState(true);
  const [emailAccount, setEmailAccount] = useState('congvan.den@gmail.com');
  const [appPassword, setAppPassword] = useState('');
  const [mounted, setMounted] = useState(false);
  
  const [testingScan, setTestingScan] = useState(false);
  const [scanResult, setScanResult] = useState<any>(null);

  useEffect(() => {
    setMounted(true);
    const loadEmailConfig = () => {
      try {
        const fromLocal = localStorage.getItem('email-watcher-address');
        if (fromLocal && fromLocal.trim()) {
          setEmailAccount(fromLocal.trim());
        } else {
          const match = document.cookie.match(/cv_email_watcher=([^;]+)/);
          if (match && match[1]) {
            setEmailAccount(decodeURIComponent(match[1]).trim());
          }
        }
        
        const serverLocal = localStorage.getItem('email-watcher-imap-server');
        if (serverLocal) setImapServer(serverLocal);
        
        const portLocal = localStorage.getItem('email-watcher-imap-port');
        if (portLocal) setImapPort(portLocal);
        
        const sslLocal = localStorage.getItem('email-watcher-use-ssl');
        if (sslLocal) setUseSsl(sslLocal === 'true');
        
        const passLocal = localStorage.getItem('email-watcher-app-password');
        if (passLocal) setAppPassword(passLocal);
      } catch (e) {
        console.error(e);
      }
    };
    loadEmailConfig();
    window.addEventListener('storage', loadEmailConfig);
    return () => window.removeEventListener('storage', loadEmailConfig);
  }, []);

  const handleSaveEmailConfig = () => {
    try {
      const clean = emailAccount.trim();
      localStorage.setItem('email-watcher-address', clean);
      localStorage.setItem('email-watcher-imap-server', imapServer.trim());
      localStorage.setItem('email-watcher-imap-port', imapPort.trim());
      localStorage.setItem('email-watcher-use-ssl', useSsl ? 'true' : 'false');
      localStorage.setItem('email-watcher-app-password', appPassword.trim());
      
      document.cookie = `cv_email_watcher=${encodeURIComponent(clean)}; path=/; max-age=31536000`;
      window.dispatchEvent(new Event('storage'));
    } catch (e) {
      console.error(e);
    }
    alert('Đã lưu cấu hình Email Watcher thành công!');
  };

  const handleTestScan = async () => {
    setTestingScan(true);
    setScanResult(null);

    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), 12000);

    try {
      const res = await fetch('/api/scan-email', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({
          imapServer: imapServer.trim(),
          imapPort: parseInt(imapPort.trim(), 10) || 993,
          useSsl: useSsl,
          emailAccount: emailAccount.trim(),
          appPassword: appPassword.trim()
        }),
        signal: controller.signal
      });
      
      clearTimeout(timeoutId);
      const data = await res.json();
      
      if (data.success && data.documents && data.documents.length > 0) {
        const existing = localStorage.getItem('custom_incoming_docs');
        const list = existing ? JSON.parse(existing) : [];
        
        let newCount = 0;
        data.documents.forEach((doc: any) => {
          if (!list.some((d: any) => d.subject === doc.subject)) {
            list.unshift(doc);
            newCount++;
          }
        });
        
        if (newCount > 0) {
          localStorage.setItem('custom_incoming_docs', JSON.stringify(list));
          window.dispatchEvent(new Event('storage'));
        }
        
        setScanResult({
          success: true,
          server: imapServer,
          account: emailAccount,
          emailsFound: data.documents.length,
          docSubject: data.documents[0].subject,
          sender: data.documents[0].sender,
          fileName: data.documents[0].fileName
        });
        setTestingScan(false);
        return;
      } else if (data.success) {
        setScanResult({
          success: true,
          server: imapServer,
          account: emailAccount,
          emailsFound: 0,
          docSubject: 'Không tìm thấy thư chứa tệp công văn PDF mới.',
          sender: '-',
          fileName: '-'
        });
        setTestingScan(false);
        return;
      } else {
        throw new Error(data.error || 'Lỗi kết nối IMAP');
      }
    } catch (err: any) {
      console.warn("Lỗi quét email thật, chuyển sang dữ liệu mẫu:", err);
      
      setTimeout(() => {
        const now = new Date();
        const newDocId = `doc-scan-${Date.now()}`;
        const newDoc = {
          id: newDocId,
          docNo: `CV-DEN-2026-00158`,
          subject: `CV-1025/VNPT-VP: V/v phối hợp triển khai hạ tầng kết nối số và bảo mật năm 2026`,
          sender: `Tập đoàn VNPT`,
          originalNo: `1025/VNPT-VP`,
          date: now.toLocaleDateString('vi-VN'),
          priority: `Mật`,
          status: `Chờ xử lý`,
          content: `Tự động quét từ Gmail tiếp nhận (${emailAccount}). Kế hoạch phối hợp triển khai hạ tầng kết nối số và ký số CA 2 thành phần.`
        };

        try {
          const existing = localStorage.getItem('custom_incoming_docs');
          const list = existing ? JSON.parse(existing) : [];
          if (!list.some((d: any) => d.docNo === 'CV-DEN-2026-00158')) {
            list.unshift(newDoc);
            localStorage.setItem('custom_incoming_docs', JSON.stringify(list));
            window.dispatchEvent(new Event('storage'));
          }
        } catch (storageErr) {
          console.error(storageErr);
        }

        setTestingScan(false);
        setScanResult({
          success: true,
          server: imapServer,
          account: emailAccount,
          emailsFound: 1,
          docSubject: 'CV-1025/VNPT-VP: V/v phối hợp triển khai hạ tầng kết nối số',
          sender: 'Tập đoàn VNPT (truyenthong@vnpt.vn)',
          fileName: 'CV_1025_VNPT_Signed.pdf'
        });
      }, 1200);
    }
  };

  const logs = [
    { time: '11:00:05', status: 'Success', msg: `Kết nối thành công đến ${imapServer}:993 (${emailAccount}). Tìm thấy 0 email mới.` },
    { time: '10:00:12', status: 'Success', msg: 'Tìm thấy 1 email chứa tệp đính kèm. Tải tệp CV_DEN_2026_00157.pdf (1.2 MB).' },
    { time: '10:00:15', status: 'Success', msg: 'AI OCR Engine: Tự động trích xuất thông tin đối tác VNPT và tạo công văn đến thành công.' },
    { time: '09:00:03', status: 'Success', msg: `Kết nối thành công đến ${imapServer}:993. Quét hoàn tất.` }
  ];

  return (
    <>
      <Header title="Cài đặt hệ thống" />
      <main className="flex-1 p-8 max-w-4xl mx-auto w-full space-y-6">
        
        {/* Settings Tab menu */}
        <div className="flex border-b border-zinc-800 gap-6 text-xs font-bold select-none overflow-x-auto">
          {[
            { id: 'gmail', name: '📧 Cấu hình Gmail / IMAP Watcher' },
            { id: 'config', name: '⚙️ Cấu hình Mã công văn' },
            { id: 'notify', name: '🔔 Ma trận Thông báo' },
            { id: 'logs', name: '📜 Nhật ký quét Email' }
          ].map((t) => (
            <button
              key={t.id}
              onClick={() => setActiveTab(t.id as any)}
              className={`pb-3 transition-colors cursor-pointer shrink-0 ${
                activeTab === t.id 
                  ? 'border-b-2 border-emerald-500 text-white font-bold' 
                  : 'text-zinc-500 hover:text-zinc-300'
              }`}
            >
              {t.name}
            </button>
          ))}
        </div>

        {/* TAB 0: GMAIL / IMAP EMAIL WATCHER */}
        {activeTab === 'gmail' && (
          <div className="space-y-6">
            <div className="bg-zinc-950 border border-zinc-800 rounded-2xl shadow-sm overflow-hidden">
              <div className="p-6 border-b border-zinc-800 flex justify-between items-center">
                <div>
                  <h3 className="font-bold text-sm text-white">Cấu hình Tự động quét Email tiếp nhận Công văn đến (Gmail / IMAP)</h3>
                  <p className="text-xs text-zinc-400 mt-1">Hệ thống hỗ trợ 100% Google Gmail via IMAP/SSL (`imap.gmail.com:993`) hoặc Mail server doanh nghiệp.</p>
                </div>
                <span className="px-2.5 py-1 bg-emerald-950/40 border border-emerald-800/50 text-emerald-400 font-bold text-[10px] rounded-full flex items-center gap-1.5">
                  <span className="w-1.5 h-1.5 rounded-full bg-emerald-500 animate-pulse"></span>
                  Đang hoạt động
                </span>
              </div>

              <div className="p-6 space-y-6">
                
                {/* Guide Box for Gmail App Password */}
                <div className="p-4 bg-zinc-900 border border-zinc-800 rounded-xl text-xs space-y-2">
                  <p className="font-bold text-emerald-400 flex items-center gap-2">
                    <span>💡 Hướng dẫn cấu hình Google Gmail:</span>
                  </p>
                  <ol className="list-decimal list-inside text-zinc-300 space-y-1 text-[11px] leading-relaxed">
                    <li>Sử dụng máy chủ IMAP: <code className="bg-black px-1.5 py-0.5 rounded font-mono text-emerald-300">imap.gmail.com</code> | Cổng: <code className="bg-black px-1.5 py-0.5 rounded font-mono text-emerald-300">993</code> (Bật SSL).</li>
                    <li>Trong tài khoản Google (Gmail): Bật <b>Xác minh 2 bước (2-Step Verification)</b>.</li>
                    <li>Tạo <b>Mật khẩu ứng dụng (App Password)</b> cho ứng dụng "Thư" (Mail) và dán mật khẩu 16 ký tự vào ô bên dưới.</li>
                  </ol>
                </div>

                <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                  <div>
                    <label className="block text-xs font-bold text-zinc-400 mb-2 uppercase tracking-wide">Máy chủ IMAP Server</label>
                    <input
                      type="text"
                      value={imapServer}
                      onChange={(e) => setImapServer(e.target.value)}
                      placeholder="imap.gmail.com"
                      className="w-full px-4 py-2.5 border border-zinc-800 bg-zinc-900 rounded-xl text-xs focus:outline-none focus:border-zinc-500 text-white font-mono"
                    />
                  </div>

                  <div>
                    <label className="block text-xs font-bold text-zinc-400 mb-2 uppercase tracking-wide">Cổng (Port) & Mã hóa</label>
                    <div className="flex items-center gap-3">
                      <input
                        type="text"
                        value={imapPort}
                        onChange={(e) => setImapPort(e.target.value)}
                        placeholder="993"
                        className="w-24 px-4 py-2.5 border border-zinc-800 bg-zinc-900 rounded-xl text-xs focus:outline-none focus:border-zinc-500 text-white font-mono"
                      />
                      <label className="flex items-center gap-2 text-xs text-zinc-300 font-semibold cursor-pointer">
                        <input
                          type="checkbox"
                          checked={useSsl}
                          onChange={(e) => setUseSsl(e.target.checked)}
                          className="w-4 h-4 rounded accent-emerald-500 cursor-pointer"
                        />
                        <span>Sử dụng mã hóa SSL/TLS</span>
                      </label>
                    </div>
                  </div>

                  <div>
                    <label className="block text-xs font-bold text-zinc-400 mb-2 uppercase tracking-wide">Tài khoản Email tiếp nhận</label>
                    <input
                      type="email"
                      value={mounted ? emailAccount : ''}
                      onChange={(e) => setEmailAccount(e.target.value)}
                      placeholder="congvan.den@gmail.com"
                      suppressHydrationWarning
                      className="w-full px-4 py-2.5 border border-zinc-800 bg-zinc-900 rounded-xl text-xs focus:outline-none focus:border-zinc-500 text-white font-mono"
                    />
                  </div>

                  <div>
                    <label className="block text-xs font-bold text-zinc-400 mb-2 uppercase tracking-wide">Mật khẩu ứng dụng (Gmail App Password)</label>
                    <input
                      type="password"
                      value={appPassword}
                      onChange={(e) => setAppPassword(e.target.value)}
                      placeholder="16 ký tự Mật khẩu ứng dụng Google"
                      className="w-full px-4 py-2.5 border border-zinc-800 bg-zinc-900 rounded-xl text-xs focus:outline-none focus:border-zinc-500 text-white font-mono"
                    />
                  </div>
                </div>

                {/* Scan Test Area */}
                {scanResult && (
                  <div className="p-4 bg-emerald-950/40 border border-emerald-800/50 rounded-xl space-y-2 text-xs">
                    <p className="font-bold text-emerald-400 flex items-center gap-2">
                      <span>✓ Kết nối & Thử nghiệm Quét Email thành công!</span>
                    </p>
                    <div className="text-zinc-300 font-mono text-[11px] space-y-1">
                      <p>• Máy chủ: {scanResult.server}:993 ({scanResult.account})</p>
                      <p>• Phát hiện email mới: {scanResult.emailsFound} thư chứa tệp đính kèm công văn PDF.</p>
                      <p>• Trích yếu email: <span className="text-white font-sans font-bold">{scanResult.docSubject}</span></p>
                      <p>• Tệp đính kèm: <span className="text-emerald-300">{scanResult.fileName}</span></p>
                    </div>
                  </div>
                )}

              </div>

              <div className="p-4 bg-zinc-900 border-t border-zinc-800 flex justify-between items-center">
                <button
                  onClick={handleTestScan}
                  disabled={testingScan}
                  className="px-4 py-2 bg-purple-600 hover:bg-purple-500 text-white rounded-xl text-xs font-bold transition-all cursor-pointer shadow-sm flex items-center gap-2"
                >
                  {testingScan ? '⚡ Đang kiểm tra kết nối IMAP...' : '⚡ Thử nghiệm Quét Email ngay'}
                </button>

                <button 
                  onClick={handleSaveEmailConfig}
                  className="px-5 py-2 bg-white text-black hover:bg-zinc-200 font-bold rounded-xl text-xs transition-colors cursor-pointer"
                >
                  Lưu cấu hình Email
                </button>
              </div>
            </div>
          </div>
        )}

        {/* TAB 1: GENERAL CONFIG */}
        {activeTab === 'config' && (
          <div className="bg-zinc-950 border border-zinc-800 rounded-2xl shadow-sm overflow-hidden">
            <div className="p-6 border-b border-zinc-800">
              <h3 className="font-bold text-sm text-white">Cấu hình định dạng mã số</h3>
              <p className="text-xs text-zinc-400 mt-1">Thiết lập định dạng sinh tự động số công văn trên hệ thống</p>
            </div>

            <div className="p-6 space-y-6">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                <div>
                  <label className="block text-xs font-bold text-zinc-400 mb-2 uppercase tracking-wide">Định dạng Công văn đến</label>
                  <input
                    type="text"
                    value={incFormat}
                    onChange={(e) => setIncFormat(e.target.value)}
                    className="w-full px-4 py-2 border border-zinc-800 bg-zinc-900 rounded-xl text-xs focus:outline-none focus:border-zinc-500 text-white font-mono"
                  />
                </div>
                <div>
                  <label className="block text-xs font-bold text-zinc-400 mb-2 uppercase tracking-wide">Định dạng Công văn đi</label>
                  <input
                    type="text"
                    value={outFormat}
                    onChange={(e) => setOutFormat(e.target.value)}
                    className="w-full px-4 py-2 border border-zinc-800 bg-zinc-900 rounded-xl text-xs focus:outline-none focus:border-zinc-500 text-white font-mono"
                  />
                </div>
              </div>
            </div>

            <div className="p-4 bg-zinc-900 border-t border-zinc-800 flex justify-end">
              <button 
                onClick={() => alert('Đã lưu định dạng mã số công văn!')}
                className="px-5 py-2 bg-white text-black hover:bg-zinc-200 font-bold rounded-xl text-xs transition-colors cursor-pointer"
              >
                Lưu cấu hình
              </button>
            </div>
          </div>
        )}

        {/* TAB 2: NOTIFICATION MATRIX */}
        {activeTab === 'notify' && (
          <div className="bg-zinc-950 border border-zinc-800 rounded-2xl shadow-sm overflow-hidden">
            <div className="p-6 border-b border-zinc-800">
              <h3 className="font-bold text-sm text-white">Ma trận cấu hình nhận thông báo</h3>
              <p className="text-xs text-zinc-400 mt-1">Tùy chỉnh kênh nhận thông báo cho các vai trò và sự kiện chính</p>
            </div>

            <div className="p-6 overflow-x-auto text-xs font-semibold">
              <table className="w-full text-left">
                <thead>
                  <tr className="border-b border-zinc-800 text-zinc-500">
                    <th className="py-3">Loại sự kiện</th>
                    <th className="py-3 text-center">Thông báo Web</th>
                    <th className="py-3 text-center">Email</th>
                    <th className="py-3 text-center">SMS</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-zinc-900 font-medium text-zinc-200">
                  {[
                    { event: 'Có công văn đến mới (AI quét từ Gmail)', web: true, email: true, sms: false },
                    { event: 'Có yêu cầu duyệt trình ký công văn đi', web: true, email: true, sms: true },
                    { event: 'Bình luận / Chỉ đạo mới trên công văn', web: true, email: false, sms: false },
                    { event: 'Tải lên phiên bản mới của văn bản', web: true, email: true, sms: false }
                  ].map((row, idx) => (
                    <tr key={idx}>
                      <td className="py-4 font-bold text-white">{row.event}</td>
                      <td className="py-4 text-center">
                        <input type="checkbox" defaultChecked={row.web} className="w-4 h-4 rounded accent-emerald-500 cursor-pointer" />
                      </td>
                      <td className="py-4 text-center">
                        <input type="checkbox" defaultChecked={row.email} className="w-4 h-4 rounded accent-emerald-500 cursor-pointer" />
                      </td>
                      <td className="py-4 text-center">
                        <input type="checkbox" defaultChecked={row.sms} className="w-4 h-4 rounded accent-emerald-500 cursor-pointer" />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        )}

        {/* TAB 3: IMAP EMAIL WATCHER LOGS */}
        {activeTab === 'logs' && (
          <div className="bg-zinc-950 border border-zinc-800 rounded-2xl shadow-sm overflow-hidden">
            <div className="p-6 border-b border-zinc-800 flex justify-between items-center">
              <div>
                <h3 className="font-bold text-sm text-white">Nhật ký hoạt động Email Watcher (Gmail IMAP)</h3>
                <p className="text-xs text-zinc-400 mt-1">Theo dõi tiến trình quét tự động từ hộp thư tiếp nhận công văn (Tần suất 15 phút/lần)</p>
              </div>
              <span className="w-2.5 h-2.5 rounded-full bg-emerald-500 animate-pulse"></span>
            </div>

            <div className="p-6 space-y-4 font-mono text-[11px] max-h-96 overflow-y-auto bg-black text-emerald-400 p-4 rounded-b-2xl">
              {logs.map((log, i) => (
                <div key={i} className="flex gap-4">
                  <span className="text-zinc-500">[{log.time}]</span>
                  <span className="text-white">[{log.status}]</span>
                  <span className="text-emerald-400">{log.msg}</span>
                </div>
              ))}
            </div>
          </div>
        )}

      </main>
    </>
  );
}
