'use client';
import { useState, useEffect } from 'react';
import { QRCodeSVG } from 'qrcode.react';

interface DocumentQRCodeProps {
  docNo: string;
  title?: string;
}

export default function DocumentQRCode({ docNo, title = 'Mã QR chia sẻ & truy cập nhanh' }: DocumentQRCodeProps) {
  const [shareUrl, setShareUrl] = useState('');
  const [copied, setCopied] = useState(false);
  const [showModal, setShowModal] = useState(false);

  useEffect(() => {
    if (typeof window !== 'undefined') {
      setShareUrl(window.location.href);
    }
  }, []);

  const handleCopy = () => {
    if (shareUrl) {
      navigator.clipboard.writeText(shareUrl);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    }
  };

  return (
    <div className="p-6 bg-zinc-950 border border-zinc-800 rounded-xl text-center space-y-4 shadow-sm">
      <div className="flex items-center justify-between">
        <h4 className="font-bold text-xs uppercase tracking-wider text-white text-left">{title}</h4>
        <span className="text-[10px] text-emerald-400 font-mono font-bold bg-emerald-950/40 border border-emerald-800/50 px-2 py-0.5 rounded-full flex items-center gap-1">
          <span className="w-1.5 h-1.5 rounded-full bg-emerald-500 animate-pulse"></span>
          Live Link
        </span>
      </div>

      {/* Interactive QR Code Container */}
      <div 
        onClick={() => setShowModal(true)}
        className="w-44 h-44 bg-white border border-zinc-700 rounded-xl mx-auto flex items-center justify-center p-3 cursor-pointer transition-transform hover:scale-105 shadow-md relative group"
        title="Click để phóng to mã QR chia sẻ"
      >
        {shareUrl ? (
          <QRCodeSVG 
            value={shareUrl} 
            size={150} 
            bgColor="#ffffff" 
            fgColor="#000000" 
            level="H" 
            includeMargin={false} 
          />
        ) : (
          <div className="text-zinc-400 text-xs font-mono">Đang tạo mã QR...</div>
        )}
        <div className="absolute inset-0 bg-black/60 rounded-xl opacity-0 group-hover:opacity-100 transition-opacity flex items-center justify-center text-white text-xs font-bold gap-1.5">
          <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0zM10 7v6m3-3H7" />
          </svg>
          Phóng to
        </div>
      </div>

      <p className="text-[11px] text-zinc-400 font-medium leading-relaxed">
        Quét mã QR để mở trực tiếp tài liệu <span className="font-mono font-bold text-white">{docNo}</span> trên thiết bị di động.
      </p>

      {/* Action Buttons */}
      <div className="pt-2 flex flex-col gap-2">
        <button
          onClick={handleCopy}
          className={`w-full py-2 px-3 rounded-lg text-xs font-semibold transition-all flex items-center justify-center gap-2 cursor-pointer shadow-sm ${
            copied
              ? 'bg-emerald-600 text-white border border-emerald-500'
              : 'bg-zinc-900 border border-zinc-800 text-white hover:bg-zinc-800 hover:border-zinc-700'
          }`}
        >
          {copied ? (
            <>
              <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7" />
              </svg>
              Đã sao chép liên kết!
            </>
          ) : (
            <>
              <svg className="w-4 h-4 text-zinc-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M8 16H6a2 2 0 01-2-2V6a2 2 0 012-2h8a2 2 0 012 2v2m-6 12h8a2 2 0 002-2v-8a2 2 0 00-2-2h-8a2 2 0 00-2 2v8a2 2 0 002 2z" />
              </svg>
              Sao chép Link chia sẻ
            </>
          )}
        </button>
      </div>

      {/* Modal Phóng to QR Code */}
      {showModal && (
        <div className="fixed inset-0 bg-black/80 backdrop-blur-md z-50 flex items-center justify-center p-4">
          <div className="bg-zinc-950 border border-zinc-800 w-full max-w-sm rounded-2xl p-6 text-center space-y-5 shadow-2xl relative">
            <button
              onClick={() => setShowModal(false)}
              className="absolute top-4 right-4 text-zinc-400 hover:text-white p-1 rounded-lg hover:bg-zinc-900 cursor-pointer"
            >
              <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12" />
              </svg>
            </button>

            <div>
              <h3 className="font-extrabold text-white text-base">Mã QR Chia sẻ Công văn</h3>
              <p className="text-xs text-zinc-400 font-mono mt-1">{docNo}</p>
            </div>

            <div className="w-56 h-56 bg-white border border-zinc-700 rounded-xl mx-auto flex items-center justify-center p-4 shadow-lg">
              <QRCodeSVG 
                value={shareUrl} 
                size={200} 
                bgColor="#ffffff" 
                fgColor="#000000" 
                level="H" 
                includeMargin={false} 
              />
            </div>

            <div className="text-left bg-zinc-900 border border-zinc-800 p-3 rounded-lg text-xs font-mono text-zinc-300 break-all">
              <span className="text-[10px] text-zinc-500 font-bold block mb-1 uppercase">URL Truy cập:</span>
              {shareUrl}
            </div>

            <button
              onClick={handleCopy}
              className={`w-full py-2.5 px-4 rounded-xl text-xs font-bold transition-all flex items-center justify-center gap-2 cursor-pointer shadow-md ${
                copied
                  ? 'bg-emerald-600 text-white'
                  : 'bg-white text-black hover:bg-zinc-200'
              }`}
            >
              {copied ? 'Đã sao chép liên kết!' : 'Sao chép liên kết công văn'}
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
