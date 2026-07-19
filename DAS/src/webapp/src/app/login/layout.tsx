import React from 'react';

export default function LoginLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="min-h-screen flex items-center justify-center bg-black transition-colors duration-300 relative overflow-hidden select-none">
      {/* Glow effect at the center similar to Vercel */}
      <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[500px] h-[500px] bg-zinc-900/30 rounded-full blur-[120px] pointer-events-none z-0"></div>
      
      {/* Center card wrapper */}
      <div className="z-10 relative w-full flex items-center justify-center p-4">
        {children}
      </div>
    </div>
  );
}
