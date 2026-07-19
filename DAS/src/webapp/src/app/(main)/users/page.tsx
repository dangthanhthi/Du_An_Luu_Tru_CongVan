'use client';
import Header from '@/components/Header';

export default function Users() {
  const users = [
    { id: '1', name: 'Administrator', email: 'admin@das.com', role: 'Admin', dept: 'Toàn hệ thống', status: 'Hoạt động' },
    { id: '2', name: 'Nguyễn Văn Director', email: 'director@das.com', role: 'Director', dept: 'Ban Giám đốc', status: 'Hoạt động' },
    { id: '3', name: 'Lê Thị Secretary', email: 'secretary@das.com', role: 'DirectorSecretary', dept: 'Ban Giám đốc', status: 'Hoạt động' },
    { id: '4', name: 'Trần Văn Employee', email: 'employee@das.com', role: 'Employee', dept: 'Phòng Hành chính', status: 'Hoạt động' },
  ];

  return (
    <>
      <Header title="Quản trị người dùng" />
      <main className="flex-1 p-8 space-y-6 max-w-7xl mx-auto w-full">
        
        {/* Controls */}
        <div className="flex justify-between items-center bg-card p-4 border border-border rounded-xl transition-all duration-200">
          <input
            type="text"
            placeholder="Tìm người dùng..."
            className="w-64 px-3 py-2 border border-border bg-muted rounded-lg text-sm focus:outline-none"
          />
          <button className="px-4 py-2.5 bg-primary text-primary-foreground hover:opacity-90 rounded-lg text-sm font-semibold transition-colors shadow-sm">
            Thêm tài khoản mới
          </button>
        </div>

        {/* Table */}
        <div className="bg-card border border-border rounded-2xl overflow-hidden shadow-[0_4px_20px_-4px_rgba(0,0,0,0.02)] transition-all duration-200">
          <div className="overflow-x-auto">
            <table className="w-full text-sm text-left">
              <thead>
                <tr className="border-b border-border text-muted-foreground text-[10px] uppercase font-bold tracking-wider bg-muted/50">
                  <th className="py-4 px-6">Tên</th>
                  <th className="py-4 px-6">Email</th>
                  <th className="py-4 px-6">Vai trò</th>
                  <th className="py-4 px-6">Phòng ban</th>
                  <th className="py-4 px-6">Trạng thái</th>
                  <th className="py-4 px-6">Hành động</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {users.map((u) => (
                  <tr key={u.id} className="hover:bg-muted/40">
                    <td className="py-4 px-6 text-zinc-850 dark:text-zinc-150 font-bold">{u.name}</td>
                    <td className="py-4 px-6 text-muted-foreground">{u.email}</td>
                    <td className="py-4 px-6 font-semibold">{u.role}</td>
                    <td className="py-4 px-6 text-muted-foreground font-semibold">{u.dept}</td>
                    <td className="py-4 px-6">
                      <span className="px-2.5 py-1 text-xs rounded-full bg-emerald-100 text-emerald-800 dark:bg-emerald-950/20 dark:text-emerald-450 font-bold">
                        {u.status}
                      </span>
                    </td>
                    <td className="py-4 px-6 flex gap-3 text-xs font-bold">
                      <button className="text-emerald-600 dark:text-emerald-400 hover:underline">Sửa</button>
                      <button className="text-red-500 hover:underline">Vô hiệu hóa</button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>

      </main>
    </>
  );
}
