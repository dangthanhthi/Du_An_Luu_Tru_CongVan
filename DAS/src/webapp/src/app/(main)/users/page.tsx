'use client';
import { useState, useEffect } from 'react';
import Header from '@/components/Header';

interface UserItem {
  id: string;
  name: string;
  email: string;
  role: string;
  dept: string;
  status: 'Hoạt động' | 'Vô hiệu';
}

interface DepartmentItem {
  id: string;
  name: string;
}

export default function Users() {
  const [users, setUsers] = useState<UserItem[]>([]);
  const [departments, setDepartments] = useState<DepartmentItem[]>([]);
  const [searchTerm, setSearchTerm] = useState('');
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingUserId, setEditingUserId] = useState<string | null>(null);

  // Form fields
  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [role, setRole] = useState('Employee');
  const [dept, setDept] = useState('');

  // Load state from localStorage
  useEffect(() => {
    // Load Users
    const storedUsers = localStorage.getItem('custom_users');
    if (storedUsers) {
      setUsers(JSON.parse(storedUsers));
    } else {
      const defaultUsers: UserItem[] = [
        { id: '1', name: 'Administrator', email: 'admin@das.com', role: 'Admin', dept: 'Toàn hệ thống', status: 'Hoạt động' },
        { id: '2', name: 'Nguyễn Văn Director', email: 'director@das.com', role: 'Director', dept: 'Ban Giám đốc', status: 'Hoạt động' },
        { id: '3', name: 'Lê Thị Secretary', email: 'secretary@das.com', role: 'DirectorSecretary', dept: 'Ban Giám đốc', status: 'Hoạt động' },
        { id: '4', name: 'Trần Văn Employee', email: 'employee@das.com', role: 'Employee', dept: 'Phòng Hành chính', status: 'Hoạt động' },
      ];
      localStorage.setItem('custom_users', JSON.stringify(defaultUsers));
      setUsers(defaultUsers);
    }

    // Load Departments to populate dropdown
    const storedDepts = localStorage.getItem('custom_departments');
    if (storedDepts) {
      const parsedDepts = JSON.parse(storedDepts);
      setDepartments(parsedDepts);
      if (parsedDepts.length > 0) {
        setDept(parsedDepts[0].name);
      }
    } else {
      const defaultDepts = [
        { id: '1', name: 'Ban Giám đốc' },
        { id: '2', name: 'Phòng Hành chính' },
        { id: '3', name: 'Phòng Kế toán' },
        { id: '4', name: 'Phòng Kỹ thuật' },
        { id: '5', name: 'Phòng Kinh doanh' }
      ];
      setDepartments(defaultDepts);
      setDept(defaultDepts[0].name);
    }
  }, []);

  const handleOpenCreate = () => {
    setEditingUserId(null);
    setName('');
    setEmail('');
    setRole('Employee');
    if (departments.length > 0) {
      setDept(departments[0].name);
    }
    setIsModalOpen(true);
  };

  const handleOpenEdit = (u: UserItem) => {
    setEditingUserId(u.id);
    setName(u.name);
    setEmail(u.email);
    setRole(u.role);
    setDept(u.dept);
    setIsModalOpen(true);
  };

  const handleSave = (e: React.FormEvent) => {
    e.preventDefault();
    if (!name || !email) {
      alert('Vui lòng điền tên và email!');
      return;
    }

    if (editingUserId) {
      // Edit mode
      const updated = users.map(u => {
        if (u.id === editingUserId) {
          return {
            ...u,
            name: name.trim(),
            email: email.trim().toLowerCase(),
            role: role,
            dept: dept
          };
        }
        return u;
      });
      localStorage.setItem('custom_users', JSON.stringify(updated));
      setUsers(updated);
      setEditingUserId(null);
    } else {
      // Create mode
      const newUser: UserItem = {
        id: `user-${Date.now()}`,
        name: name.trim(),
        email: email.trim().toLowerCase(),
        role: role,
        dept: dept || 'Chưa phân phòng',
        status: 'Hoạt động'
      };

      const updated = [newUser, ...users];
      localStorage.setItem('custom_users', JSON.stringify(updated));
      setUsers(updated);
    }

    // Reset Form & Close Modal
    setName('');
    setEmail('');
    setRole('Employee');
    if (departments.length > 0) {
      setDept(departments[0].name);
    }
    setIsModalOpen(false);
  };

  const toggleStatus = (id: string) => {
    const updated: UserItem[] = users.map(u => {
      if (u.id === id) {
        return {
          ...u,
          status: u.status === 'Hoạt động' ? 'Vô hiệu' : 'Hoạt động'
        };
      }
      return u;
    });
    localStorage.setItem('custom_users', JSON.stringify(updated));
    setUsers(updated);
  };

  const deleteUser = (id: string) => {
    if (id === '1') {
      alert('Không thể xóa tài khoản Administrator mặc định của hệ thống!');
      return;
    }
    if (confirm('Bạn có chắc chắn muốn xóa tài khoản này không?')) {
      const updated = users.filter(u => u.id !== id);
      localStorage.setItem('custom_users', JSON.stringify(updated));
      setUsers(updated);
    }
  };

  const filteredUsers = users.filter(u => 
    u.name.toLowerCase().includes(searchTerm.toLowerCase()) || 
    u.email.toLowerCase().includes(searchTerm.toLowerCase()) || 
    u.role.toLowerCase().includes(searchTerm.toLowerCase()) ||
    u.dept.toLowerCase().includes(searchTerm.toLowerCase())
  );

  return (
    <>
      <Header title="Quản trị người dùng" />
      <main className="flex-1 p-8 space-y-6 max-w-7xl mx-auto w-full">
        
        {/* Controls */}
        <div className="flex justify-between items-center bg-zinc-950 p-4 border border-zinc-800 rounded-xl transition-all duration-200">
          <input
            type="text"
            placeholder="Tìm người dùng (tên, email, vai trò, phòng)..."
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            className="w-80 px-3 py-2 border border-zinc-800 bg-zinc-900 text-white rounded-lg text-sm focus:outline-none focus:border-emerald-600 font-medium"
          />
          <button 
            onClick={handleOpenCreate}
            className="px-4 py-2 bg-zinc-900 dark:bg-white text-white dark:text-black hover:bg-zinc-800 dark:hover:bg-zinc-200 rounded-lg text-xs font-bold transition-all active:scale-95 duration-150 flex items-center gap-2 shadow-xs cursor-pointer"
          >
            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M18 9v3m0 0v3m0-3h3m-3 0h-3m-2-5a4 4 0 11-8 0 4 4 0 018 0zM3 20a6 6 0 0112 0v1H3v-1z" />
            </svg>
            Thêm tài khoản mới
          </button>
        </div>

        {/* Table */}
        <div className="bg-zinc-950 border border-zinc-800 rounded-2xl overflow-hidden shadow-[0_4px_20px_-4px_rgba(0,0,0,0.02)] transition-all duration-200">
          <div className="overflow-x-auto">
            <table className="w-full text-sm text-left">
              <thead>
                <tr className="border-b border-zinc-800 text-zinc-400 text-[10px] uppercase font-bold tracking-wider bg-zinc-900/50">
                  <th className="py-4 px-6">Tên</th>
                  <th className="py-4 px-6">Email</th>
                  <th className="py-4 px-6">Vai trò</th>
                  <th className="py-4 px-6">Phòng ban</th>
                  <th className="py-4 px-6">Trạng thái</th>
                  <th className="py-4 px-6 text-right">Hành động</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-zinc-900 text-white">
                {filteredUsers.length > 0 ? (
                  filteredUsers.map((u) => (
                    <tr key={u.id} className="hover:bg-zinc-900/40 font-medium text-xs">
                      <td className="py-4 px-6 text-zinc-150 font-bold">{u.name}</td>
                      <td className="py-4 px-6 text-zinc-400">{u.email}</td>
                      <td className="py-4 px-6">
                        <span className="px-2 py-0.5 bg-zinc-900 border border-zinc-850 rounded text-zinc-300 font-bold text-[10px]">
                          {u.role}
                        </span>
                      </td>
                      <td className="py-4 px-6 text-zinc-300 font-semibold">{u.dept}</td>
                      <td className="py-4 px-6">
                        <span className={`px-2.5 py-0.5 text-[10px] rounded-full font-bold border ${
                          u.status === 'Hoạt động'
                            ? 'bg-emerald-950/40 border-emerald-800/50 text-emerald-400'
                            : 'bg-zinc-900 border-zinc-800 text-zinc-500'
                        }`}>
                          {u.status}
                        </span>
                      </td>
                      <td className="py-4 px-6 text-right flex justify-end gap-3 font-bold text-xs">
                        <button 
                          onClick={() => handleOpenEdit(u)}
                          className="text-emerald-500 hover:underline cursor-pointer"
                        >
                          Sửa
                        </button>
                        <button 
                          onClick={() => toggleStatus(u.id)}
                          className={`hover:underline cursor-pointer ${
                            u.status === 'Hoạt động' ? 'text-amber-500' : 'text-emerald-500'
                          }`}
                        >
                          {u.status === 'Hoạt động' ? 'Vô hiệu hóa' : 'Kích hoạt'}
                        </button>
                        <button 
                          onClick={() => deleteUser(u.id)}
                          className="text-red-500 hover:underline cursor-pointer"
                        >
                          Xóa
                        </button>
                      </td>
                    </tr>
                  ))
                ) : (
                  <tr>
                    <td colSpan={6} className="py-8 text-center text-zinc-500">
                      Không tìm thấy người dùng nào phù hợp.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </div>

      </main>

      {/* Creation/Edit Modal */}
      {isModalOpen && (
        <div className="fixed inset-0 bg-black/60 backdrop-blur-xs flex items-center justify-center z-50 p-4">
          <div className="bg-zinc-950 border border-zinc-800 w-full max-w-md rounded-2xl shadow-2xl p-6 space-y-4 animate-scale-in text-white">
            <div className="flex justify-between items-center pb-3 border-b border-zinc-900">
              <h3 className="text-sm font-extrabold uppercase tracking-wider text-emerald-400">
                {editingUserId ? 'Chỉnh sửa thông tin tài khoản' : 'Thêm tài khoản mới'}
              </h3>
              <button 
                onClick={() => setIsModalOpen(false)}
                className="text-zinc-400 hover:text-white cursor-pointer"
              >
                ✕
              </button>
            </div>

            <form onSubmit={handleSave} className="space-y-4 text-xs font-semibold">
              <div className="space-y-1.5">
                <label className="text-zinc-400">Họ và tên <span className="text-red-500">*</span></label>
                <input 
                  type="text" 
                  required
                  placeholder="Ví dụ: Nguyễn Văn A" 
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                  className="w-full px-3 py-2 border border-zinc-850 bg-zinc-900 rounded-lg text-white font-medium focus:outline-none focus:border-emerald-600"
                />
              </div>

              <div className="space-y-1.5">
                <label className="text-zinc-400">Địa chỉ Email <span className="text-red-500">*</span></label>
                <input 
                  type="email" 
                  required
                  placeholder="Ví dụ: name@das.com" 
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  className="w-full px-3 py-2 border border-zinc-850 bg-zinc-900 rounded-lg text-white font-medium focus:outline-none focus:border-emerald-600"
                />
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-1.5">
                  <label className="text-zinc-400">Vai trò</label>
                  <select 
                    value={role}
                    onChange={(e) => setRole(e.target.value)}
                    className="w-full px-3 py-2 border border-zinc-850 bg-zinc-900 rounded-lg text-white font-medium focus:outline-none focus:border-emerald-600 cursor-pointer"
                  >
                    <option value="Employee">Nhân viên (Employee)</option>
                    <option value="DirectorSecretary">Thư ký (Secretary)</option>
                    <option value="Director">Giám đốc/Trưởng phòng</option>
                    <option value="Admin">Quản trị viên (Admin)</option>
                  </select>
                </div>

                <div className="space-y-1.5">
                  <label className="text-zinc-400">Phòng ban</label>
                  <select 
                    value={dept}
                    onChange={(e) => setDept(e.target.value)}
                    className="w-full px-3 py-2 border border-zinc-850 bg-zinc-900 rounded-lg text-white font-medium focus:outline-none focus:border-emerald-600 cursor-pointer"
                  >
                    {departments.map((d) => (
                      <option key={d.id} value={d.name}>{d.name}</option>
                    ))}
                    <option value="Toàn hệ thống">Toàn hệ thống</option>
                  </select>
                </div>
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
                  {editingUserId ? 'Cập nhật' : 'Thêm tài khoản'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </>
  );
}
