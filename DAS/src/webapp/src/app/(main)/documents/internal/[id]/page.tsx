'use client';
import Header from '@/components/Header';
import DocumentQRCode from '@/components/DocumentQRCode';
import { useState, useEffect } from 'react';
import { useParams } from 'next/navigation';

export default function InternalDocDetail() {
  const params = useParams();
  const docId = (params?.id as string) || '1';

  const [activeTab, setActiveTab] = useState<'info' | 'pdf' | 'approval' | 'versions'>('info');
  // Pending document (ID 1) starts as UNSIGNED (false)
  const [signed, setSigned] = useState(false);
  const [signingModal, setSigningModal] = useState(false);
  const [pinCode, setPinCode] = useState('1234');
  const [signedDate, setSignedDate] = useState('17/07/2026 11:15');
  const [hashValue, setHashValue] = useState('7f6e5d4c3b2a1f0e9d8c7b6a5f4e3d2c1b0a9f8');

  const [customDoc, setCustomDoc] = useState<any>(null);

  useEffect(() => {
    const saved = localStorage.getItem(`signed-doc-internal-${docId}`);
    if (saved !== null) {
      setSigned(saved === 'true');
    } else {
      if (docId === '1') {
        setSigned(false);
      } else {
        setSigned(true);
      }
    }

    try {
      const customSaved = localStorage.getItem('custom_internal_docs');
      if (customSaved) {
        const list = JSON.parse(customSaved);
        const found = list.find((d: any) => d.id === docId);
        if (found) setCustomDoc(found);
      }
    } catch (e) {
      console.error(e);
    }
  }, [docId]);

  const doc = customDoc ? {
    id: docId,
    docNo: customDoc.docNo,
    subject: customDoc.subject,
    type: 'Công văn nội bộ',
    sender: customDoc.department || 'Ban Giám đốc',
    date: customDoc.date,
    status: signed ? 'Đã ký số ban hành' : (customDoc.status || 'Chờ xử lý'),
    priority: customDoc.priority || 'Thường',
    summary: customDoc.content || customDoc.subject
  } : {
    id: docId,
    docNo: docId === '1' ? 'CV-NB-2026-00034' : 'CV-NB-2026-00033',
    subject: 'Thông báo lịch nghỉ phép tập thể và làm việc luân phiên năm 2026',
    type: 'Công văn nội bộ',
    sender: 'Ban Giám đốc',
    date: '17/07/2026',
    status: signed ? 'Đã ký số ban hành' : 'Chờ xử lý',
    priority: 'Thường',
    summary: 'Thông báo toàn thể cán bộ nhân viên về lịch nghỉ phép tập thể và phân công trực ca trực đảm bảo vận hành hệ thống trong quý III.'
  };

  const handleSign = () => {
    if (pinCode === '1234' || pinCode.length >= 4) {
      setSigned(true);
      localStorage.setItem(`signed-doc-internal-${docId}`, 'true');
      const now = new Date();
      setSignedDate(now.toLocaleString('vi-VN'));
      setHashValue('c' + Math.random().toString(36).substring(2, 15) + Math.random().toString(36).substring(2, 15));
      setSigningModal(false);
    } else {
      alert('Mã PIN không đúng! Mã PIN mẫu thử nghiệm là: 1234');
    }
  };

  const handleUnsign = () => {
    setSigned(false);
    localStorage.setItem(`signed-doc-internal-${docId}`, 'false');
  };

  const handlePrint = () => {
    window.print();
  };

  return (
    <>
      <Header title={`Chi tiết Công văn nội bộ: ${doc.docNo}`} />
      <main className="flex-1 p-8 space-y-6 max-w-7xl mx-auto w-full">
        
        {/* Action Controls Bar */}
        <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 bg-zinc-950 p-4 border border-zinc-800 rounded-xl shadow-sm">
          <div className="flex items-center gap-3">
            <h2 className="text-base font-bold text-white tracking-tight">{doc.docNo}</h2>
            <span className={`px-2.5 py-0.5 text-xs rounded-full font-bold border ${
              signed 
                ? 'bg-emerald-950/40 border-emerald-800/50 text-emerald-400' 
                : 'bg-amber-950/40 border-amber-800/50 text-amber-300'
            }`}>
              {signed ? '● Đã ký số ban hành' : '○ Chờ xử lý (Chưa ký)'}
            </span>
          </div>

          <div className="flex items-center gap-2">
            <button 
              onClick={handlePrint}
              className="px-3.5 py-1.5 bg-zinc-900 hover:bg-zinc-800 border border-zinc-800 text-white rounded-lg text-xs font-semibold flex items-center gap-2 transition-colors cursor-pointer"
            >
              <svg className="w-4 h-4 text-zinc-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M17 17h2a2 2 0 002-2v-4a2 2 0 00-2-2H5a2 2 0 00-2 2v4a2 2 0 002 2h2m2 4h6a2 2 0 002-2v-4a2 2 0 00-2-2H9a2 2 0 00-2 2v4a2 2 0 002 2zm8-12V5a2 2 0 00-2-2H9a2 2 0 00-2 2v4h10z" />
              </svg>
              In công văn
            </button>

            {!signed ? (
              <button 
                onClick={() => setSigningModal(true)}
                className="px-4 py-1.5 bg-emerald-600 hover:bg-emerald-500 text-white font-bold rounded-lg text-xs transition-all cursor-pointer shadow-sm flex items-center gap-2 animate-pulse"
              >
                🔏 Thực hiện Ký số CA
              </button>
            ) : (
              <button 
                onClick={handleUnsign}
                className="px-3 py-1.5 bg-zinc-900 hover:bg-zinc-800 border border-zinc-800 text-zinc-400 hover:text-red-400 rounded-lg text-xs font-semibold transition-colors cursor-pointer"
                title="Trả về trạng thái chưa ký (Thử nghiệm)"
              >
                Trả về Chưa ký
              </button>
            )}
          </div>
        </div>

        {/* Dynamic Navigation Tabs */}
        <div className="flex border-b border-zinc-800 gap-8 text-xs font-bold">
          {[
            { id: 'info', name: 'Nội dung & Bản ký số 2 thành phần' },
            { id: 'pdf', name: 'Tệp đính kèm (PDF File Viewer)' },
            { id: 'approval', name: 'Luồng phê duyệt & Chứng thư CA' }
          ].map((t) => (
            <button
              key={t.id}
              onClick={() => setActiveTab(t.id as any)}
              className={`pb-3 relative transition-colors cursor-pointer ${
                activeTab === t.id ? 'text-white font-bold' : 'text-zinc-500 hover:text-zinc-300'
              }`}
            >
              {t.name}
              {activeTab === t.id && (
                <span className="absolute bottom-0 left-0 right-0 h-0.5 bg-white rounded-full"></span>
              )}
            </button>
          ))}
        </div>

        {/* TAB 1: CONTENT & VISUAL SIGNATURE STAMP */}
        {activeTab === 'info' && (
          <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
            
            {/* Main Document Content */}
            <div className="lg:col-span-2 space-y-6">
              
              <div className="p-8 bg-zinc-950 border border-zinc-800 rounded-xl space-y-6 shadow-sm relative overflow-hidden">
                
                {/* Header Republic Seal */}
                <div className="border-b border-zinc-800 pb-6 flex justify-between items-start text-xs font-serif">
                  <div className="space-y-1">
                    <p className="font-bold text-white uppercase tracking-wider">CÔNG TY CỔ PHẦN DAS ENTERPRISE</p>
                    <p className="text-zinc-400">Số: {doc.docNo}</p>
                  </div>
                  <div className="text-center space-y-1">
                    <p className="font-bold text-white uppercase tracking-wider">CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM</p>
                    <p className="font-bold text-zinc-300">Độc lập - Tự do - Hạnh phúc</p>
                    <p className="text-zinc-500 text-[10px] italic">Hà Nội, ngày 17 tháng 07 năm 2026</p>
                  </div>
                </div>

                {/* Subject & Body */}
                <div className="space-y-4 py-2">
                  <h3 className="text-base font-extrabold text-white text-center uppercase tracking-tight font-serif leading-snug">
                    {doc.subject}
                  </h3>
                  
                  <div className="text-xs text-zinc-300 leading-relaxed space-y-3 font-serif">
                    <p>{doc.summary}</p>
                  </div>
                </div>

                {/* 2-PART DIGITAL SIGNATURE DISPLAY */}
                <div className="pt-8 border-t border-zinc-800 flex justify-between items-end text-xs font-serif">
                  <div className="space-y-1 text-zinc-400">
                    <p className="font-bold text-white uppercase tracking-wider">Nơi nhận:</p>
                    <p>- Toàn thể CBNV;</p>
                    <p>- Lưu: VT, HC.</p>
                  </div>

                  <div className="text-center space-y-2 relative pr-4 min-w-[240px]">
                    <p className="font-bold text-white uppercase tracking-wider">GIÁM ĐỐC CÔNG TY</p>
                    
                    {signed ? (
                      <div className="relative my-2 p-3 border-2 border-red-600 rounded-xl bg-red-950/20 backdrop-blur-xs text-red-500 font-serif text-center shadow-lg transform rotate-[-2deg]">
                        <div className="absolute -top-3 -right-3 w-16 h-16 rounded-full border-2 border-red-600 flex items-center justify-center opacity-85 pointer-events-none rotate-12 bg-red-950/40">
                          <span className="text-[7px] font-bold text-red-500 text-center uppercase leading-tight">CÔNG TY DAS<br/>★<br/>ĐÃ KÝ SỐ</span>
                        </div>
                        
                        <div className="text-xl font-extrabold italic text-red-500 tracking-wider my-1">
                          NguyenVanDirector
                        </div>
                        
                        <div className="text-[9px] font-mono font-semibold text-red-400 text-left border-t border-red-600/40 pt-1.5 mt-1 space-y-0.5">
                          <p className="truncate">Ký bởi: Nguyễn Văn Director</p>
                          <p>Ngày ký: {signedDate}</p>
                          <p className="text-[8px] opacity-90 truncate">SHA-256: {hashValue}</p>
                          <p className="text-[8px] text-emerald-400 font-bold">✓ CA Verified (Chính phủ PKCS#7)</p>
                        </div>
                      </div>
                    ) : (
                      <div className="h-28 flex flex-col items-center justify-center text-amber-400/90 text-xs italic border border-dashed border-amber-800/60 rounded-xl my-2 p-3 bg-amber-950/10">
                        <span className="font-bold text-amber-300 not-italic mb-1">⏳ CHỜ PHÊ DUYỆT KÝ SỐ</span>
                        <p className="text-[10px] text-zinc-400">Vui lòng chọn "Thực hiện Ký số CA" để xác thực mã PIN.</p>
                      </div>
                    )}

                    <p className="font-bold text-white">Nguyễn Văn Director</p>
                  </div>
                </div>

              </div>

              {/* PDF File Attachment Card */}
              <div className="p-6 bg-zinc-950 border border-zinc-800 rounded-xl space-y-4 shadow-sm">
                <div className="flex justify-between items-center">
                  <h4 className="font-bold text-xs uppercase tracking-wider text-white flex items-center gap-2">
                    <svg className="w-4 h-4 text-red-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                      <path strokeLinecap="round" strokeLinejoin="round" d="M7 21h10a2 2 0 002-2V9.414a1 1 0 00-.293-.707l-5.414-5.414A1 1 0 0012.586 3H7a2 2 0 00-2 2v14a2 2 0 002 2z" />
                    </svg>
                    File đính kèm: {doc.docNo}_Signed.pdf
                  </h4>
                  <div className="flex items-center gap-2">
                    {signed ? (
                      <span className="px-2.5 py-1 border border-emerald-800 text-emerald-400 bg-emerald-950/40 text-[10px] font-bold rounded">
                        🔒 Bản gốc PDF đã ký số
                      </span>
                    ) : (
                      <span className="px-2.5 py-1 border border-amber-800 text-amber-300 bg-amber-950/40 text-[10px] font-bold rounded">
                        ⏳ Chưa ký số
                      </span>
                    )}
                    <button 
                      onClick={() => setActiveTab('pdf')}
                      className="px-3 py-1 bg-zinc-900 border border-zinc-800 text-xs font-semibold text-zinc-300 hover:text-white rounded-lg cursor-pointer"
                    >
                      Xem toàn màn hình PDF
                    </button>
                  </div>
                </div>

                <div className="w-full h-[450px] border border-zinc-800 rounded-xl bg-zinc-900 overflow-hidden relative">
                  <iframe 
                    src="/documents/samples/outgoing-sample.pdf"
                    className="w-full h-full border-none"
                    title="Tài liệu PDF Công văn"
                  />
                </div>
              </div>

            </div>

            {/* Right Info Sidebar */}
            <div className="space-y-6">
              <div className="p-6 bg-zinc-950 border border-zinc-800 rounded-xl space-y-4 shadow-sm">
                <h4 className="font-bold text-xs uppercase tracking-wider text-white">Thông tin công văn</h4>
                <div className="space-y-3 text-xs">
                  <div>
                    <span className="text-zinc-500 block text-[10px] font-bold uppercase">Trạng thái hiện tại:</span>
                    <span className={`font-bold ${signed ? 'text-emerald-400' : 'text-amber-400'}`}>
                      {signed ? 'Đã ký số ban hành' : 'Chờ xử lý (Chưa ký)'}
                    </span>
                  </div>
                  {signed && (
                    <div>
                      <span className="text-zinc-500 block text-[10px] font-bold uppercase">Mã Hash SHA-256:</span>
                      <span className="font-mono text-[10px] text-emerald-400 break-all">{hashValue}</span>
                    </div>
                  )}
                </div>
              </div>

              {/* Dynamic QR Code Share Box */}
              <DocumentQRCode docNo={doc.docNo} />
            </div>

          </div>
        )}

        {/* TAB 2: FULL PDF FILE VIEWER */}
        {activeTab === 'pdf' && (
          <div className="p-6 bg-zinc-950 border border-zinc-800 rounded-xl space-y-4 shadow-sm">
            <div className="flex justify-between items-center">
              <h4 className="font-bold text-xs uppercase tracking-wider text-white">Trình xem File PDF Công văn đính kèm</h4>
              <a 
                href="/documents/samples/outgoing-sample.pdf" 
                download
                className="px-4 py-2 bg-white text-black font-bold rounded-lg text-xs hover:bg-zinc-200 cursor-pointer shadow-sm"
              >
                Tải xuống PDF gốc
              </a>
            </div>
            
            <div className="w-full h-[650px] border border-zinc-800 rounded-xl bg-zinc-900 overflow-hidden relative">
              <iframe 
                src="/documents/samples/outgoing-sample.pdf"
                className="w-full h-full border-none"
                title="Tài liệu PDF Công văn"
              />
            </div>
          </div>
        )}

        {/* TAB 3: APPROVAL DETAILS */}
        {activeTab === 'approval' && (
          <div className="p-6 bg-zinc-950 border border-zinc-800 rounded-xl space-y-8 shadow-sm">
            <h4 className="font-bold text-xs uppercase tracking-wider text-white">Tiến trình Trình ký & Chứng thư CA 2 thành phần</h4>
            
            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
              <div className="p-5 border border-zinc-800 rounded-xl bg-zinc-900/50 space-y-3">
                <div className="flex items-center gap-2">
                  <span className="p-2 bg-emerald-950 border border-emerald-800 text-emerald-400 rounded-lg text-base">🔒</span>
                  <div>
                    <h5 className="font-bold text-sm text-white">Phần 1: Mã hóa Thuật toán SHA-256 & Chứng thư CA</h5>
                    <p className="text-[11px] text-zinc-400">Đảm bảo tính chống chối bỏ & tính toàn vẹn dữ liệu gốc</p>
                  </div>
                </div>
                <div className="text-xs font-mono bg-black p-3 rounded-lg border border-zinc-800 space-y-1 text-zinc-300">
                  <p><span className="text-zinc-500">Thuật toán:</span> SHA-256 with RSA 2048-bit</p>
                  <p><span className="text-zinc-500">Chứng thư:</span> CA-2026-DAS-GOV-SIGNED</p>
                  <p><span className="text-zinc-500">Trạng thái:</span> <span className={signed ? 'text-emerald-400 font-bold' : 'text-amber-400 font-bold'}>{signed ? '✓ HASH MATCHED (Đã ký)' : '⏳ CHỜ KÝ SỐ'}</span></p>
                  {signed && <p className="text-[10px] text-zinc-500 break-all pt-1">Hash: {hashValue}</p>}
                </div>
              </div>

              <div className="p-5 border border-zinc-800 rounded-xl bg-zinc-900/50 space-y-3">
                <div className="flex items-center gap-2">
                  <span className="p-2 bg-red-950 border border-red-800 text-red-400 rounded-lg text-base">🖋️</span>
                  <div>
                    <h5 className="font-bold text-sm text-white">Phần 2: Hình chữ ký số & Con dấu đỏ (Bản in)</h5>
                    <p className="text-[11px] text-zinc-400">Phục vụ in ra giấy vẫn đầy đủ hình chữ ký và dấu đỏ</p>
                  </div>
                </div>
                <div className="text-xs bg-black p-3 rounded-lg border border-zinc-800 flex items-center justify-between">
                  <div>
                    <p className="font-bold text-white">Con dấu: CÔNG TY DAS ★ ĐÃ KÝ SỐ</p>
                    <p className="text-[10px] text-zinc-400">Quy chuẩn Nghị định 30/2020/NĐ-CP của Chính phủ</p>
                  </div>
                  <span className={`px-2.5 py-1 border font-extrabold text-[10px] rounded ${
                    signed ? 'bg-red-950/80 border-red-800 text-red-400' : 'bg-zinc-900 border-zinc-800 text-zinc-500'
                  }`}>
                    {signed ? 'RED SEAL OK' : 'NO SEAL'}
                  </span>
                </div>
              </div>
            </div>
          </div>
        )}

      </main>

      {/* Dynamic Digital Signing Modal */}
      {signingModal && (
        <div className="fixed inset-0 bg-black/80 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-zinc-950 border border-zinc-800 w-full max-w-md rounded-2xl shadow-2xl overflow-hidden p-6 space-y-5">
            <div className="text-center space-y-2">
              <div className="w-12 h-12 rounded-xl bg-emerald-950 border border-emerald-800 text-emerald-400 flex items-center justify-center mx-auto text-xl">
                🔏
              </div>
              <h3 className="font-extrabold text-white text-base">Xác thực Ký số Điện tử 2 Thành phần</h3>
              <p className="text-xs text-zinc-400 font-medium">Hệ thống sẽ thực hiện mã hóa Hash SHA-256 và chèn con dấu đỏ điện tử vào công văn.</p>
            </div>

            <div className="space-y-4">
              <div>
                <label className="block text-[10px] font-bold text-zinc-500 mb-1.5 uppercase tracking-wide">1. Chứng thư số CA cá nhân</label>
                <select className="w-full px-3 py-2 border border-zinc-800 bg-zinc-900 rounded-lg text-xs text-white font-semibold focus:outline-none">
                  <option>Giám đốc Nguyễn Văn Director (CA-2026-DAS)</option>
                </select>
              </div>

              <div>
                <label className="block text-[10px] font-bold text-zinc-500 mb-1.5 uppercase tracking-wide">2. Mã PIN xác thực chữ ký (Mã thử nghiệm: 1234)</label>
                <input
                  type="password"
                  value={pinCode}
                  onChange={(e) => setPinCode(e.target.value)}
                  placeholder="Mã PIN"
                  className="w-full px-3 py-2 border border-zinc-800 bg-zinc-900 rounded-lg text-xs text-center text-white font-bold tracking-widest focus:outline-none focus:border-zinc-500"
                />
              </div>
            </div>

            <div className="pt-2 flex justify-end gap-3 text-xs font-bold">
              <button
                onClick={() => setSigningModal(false)}
                className="px-4 py-2 border border-zinc-800 bg-zinc-900 text-zinc-300 hover:text-white rounded-lg cursor-pointer"
              >
                Hủy bỏ
              </button>
              <button
                onClick={handleSign}
                className="px-5 py-2 bg-emerald-600 hover:bg-emerald-500 text-white rounded-lg cursor-pointer shadow-md font-bold"
              >
                Xác nhận Ký & Đóng dấu
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  );
}
