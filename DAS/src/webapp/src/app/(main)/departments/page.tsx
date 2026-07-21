'use client';
import { useState, useEffect } from 'react';
import Header from '@/components/Header';

interface DepartmentItem {
  id: string;
  name: string;
  code: string;
  manager: string;
  secretary: string;
  count: number;
}

export default function Departments() {
  const [depts, setDepts] = useState<DepartmentItem[]>([]);
  const [searchTerm, setSearchTerm] = useState('');
  const [isModalOpen, setIsModalOpen] = useState(false);
  
  // Form fields
  const [name, setName] = useState('');
  const [code, setCode] = useState('');
  const [manager, setManager] = useState('');
  const [secretary, setSecretary] = useState('');
  const [count, setCount] = useState(0);

  // Load from localStorage
  useEffect(() => {
    const stored = localStorage.getItem('custom_departments');
    if (stored) {
      setDepts(JSON.parse(stored));
    } else {
      const defaultDepts: DepartmentItem[] = [
        { id: '1', name: 'Ban Giám đốc', code: 'BGD', manager: 'Nguyễn Văn Director', secretary: 'Lê Thị Secretary', count: 3 },
        { id: '2', name: 'Phòng Hành chính', code: 'PHC', manager: 'Trần Văn Trưởng Phòng', secretary: 'Phạm Thị Thư Ký', count: 12 },
        { id: '3', name: 'Phòng Kế toán', code: 'PKT', manager: 'Nguyễn Kế Toán Trưởng', secretary: 'Lê Kế Toán Viên', count: 6 },
        { id: '4', name: 'Phòng Kỹ thuật', code: 'PKH', manager: 'Trần Văn Tech Lead', secretary: 'Lê Văn Dev', count: 25 },
        { id: '5', name: 'Phòng Kinh doanh', code: 'PKD', manager: 'Lê Thị Sales Manager', secretary: 'Phạm Văn Sales Staff', count: 15 }
      ];
      localStorage.setItem('custom_departments', JSON.stringify(defaultDepts));
      setDepts(defaultDepts);
    }
  }, []);

  const handleSave = (e: React.FormEvent) => {
    e.preventDefault();
    if (!name || !code) {
      alert('Vui lòng nhập tên và mã phòng ban!');
      return;
    }

    const newDept: DepartmentItem = {
      id: `dept-${Date.now()}`,
      name: name.trim(),
      code: code.trim().toUpperCase(),
      manager: manager.trim() || 'Chưa chỉ định',
      secretary: secretary.trim() || 'Chưa chỉ định',
      count: count || 0
    };

    const updated = [newDept, ...depts];
    localStorage.setItem('custom_departments', JSON.stringify(updated));
    setDepts(updated);
    
    // Reset Form & Close Modal
    setName('');
    setCode('');
    setManager('');
    setSecretary('');
    setCount(0);
    setIsModalOpen(false);
  };

  const filteredDepts = depts.filter(d => 
    d.name.toLowerCase().includes(searchTerm.toLowerCase()) || 
    d.code.toLowerCase().includes(searchTerm.toLowerCase())
  );

  return (
    <>
      <Header title="Danh sách phòng ban" />
      <main className="flex-1 p-8 space-y-6 max-w-7xl mx-auto w-full">
        
        {/* Controls */}
        <div className="flex justify-between items-center bg-zinc-950 p-4 border border-zinc-800 rounded-xl transition-all duration-200">
          <input
            type="text"
            placeholder="Tìm phòng ban (tên hoặc mã)..."
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            className="w-80 px-3 py-2 border border-zinc-800 bg-zinc-900 text-white rounded-lg text-sm focus:outline-none focus:border-emerald-600 font-medium"
          />
          <button 
            onClick={() => setIsModalOpen(true)}
            className="px-4 py-2 bg-emerald-600 hover:bg-emerald-500 text-white hover:opacity-90 rounded-lg text-sm font-bold transition-all shadow-sm cursor-pointer active:scale-95"
          >
            + Tạo phòng ban mới
          </button>
        </div>

        {/* Grid of Departments */}
        {filteredDepts.length > 0 ? (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            {filteredDepts.map((d) => (
              <div key={d.id} className="p-6 bg-zinc-950 border border-zinc-800 hover:border-zinc-700 rounded-2xl space-y-4 shadow-[0_4px_20px_-4px_rgba(0,0,0,0.02)] transition-all duration-200">
                <div className="flex justify-between items-start">
                  <div>
                    <h4 className="font-bold text-white text-base leading-tight mb-1.5">{d.name}</h4>
                    <span className="text-[10px] px-2.5 py-0.5 bg-zinc-900 border border-zinc-800 text-emerald-400 font-extrabold rounded-md uppercase tracking-wider">{d.code}</span>
                  </div>
                  <span className="text-[10px] text-zinc-400 font-bold uppercase tracking-wider bg-zinc-900 border border-zinc-800 px-2 py-0.5 rounded-full">{d.count} thành viên</span>
                </div>
                <div className="space-y-2.5 text-xs border-t border-zinc-900 pt-4 font-medium">
                  <div className="flex justify-between">
                    <span className="text-zinc-500">Trưởng phòng:</span>
                    <span className="font-bold text-white">{d.manager}</span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-zinc-500">Thư ký phòng:</span>
                    <span className="font-semibold text-zinc-300">{d.secretary}</span>
                  </div>
                </div>
              </div>
            ))}
          </div>
        ) : (
          <div className="p-12 text-center border border-dashed border-zinc-800 rounded-2xl bg-zinc-950/20 text-zinc-500">
            Không tìm thấy phòng ban nào phù hợp.
          </div>
        )}

      </main>

      {/* Creation Modal */}
      {isModalOpen && (
        <div className="fixed inset-0 bg-black/60 backdrop-blur-xs flex items-center justify-center z-50 p-4">
          <div className="bg-zinc-950 border border-zinc-800 w-full max-w-md rounded-2xl shadow-2xl p-6 space-y-4 animate-scale-in text-white">
            <div className="flex justify-between items-center pb-3 border-b border-zinc-900">
              <h3 className="text-sm font-extrabold uppercase tracking-wider text-emerald-400">Tạo phòng ban mới</h3>
              <button 
                onClick={() => setIsModalOpen(false)}
                className="text-zinc-400 hover:text-white cursor-pointer"
              >
                ✕
              </button>
            </div>

            <form onSubmit={handleSave} className="space-y-4 text-xs font-semibold">
              <div className="space-y-1.5">
                <label className="text-zinc-400">Tên phòng ban <span className="text-red-500">*</span></label>
                <input 
                  type="text" 
                  required
                  placeholder="Ví dụ: Phòng Hành chính" 
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                  className="w-full px-3 py-2 border border-zinc-850 bg-zinc-900 rounded-lg text-white font-medium focus:outline-none focus:border-emerald-600"
                />
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-1.5">
                  <label className="text-zinc-400">Mã phòng ban <span className="text-red-500">*</span></label>
                  <input 
                    type="text" 
                    required
                    placeholder="Ví dụ: PHC" 
                    value={code}
                    onChange={(e) => setCode(e.target.value)}
                    className="w-full px-3 py-2 border border-zinc-850 bg-zinc-900 rounded-lg text-white font-medium focus:outline-none focus:border-emerald-600 uppercase"
                  />
                </div>
                <div className="space-y-1.5">
                  <label className="text-zinc-400">Số thành viên</label>
                  <input 
                    type="number" 
                    min="0"
                    placeholder="0" 
                    value={count || ''}
                    onChange={(e) => setCount(parseInt(e.target.value, 10) || 0)}
                    className="w-full px-3 py-2 border border-zinc-850 bg-zinc-900 rounded-lg text-white font-medium focus:outline-none focus:border-emerald-600"
                  />
                </div>
              </div>

              <div className="space-y-1.5">
                <label className="text-zinc-400">Trưởng phòng</label>
                <input 
                  type="text" 
                  placeholder="Ví dụ: Nguyễn Văn A" 
                  value={manager}
                  onChange={(e) => setManager(e.target.value)}
                  className="w-full px-3 py-2 border border-zinc-850 bg-zinc-900 rounded-lg text-white font-medium focus:outline-none focus:border-emerald-600"
                />
              </div>

              <div className="space-y-1.5">
                <label className="text-zinc-400">Thư ký phòng</label>
                <input 
                  type="text" 
                  placeholder="Ví dụ: Lê Thị B" 
                  value={secretary}
                  onChange={(e) => setSecretary(e.target.value)}
                  className="w-full px-3 py-2 border border-zinc-850 bg-zinc-900 rounded-lg text-white font-medium focus:outline-none focus:border-emerald-600"
                />
              </div>

              <div className="flex justify-end gap-3 pt-3 border-t border-zinc-900">
                <button 
                  type="button"
                  onClick={() => setIsModalOpen(false)}
                  className="px-4 py-2 border border-zinc-800 bg-zinc-900 hover:bg-zinc-850 rounded-lg text-zinc-300 cursor-pointer font-bold"
                >
                  Hủy
                </button>
                <button 
                  type="submit"
                  className="px-4 py-2 bg-emerald-600 hover:bg-emerald-500 rounded-lg text-white cursor-pointer font-bold"
                >
                  Lưu lại
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </>
  );
}
