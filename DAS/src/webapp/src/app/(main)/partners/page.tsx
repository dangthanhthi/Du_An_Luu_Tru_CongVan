'use client';
import { useState, useEffect } from 'react';
import Header from '@/components/Header';

interface Partner {
  id: string;
  name: string;
  shortName: string;
  taxCode: string;
  contact: string;
  phone: string;
  email: string;
  type: string;
  isDeleted?: boolean;
}

const DEFAULT_PARTNERS: Partner[] = [
  { id: '1', name: 'Tập đoàn Công nghệ Viễn thông VNPT', shortName: 'VNPT', taxCode: '0100684398', contact: 'Nguyễn Văn A', phone: '0243789789', email: 'contact@vnpt.vn', type: 'Doanh nghiệp', isDeleted: false },
  { id: '2', name: 'Cục Kiểm soát Thủ tục Hành chính', shortName: 'KSTTHC', taxCode: '0102030405', contact: 'Trần Thị B', phone: '0243654321', email: 'kstthc@chinhphu.vn', type: 'Cơ quan nhà nước', isDeleted: false },
  { id: '3', name: 'Ngân hàng Thương mại Cổ phần Ngoại thương Việt Nam', shortName: 'Vietcombank', taxCode: '0100112437', contact: 'Phạm Văn C', phone: '0243934313', email: 'contact@vietcombank.com.vn', type: 'Doanh nghiệp', isDeleted: false }
];

