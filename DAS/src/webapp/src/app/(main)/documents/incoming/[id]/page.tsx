'use client';
import Header from '@/components/Header';
import { useState } from 'react';

export default function IncomingDocDetail() {
  const [activeTab, setActiveTab] = useState<'content' | 'approval' | 'versions' | 'ai'>('content');
  const [comments, setComments] = useState([
    { user: 'Lê Thị Secretary', text: 'AI đã trích xuất đúng thông tin công văn gốc. Đã trình Giám đốc xem xét.', date: '19/07/2026 09:30' },
    { user: 'Nguyễn Văn Director', text: 'Chuyển phòng Hành chính và Kỹ thuật phối hợp triển khai.', date: '19/07/2026 10:15' }
  ]);
  const [newComment, setNewComment] = useState('');
  const [signed, setSigned] = useState(false);
  const [signingModal, setSigningModal] = useState(false);
  const [pinCode, setPinCode] = useState('');

  const doc = {
    docNo: 'CV-DEN-2026-00157',
    subject: 'V/v hướng dẫn công tác báo cáo công văn lưu trữ quý II',
    sender: 'Tập đoàn Công nghệ Viễn thông VNPT',
    senderShort: 'VNPT',
    docDate: '18/07/2026',
    receivedDate: '19/07/2026',
    originalNo: '1025/VNPT-VP',
    priority: 'Mật',
    status: 'Chờ xử lý',
    distributedDepts: ['Phòng Hành chính', 'Phòng Kỹ thuật'],
    content: 'Căn cứ theo nghị định số 102/NĐ-CP về việc lưu trữ tài liệu công văn điện tử công ty...'
  };

  const handleAddComment = (e: React.FormEvent) => {
    e.preventDefault();
    if (!newComment.trim()) return;
    setComments([...comments, {
      user: 'Administrator',
      text: newComment,
      date: new Date().toLocaleString('vi-VN')
    }]);
    setNewComment('');
  };

  const handleSign = () => {
    if (pinCode === '1234') {
      setSigned(true);
      setSigningModal(false);
    } else {
      alert('Mã PIN không đúng (Gợi ý: 1234)');
    }
  };

  return (
    <>
      <Header title="Chi tiết công văn" />
      <main className="flex-1 p-8 grid grid-cols-1 lg:grid-cols-4 gap-8 max-w-7xl mx-auto w-full">
        
        {/* Main tabs area */}
        <div className="lg:col-span-3 space-y-6">
          
          {/* Tabs bar */}
          <div className="flex border-b border-border gap-6 text-sm font-semibold select-none">
            {[
              { id: 'content', name: 'Nội dung & Tệp tin' },
              { id: 'approval', name: 'Trình ký & Ký số' },
              { id: 'versions', name: 'Lịch sử phiên bản' },
              { id: 'ai', name: 'AI OCR Đối soát' }
            ].map((t) => (
              <button
                key={t.id}
                onClick={() => setActiveTab(t.id as any)}
                className={`pb-3 transition-colors cursor-pointer ${
                  activeTab === t.id 
                    ? 'border-b-2 border-emerald-500 text-emerald-600 dark:text-emerald-400 font-bold' 
                    : 'text-muted-foreground hover:text-muted-foreground'
                }`}
              >
                {t.name}
              </button>
            ))}
          </div>

          {/* TAB 1: CONTENT */}
          {activeTab === 'content' && (
            <div className="space-y-6">
              <div className="p-6 bg-card border border-border rounded-2xl space-y-6">
                <div className="flex justify-between items-center border-b border-border pb-4">
                  <div>
                    <h3 className="text-xl font-bold text-foreground">{doc.docNo}</h3>
                    <p className="text-xs text-muted-foreground font-semibold mt-1">{doc.subject}</p>
                  </div>
                  <span className="px-3 py-1 bg-amber-100 text-amber-800 dark:bg-amber-950/20 dark:text-amber-450 rounded-full text-xs font-bold">
                    {doc.status}
                  </span>
                </div>

                <div className="grid grid-cols-1 md:grid-cols-2 gap-6 text-xs font-medium">
                  <div>
                    <span className="text-muted-foreground block mb-1">Đối tác gửi:</span>
                    <span className="font-bold text-foreground">{doc.sender} ({doc.senderShort})</span>
                  </div>
                  <div>
                    <span className="text-muted-foreground block mb-1">Số hiệu gốc:</span>
                    <span className="font-bold text-foreground">{doc.originalNo}</span>
                  </div>
                  <div>
                    <span className="text-muted-foreground block mb-1">Ngày công văn:</span>
                    <span className="text-foreground">{doc.docDate}</span>
                  </div>
                  <div>
                    <span className="text-muted-foreground block mb-1">Ngày nhận trên hệ thống:</span>
                    <span className="text-foreground">{doc.receivedDate}</span>
                  </div>
                  <div>
                    <span className="text-muted-foreground block mb-1">Phòng ban đã phân phối:</span>
                    <div className="flex flex-wrap gap-1.5 mt-1">
                      {doc.distributedDepts.map((d, i) => (
                        <span key={i} className="px-2.5 py-0.5 bg-muted text-foreground rounded text-[10px] font-bold">
                          {d}
                        </span>
                      ))}
                    </div>
                  </div>
                </div>

                <div className="border-t border-border pt-6">
                  <span className="text-muted-foreground text-xs block mb-2 font-bold uppercase">Trích yếu nội dung:</span>
                  <p className="text-sm text-muted-foreground leading-relaxed bg-muted p-4 rounded-xl border border-border">
                    {doc.content}
                  </p>
                </div>
              </div>

              {/* PDF Preview */}
              <div className="p-6 bg-card border border-border rounded-2xl space-y-4">
                <div className="flex justify-between items-center">
                  <h4 className="font-bold text-xs uppercase tracking-wide text-foreground">File tài liệu đính kèm (PDF)</h4>
                  {signed && (
                    <span className="px-2.5 py-1 border border-emerald-500 text-emerald-600 dark:text-emerald-400 bg-emerald-50 dark:bg-emerald-950/20 text-[10px] font-bold rounded">
                      🔒 ĐÃ KÝ SỐ CHỨNG THƯ
                    </span>
                  )}
                </div>
                <div className="w-full h-[500px] border border-border rounded-xl bg-muted overflow-hidden relative">
                  <iframe 
                    src="/documents/samples/incoming-sample.pdf"
                    className="w-full h-full border-none"
                    title="Tài liệu PDF"
                  />
                  
                  {signed && (
                    <div className="absolute bottom-6 right-6 border-2 border-red-500 p-2 text-red-500 font-black rounded-lg text-xs uppercase transform rotate-[-6deg] select-none tracking-widest bg-card shadow-md z-10">
                      ❌ ĐÃ KÝ SỐ<br/>
                      <span className="text-[9px] font-semibold text-muted-foreground normal-case">DAS Sign Service</span>
                    </div>
                  )}
                </div>
              </div>
            </div>
          )}

          {/* TAB 2: APPROVAL & SIGNING */}
          {activeTab === 'approval' && (
            <div className="p-6 bg-card border border-border rounded-2xl space-y-8">
              <h4 className="font-bold text-xs uppercase tracking-wide text-foreground">Sơ đồ tiến độ Trình ký & Duyệt</h4>
              
              <div className="flex flex-col md:flex-row items-center justify-between gap-6 relative">
                {[
                  { step: 1, title: 'Soạn thảo', desc: 'Thư ký Hành chính', status: 'Đã hoàn thành', date: '19/07 09:15', done: true },
                  { step: 2, title: 'Kiểm duyệt', desc: 'Trưởng phòng Hành chính', status: 'Đã duyệt', date: '19/07 09:30', done: true },
                  { step: 3, title: 'Phê duyệt & Ký số', desc: 'Giám đốc Công ty', status: signed ? 'Đã ký số' : 'Chờ phê duyệt', date: signed ? '19/07 10:15' : 'Đang xử lý', done: signed }
                ].map((s, idx) => (
                  <div key={idx} className="flex-1 flex flex-col items-center text-center p-4 border border-border bg-muted/50 rounded-xl relative w-full">
                    <div className={`w-8 h-8 rounded-full flex items-center justify-center font-bold text-xs mb-3 ${
                      s.done 
                        ? 'bg-emerald-500 text-white' 
                        : 'bg-zinc-250 text-zinc-600 dark:bg-zinc-850'
                    }`}>
                      {s.step}
                    </div>
                    <h5 className="font-bold text-sm">{s.title}</h5>
                    <p className="text-[11px] text-muted-foreground font-semibold mt-1">{s.desc}</p>
                    <span className={`text-[10px] px-2 py-0.5 mt-3 rounded-full font-bold ${
                      s.done ? 'bg-emerald-100 text-emerald-800 dark:bg-emerald-950/30' : 'bg-amber-100 text-amber-800 dark:bg-amber-950/20'
                    }`}>
                      {s.status}
                    </span>
                  </div>
                ))}
              </div>

              {!signed && (
                <div className="pt-6 border-t border-border flex justify-center">
                  <button
                    onClick={() => setSigningModal(true)}
                    className="px-6 py-3 bg-emerald-600 hover:bg-emerald-700 text-white font-bold rounded-xl text-sm transition-all flex items-center gap-2 cursor-pointer shadow-md shadow-emerald-500/10"
                  >
                    🔏 Thực hiện Ký số tài liệu
                  </button>
                </div>
              )}
            </div>
          )}

          {/* TAB 3: VERSIONS */}
          {activeTab === 'versions' && (
            <div className="p-6 bg-card border border-border rounded-2xl space-y-6">
              <div className="flex justify-between items-center border-b border-border pb-4">
                <h4 className="font-bold text-xs uppercase tracking-wide text-foreground">Lịch sử các phiên bản tệp tài liệu</h4>
                <button className="px-3 py-1.5 bg-muted text-foreground border border-border rounded-lg text-xs font-bold hover:bg-muted/80 cursor-pointer">
                  Tải lên phiên bản mới
                </button>
              </div>

              <div className="space-y-4">
                {[
                  { version: 'v2.0 (Bản ký số)', size: '1.2 MB', date: '19/07/2026 10:15', author: 'Giám đốc Nguyễn Văn Director', current: signed, note: 'Bản chứa chữ ký số và con dấu đỏ điện tử.' },
                  { version: 'v1.0 (Dự thảo)', size: '1.1 MB', date: '18/07/2026 14:30', author: 'Lê Thị Secretary', current: !signed, note: 'Bản soạn thảo ban đầu trình ký.' }
                ].map((v, i) => (
                  <div key={i} className="p-4 border border-border rounded-xl flex justify-between items-start bg-muted/30">
                    <div className="space-y-1">
                      <div className="flex items-center gap-2">
                        <span className="font-bold text-sm text-foreground">{v.version}</span>
                        {v.current && (
                          <span className="text-[9px] bg-emerald-100 text-emerald-800 dark:bg-emerald-950/30 px-2 py-0.5 rounded font-bold">
                            Hiện tại
                          </span>
                        )}
                      </div>
                      <p className="text-xs text-muted-foreground font-semibold">{v.note}</p>
                      <div className="text-[10px] text-muted-foreground flex gap-4 pt-1 font-semibold">
                        <span>Kích thước: {v.size}</span>
                        <span>Cập nhật: {v.date}</span>
                        <span>Người tạo: {v.author}</span>
                      </div>
                    </div>
                    <button className="text-xs font-bold text-accent hover:underline cursor-pointer">
                      Tải xuống
                    </button>
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* TAB 4: AI OCR CHECK */}
          {activeTab === 'ai' && (
            <div className="p-6 bg-card border border-border rounded-2xl space-y-6">
              <div className="flex justify-between items-center border-b border-border pb-4">
                <div>
                  <h4 className="font-bold text-xs uppercase tracking-wide text-foreground">AI OCR Đối soát tự động</h4>
                  <p className="text-xs text-muted-foreground mt-1">Hệ thống đối sánh dữ liệu trích xuất từ văn bản đính kèm so với thông tin đăng ký.</p>
                </div>
                <div className="text-right">
                  <span className="text-xs font-bold text-muted-foreground">Mức độ tin cậy AI</span>
                  <div className="text-xl font-extrabold text-emerald-650 dark:text-emerald-450">98.5%</div>
                </div>
              </div>

              <div className="overflow-x-auto">
                <table className="w-full text-xs text-left font-semibold">
                  <thead>
                    <tr className="border-b border-border text-muted-foreground">
                      <th className="py-2.5">Trường thông tin</th>
                      <th className="py-2.5">Giá trị đăng ký</th>
                      <th className="py-2.5">AI OCR Trích xuất</th>
                      <th className="py-2.5">Tình trạng</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-border/60">
                    {[
                      { field: 'Số hiệu gốc', reg: '1025/VNPT-VP', ai: '1025/VNPT-VP', match: true },
                      { field: 'Nơi gửi', reg: 'Tập đoàn Công nghệ Viễn thông VNPT', ai: 'VNPT Corp', match: true },
                      { field: 'Ngày văn bản', reg: '18/07/2026', ai: '18/07/2026', match: true },
                      { field: 'Độ khẩn', reg: 'Mật', ai: 'Mat', match: true }
                    ].map((row, idx) => (
                      <tr key={idx} className="hover:bg-muted/10">
                        <td className="py-3 font-bold text-foreground">{row.field}</td>
                        <td className="py-3 text-muted-foreground">{row.reg}</td>
                        <td className="py-3 text-foreground">{row.ai}</td>
                        <td className="py-3">
                          <span className={`px-2 py-0.5 rounded text-[10px] font-bold ${
                            row.match ? 'bg-emerald-100 text-emerald-800 dark:bg-emerald-950/20 dark:text-emerald-450' : 'bg-rose-100 text-rose-800'
                          }`}>
                            {row.match ? '✓ Khớp' : '✗ Lệch'}
                          </span>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}

        </div>

        {/* Right sidebar info */}
        <div className="space-y-6">
          
          {/* Action buttons */}
          <div className="p-6 bg-card border border-border rounded-2xl space-y-4">
            <h4 className="font-bold text-xs uppercase tracking-wide text-foreground">Xử lý tài liệu</h4>
            <div className="grid grid-cols-1 gap-3">
              <button className="w-full py-3 bg-primary text-primary-foreground hover:opacity-90 font-bold rounded-xl text-sm transition-colors shadow-sm cursor-pointer">
                Distribute (Phân phối)
              </button>
              <button className="w-full py-3 border border-border hover:bg-muted text-foreground font-bold rounded-xl text-sm transition-colors cursor-pointer">
                Sửa thông tin
              </button>
            </div>
          </div>

          {/* Real dynamic QR code */}
          <div className="p-6 bg-card border border-border rounded-2xl text-center space-y-4">
            <h4 className="font-bold text-xs uppercase tracking-wide text-foreground text-left">Mã QR truy cập nhanh</h4>
            <div className="w-40 h-40 border border-border rounded-xl bg-white mx-auto flex items-center justify-center p-3 overflow-hidden shadow-sm">
              <img 
                src={`https://api.qrserver.com/v1/create-qr-code/?size=150x150&data=${encodeURIComponent(
                  typeof window !== 'undefined' ? window.location.href : 'http://localhost:3000/documents/incoming/1'
                )}`} 
                alt="QR Code" 
                className="w-full h-full object-contain"
              />
            </div>
            <p className="text-[10px] text-muted-foreground font-semibold leading-relaxed">Sử dụng camera điện thoại hoặc ứng dụng quét để mở nhanh tài liệu gốc trên di động.</p>
          </div>

          {/* Comments panel */}
          <div className="p-6 bg-card border border-border rounded-2xl space-y-4">
            <h4 className="font-bold text-xs uppercase tracking-wide text-foreground">Ý kiến chỉ đạo</h4>
            
            <div className="space-y-4 max-h-52 overflow-y-auto pr-1">
              {comments.map((c, i) => (
                <div key={i} className="text-xs space-y-1.5 p-3 bg-muted/40 border border-border rounded-xl">
                  <div className="flex justify-between font-bold text-foreground">
                    <span>{c.user}</span>
                    <span className="text-[10px] text-muted-foreground font-semibold">{c.date}</span>
                  </div>
                  <p className="text-muted-foreground leading-relaxed font-medium">{c.text}</p>
                </div>
              ))}
            </div>

            <form onSubmit={handleAddComment} className="border-t border-border/80 pt-4 flex gap-2">
              <input
                type="text"
                value={newComment}
                onChange={(e) => setNewComment(e.target.value)}
                placeholder="Nhập ý kiến chỉ đạo..."
                className="flex-1 px-3 py-2 border border-border bg-muted rounded-xl text-xs focus:outline-none focus:border-emerald-500 text-foreground font-semibold"
              />
              <button
                type="submit"
                className="px-3 py-2 bg-primary text-primary-foreground hover:opacity-90 rounded-xl text-xs font-bold transition-colors cursor-pointer"
              >
                Gửi
              </button>
            </form>
          </div>

        </div>

      </main>

      {/* DIGITAL SIGNING PIN MODAL */}
      {signingModal && (
        <div className="fixed inset-0 bg-black/50 dark:bg-black/75 z-50 flex items-center justify-center p-4 backdrop-blur-sm">
          <div className="bg-card border border-border w-full max-w-sm rounded-2xl shadow-xl overflow-hidden transition-all duration-200 p-6 space-y-4">
            <div className="text-center space-y-2">
              <div className="w-12 h-12 rounded-full bg-emerald-100 text-emerald-700 dark:bg-emerald-950/30 dark:text-emerald-400 flex items-center justify-center mx-auto text-xl">
                🔏
              </div>
              <h3 className="font-bold text-foreground text-base">Xác nhận ký số điện tử</h3>
              <p className="text-xs text-muted-foreground font-semibold">Nhập mã PIN của Chứng thư số CA cá nhân để hoàn tất quy trình phê duyệt công văn đến.</p>
            </div>

            <div className="space-y-4">
              <div>
                <label className="block text-[10px] font-bold text-muted-foreground mb-1.5 uppercase tracking-wide">Chọn Chứng thư số</label>
                <select className="w-full px-3 py-2 border border-border bg-muted rounded-xl text-xs focus:outline-none text-foreground font-semibold">
                  <option>Giám đốc Nguyễn Văn Director (CA-2026-DAS)</option>
                  <option>Thư ký Lê Thị Secretary (CA-2026-DAS)</option>
                </select>
              </div>

              <div>
                <label className="block text-[10px] font-bold text-muted-foreground mb-1.5 uppercase tracking-wide">Mã PIN xác thực (PIN Code)</label>
                <input
                  type="password"
                  value={pinCode}
                  onChange={(e) => setPinCode(e.target.value)}
                  placeholder="••••"
                  className="w-full px-3 py-2 border border-border bg-muted rounded-xl text-xs focus:outline-none text-center text-foreground font-bold tracking-widest"
                />
              </div>
            </div>

            <div className="pt-2 flex justify-end gap-3 text-xs font-bold">
              <button
                onClick={() => setSigningModal(false)}
                className="px-4 py-2 border border-border bg-muted text-foreground hover:opacity-90 rounded-lg cursor-pointer"
              >
                Hủy bỏ
              </button>
              <button
                onClick={handleSign}
                className="px-4 py-2 bg-emerald-600 hover:bg-emerald-700 text-white rounded-lg cursor-pointer"
              >
                Xác thực ký
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  );
}
