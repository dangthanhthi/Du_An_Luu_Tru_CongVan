'use client';
import Sidebar from '@/components/Sidebar';
import { useRouter } from 'next/navigation';
import { useEffect, useState } from 'react';

export default function MainLayout({ children }: { children: React.ReactNode }) {
  const router = useRouter();
  const [mounted, setMounted] = useState(false);

  useEffect(() => {
    const token = localStorage.getItem('das-token');
    if (!token) {
      router.push('/login');
    } else {
      setMounted(true);
    }
  }, [router]);

  if (!mounted) return null;

  return (
    <div className="flex bg-black min-h-screen text-zinc-100 vercel-bg-pattern font-sans antialiased selection:bg-white selection:text-black">
      <Sidebar />
      <div className="flex-1 flex flex-col min-w-0 pl-20 transition-all duration-300">
        {children}
      </div>
    </div>
  );
}