export default function Partners() {
  const [partners, setPartners] = useState<Partner[]>([]);
  const [searchQuery, setSearchQuery] = useState('');
  const [typeFilter, setTypeFilter] = useState('Tất cả loại');
  const [showDeleted, setShowDeleted] = useState(false);

  // Modal states
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingPartner, setEditingPartner] = useState<Partner | null>(null);

  // Form states
  const [name, setName] = useState('');
  const [shortName, setShortName] = useState('');
  const [taxCode, setTaxCode] = useState('');
  const [contact, setContact] = useState('');
  const [phone, setPhone] = useState('');
  const [email, setEmail] = useState('');
  const [type, setType] = useState('Doanh nghiệp');

  // Load partners from localStorage on mount
  useEffect(() => {
    const saved = localStorage.getItem('das-partners');
    if (saved) {
      try {
        setPartners(JSON.parse(saved));
      } catch (e) {
        setPartners(DEFAULT_PARTNERS);
      }
    } else {
      setPartners(DEFAULT_PARTNERS);
      localStorage.setItem('das-partners', JSON.stringify(DEFAULT_PARTNERS));
    }
  }, []);

  // Save partners to localStorage helper
  const savePartners = (list: Partner[]) => {
    setPartners(list);
    localStorage.setItem('das-partners', JSON.stringify(list));
  };

  // Open modal for add
  const openAddModal = () => {
    setEditingPartner(null);
    setName('');
    setShortName('');
    setTaxCode('');
    setContact('');
    setPhone('');
    setEmail('');
    setType('Doanh nghiệp');
    setIsModalOpen(true);
  };

  // Open modal for edit
  const openEditModal = (partner: Partner) => {
    setEditingPartner(partner);
    setName(partner.name);
    setShortName(partner.shortName);
    setTaxCode(partner.taxCode);
    setContact(partner.contact);
    setPhone(partner.phone);
    setEmail(partner.email);
    setType(partner.type);
    setIsModalOpen(true);
  };

  // Handle Save (Add or Update)
  const handleSave = (e: React.FormEvent) => {
    e.preventDefault();

    if (editingPartner) {
      // Update
      const updatedList = partners.map((p) =>
        p.id === editingPartner.id
          ? { ...p, name, shortName, taxCode, contact, phone, email, type }
          : p
      );
      savePartners(updatedList);
    } else {
      // Create new
      const newPartner: Partner = {
        id: String(Date.now()),
        name,
        shortName,
        taxCode,
        contact,
        phone,
        email,
        type,
        isDeleted: false
      };
      savePartners([newPartner, ...partners]);
    }

    setIsModalOpen(false);
  };

  // Handle Soft Delete
  const handleSoftDelete = (id: string) => {
    const updatedList = partners.map((p) =>
      p.id === id ? { ...p, isDeleted: true } : p
    );
    savePartners(updatedList);
  };

  // Handle Restore
  const handleRestore = (id: string) => {
    const updatedList = partners.map((p) =>
      p.id === id ? { ...p, isDeleted: false } : p
    );
    savePartners(updatedList);
  };

  // Filter logic
  const filteredPartners = partners.filter((p) => {
    const matchesDeleted = showDeleted ? true : !p.isDeleted;
    const matchesType = typeFilter === 'Tất cả loại' ? true : p.type === typeFilter;
    
    const q = searchQuery.toLowerCase();
    const matchesSearch =
      p.name.toLowerCase().includes(q) ||
      p.shortName.toLowerCase().includes(q) ||
      p.taxCode.toLowerCase().includes(q) ||
      p.contact.toLowerCase().includes(q) ||
      p.email.toLowerCase().includes(q);

    return matchesDeleted && matchesType && matchesSearch;
  });

  return (
    <>
      <Header title="Quản lý đối tác gửi/nhận" />
      <main className="flex-1 p-8 space-y-6 max-w-7xl mx-auto w-full">
        
        {/* Controls Bar */}
        <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 bg-white dark:bg-zinc-950/80 p-4 border border-zinc-200 dark:border-zinc-800 rounded-xl transition-all duration-200 shadow-xs dark:shadow-sm">
          <div className="flex flex-wrap items-center gap-3">
            
            {/* Search Bar */}
            <div className="relative flex items-center w-full md:w-64">
              <input
                type="text"
                placeholder="Tìm kiếm đối tác..."
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                className="w-full pl-9 pr-8 py-2 bg-zinc-50 dark:bg-black border border-zinc-200 dark:border-zinc-800 rounded-lg text-xs focus:outline-none focus:border-zinc-400 dark:focus:border-zinc-500 text-zinc-900 dark:text-white placeholder-zinc-400 dark:placeholder-zinc-500 font-medium transition-colors"
              />
              <span className="absolute left-3 text-zinc-400 dark:text-zinc-500 pointer-events-none">
                <svg className="w-3.5 h-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
                  <path strokeLinecap="round" strokeLinejoin="round" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
                </svg>
              </span>
            </div>
            
            <select
              value={typeFilter}
              onChange={(e) => setTypeFilter(e.target.value)}
              className="px-3 py-2 bg-zinc-50 dark:bg-black border border-zinc-200 dark:border-zinc-800 rounded-lg text-xs font-semibold text-zinc-700 dark:text-zinc-300 focus:outline-none"
            >
              <option value="Tất cả loại">Tất cả loại đối tác</option>
              <option value="Doanh nghiệp">Doanh nghiệp</option>
              <option value="Cơ quan nhà nước">Cơ quan nhà nước</option>
              <option value="Tổ chức">Tổ chức</option>
            </select>

            <label className="flex items-center gap-2 text-xs font-medium text-zinc-500 dark:text-zinc-400 select-none cursor-pointer">
              <input
                type="checkbox"
                checked={showDeleted}
                onChange={(e) => setShowDeleted(e.target.checked)}
                className="w-4 h-4 rounded border-zinc-300 dark:border-zinc-800 bg-zinc-50 dark:bg-black text-zinc-900 dark:text-white focus:ring-0"
              />
              Hiển thị đối tác đã ẩn (Soft Deleted)
            </label>
          </div>
          
          <button
            onClick={openAddModal}
            className="px-4 py-2 bg-zinc-900 dark:bg-white text-white dark:text-black hover:bg-zinc-800 dark:hover:bg-zinc-200 rounded-lg text-xs font-bold transition-all active:scale-95 duration-150 flex items-center gap-2 self-start md:self-auto shadow-xs cursor-pointer"
          >
            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" />
            </svg>
            Thêm đối tác
          </button>
        </div>

        {/* Partners Table */}
        <div className="bg-white dark:bg-zinc-950/80 border border-zinc-200 dark:border-zinc-800 rounded-xl overflow-hidden shadow-xs dark:shadow-sm transition-all duration-200">
          <div className="overflow-x-auto">
            <table className="w-full text-sm text-left">
              <thead>
                <tr className="border-b border-zinc-100 dark:border-zinc-900 text-zinc-500 text-[10px] uppercase font-bold tracking-wider bg-zinc-50/50 dark:bg-black/40">
                  <th className="py-3.5 px-6">Tên đầy đủ</th>
                  <th className="py-3.5 px-6">Tên viết tắt</th>
                  <th className="py-3.5 px-6">Mã số thuế</th>
                  <th className="py-3.5 px-6">Liên hệ</th>
                  <th className="py-3.5 px-6">SĐT</th>
                  <th className="py-3.5 px-6">Email</th>
                  <th className="py-3.5 px-6">Loại</th>
                  <th className="py-3.5 px-6">Hành động</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-zinc-100 dark:divide-zinc-900">
                {filteredPartners.length > 0 ? (
                  filteredPartners.map((p) => (
                    <tr 
                      key={p.id} 
                      className={`hover:bg-zinc-50/80 dark:hover:bg-zinc-900/40 transition-colors ${
                        p.isDeleted ? 'opacity-40 bg-zinc-50/50 dark:bg-zinc-950/30' : ''
                      }`}
                    >
                      <td className="py-4 px-6 text-zinc-900 dark:text-white font-bold max-w-xs">{p.name}</td>
                      <td className="py-4 px-6 font-mono font-semibold text-zinc-700 dark:text-zinc-300">{p.shortName}</td>
                      <td className="py-4 px-6 text-zinc-500 font-mono text-xs">{p.taxCode}</td>
                      <td className="py-4 px-6 text-zinc-800 dark:text-zinc-300 font-medium">{p.contact}</td>
                      <td className="py-4 px-6 text-zinc-500 font-mono text-xs">{p.phone}</td>
                      <td className="py-4 px-6 text-zinc-500 dark:text-zinc-400 font-mono text-xs">{p.email}</td>
                      <td className="py-4 px-6">
                        <span className="px-2 py-0.5 bg-zinc-100 dark:bg-zinc-900 border border-zinc-200 dark:border-zinc-800 text-zinc-700 dark:text-zinc-300 rounded-full text-[10px] font-semibold">
                          {p.type}
                        </span>
                      </td>
                      <td className="py-4 px-6 text-xs font-bold whitespace-nowrap">
                        <div className="flex gap-3">
                          <button 
                            onClick={() => openEditModal(p)}
                            className="text-zinc-700 hover:text-black dark:text-zinc-300 dark:hover:text-white hover:underline cursor-pointer"
                          >
                            Sửa
                          </button>
                          {p.isDeleted ? (
                            <button 
                              onClick={() => handleRestore(p.id)}
                              className="text-emerald-600 dark:text-emerald-400 hover:underline cursor-pointer"
                            >
                              Khôi phục
                            </button>
                          ) : (
                            <button 
                              onClick={() => handleSoftDelete(p.id)}
                              className="text-red-500 dark:text-red-400 hover:underline cursor-pointer"
                            >
                              Xóa (Ẩn)
                            </button>
                          )}
                        </div>
                      </td>
                    </tr>
                  ))
                ) : (
                  <tr>
                    <td colSpan={8} className="py-12 text-center text-xs text-zinc-500 font-medium">
                      Không tìm thấy đối tác nào phù hợp.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </div>

      </main>

      {/* Modal */}
      {isModalOpen && (
        <div className="fixed inset-0 bg-black/40 dark:bg-black/80 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white dark:bg-zinc-950 border border-zinc-200 dark:border-zinc-800 w-full max-w-lg rounded-xl shadow-2xl overflow-hidden transition-all duration-200">
            <div className="px-6 py-4 border-b border-zinc-100 dark:border-zinc-900 flex justify-between items-center">
              <h3 className="font-bold text-zinc-900 dark:text-white text-base">
                {editingPartner ? 'Cập nhật thông tin đối tác' : 'Thêm đối tác mới'}
              </h3>
              <button 
                onClick={() => setIsModalOpen(false)}
                className="text-zinc-400 hover:text-zinc-900 dark:hover:text-white cursor-pointer"
              >
                <svg className="w-5 h-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
                  <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12" />
                </svg>
              </button>
            </div>
            
            <form onSubmit={handleSave} className="p-6 space-y-4">
              <div className="space-y-4">
                <div>
                  <label className="block text-[10px] font-bold text-zinc-500 mb-1 uppercase tracking-wide">Tên đầy đủ của tổ chức đối tác</label>
                  <input
                    type="text"
                    value={name}
                    onChange={(e) => setName(e.target.value)}
                    required
                    placeholder="Ví dụ: Tập đoàn Công nghệ Viễn thông VNPT..."
                    className="w-full px-3 py-2 bg-zinc-50 dark:bg-black border border-zinc-200 dark:border-zinc-800 rounded-lg text-xs focus:outline-none focus:border-zinc-400 dark:focus:border-zinc-500 text-zinc-900 dark:text-white font-medium transition-colors"
                  />
                </div>

                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <label className="block text-[10px] font-bold text-zinc-500 mb-1 uppercase tracking-wide">Tên viết tắt</label>
                    <input
                      type="text"
                      value={shortName}
                      onChange={(e) => setShortName(e.target.value)}
                      required
                      placeholder="Ví dụ: VNPT"
                      className="w-full px-3 py-2 bg-zinc-50 dark:bg-black border border-zinc-200 dark:border-zinc-800 rounded-lg text-xs focus:outline-none focus:border-zinc-400 dark:focus:border-zinc-500 text-zinc-900 dark:text-white font-medium transition-colors"
                    />
                  </div>

                  <div>
                    <label className="block text-[10px] font-bold text-zinc-500 mb-1 uppercase tracking-wide">Mã số thuế</label>
                    <input
                      type="text"
                      value={taxCode}
                      onChange={(e) => setTaxCode(e.target.value)}
                      required
                      placeholder="Mã số thuế"
                      className="w-full px-3 py-2 bg-zinc-50 dark:bg-black border border-zinc-200 dark:border-zinc-800 rounded-lg text-xs focus:outline-none focus:border-zinc-400 dark:focus:border-zinc-500 text-zinc-900 dark:text-white font-medium transition-colors"
                    />
                  </div>
                </div>

                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <label className="block text-[10px] font-bold text-zinc-500 mb-1 uppercase tracking-wide">Người liên hệ</label>
                    <input
                      type="text"
                      value={contact}
                      onChange={(e) => setContact(e.target.value)}
                      required
                      placeholder="Người liên hệ"
                      className="w-full px-3 py-2 bg-zinc-50 dark:bg-black border border-zinc-200 dark:border-zinc-800 rounded-lg text-xs focus:outline-none focus:border-zinc-400 dark:focus:border-zinc-500 text-zinc-900 dark:text-white font-medium transition-colors"
                    />
                  </div>

                  <div>
                    <label className="block text-[10px] font-bold text-zinc-500 mb-1 uppercase tracking-wide">Số điện thoại</label>
                    <input
                      type="text"
                      value={phone}
                      onChange={(e) => setPhone(e.target.value)}
                      required
                      placeholder="Số điện thoại"
                      className="w-full px-3 py-2 bg-zinc-50 dark:bg-black border border-zinc-200 dark:border-zinc-800 rounded-lg text-xs focus:outline-none focus:border-zinc-400 dark:focus:border-zinc-500 text-zinc-900 dark:text-white font-medium transition-colors"
                    />
                  </div>
                </div>

                <div>
                  <label className="block text-[10px] font-bold text-zinc-500 mb-1 uppercase tracking-wide">Email liên hệ</label>
                  <input
                    type="email"
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    required
                    placeholder="contact@vnpt.vn"
                    className="w-full px-3 py-2 bg-zinc-50 dark:bg-black border border-zinc-200 dark:border-zinc-800 rounded-lg text-xs focus:outline-none focus:border-zinc-400 dark:focus:border-zinc-500 text-zinc-900 dark:text-white font-medium transition-colors"
                  />
                </div>

                <div>
                  <label className="block text-[10px] font-bold text-zinc-500 mb-1 uppercase tracking-wide">Loại đối tác</label>
                  <select
                    value={type}
                    onChange={(e) => setType(e.target.value)}
                    className="w-full px-3 py-2 bg-zinc-50 dark:bg-black border border-zinc-200 dark:border-zinc-800 rounded-lg text-xs focus:outline-none focus:border-zinc-400 dark:focus:border-zinc-500 text-zinc-900 dark:text-white font-medium"
                  >
                    <option value="Doanh nghiệp">Doanh nghiệp</option>
                    <option value="Cơ quan nhà nước">Cơ quan nhà nước</option>
                    <option value="Tổ chức">Tổ chức</option>
                  </select>
                </div>
              </div>

              <div className="pt-4 border-t border-zinc-100 dark:border-zinc-900 flex justify-end gap-3">
                <button
                  type="button"
                  onClick={() => setIsModalOpen(false)}
                  className="px-4 py-2 border border-zinc-200 dark:border-zinc-800 bg-transparent text-zinc-600 dark:text-zinc-400 hover:text-zinc-900 dark:hover:text-white rounded-lg text-xs font-semibold cursor-pointer transition-all active:scale-95"
                >
                  Hủy bỏ
                </button>
                <button
                  type="submit"
                  className="px-4 py-2 bg-zinc-900 dark:bg-white text-white dark:text-black hover:bg-zinc-800 dark:hover:bg-zinc-200 rounded-lg text-xs font-bold cursor-pointer transition-all active:scale-95 shadow-xs"
                >
                  Lưu thay đổi
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </>
  );
}
