'use client';
import Header from '@/components/Header';

export default function Departments() {
  const depts = [
    { id: '1', name: 'Ban Giám đốc', code: 'BGD', manager: 'Nguyễn Văn Director', secretary: 'Lê Thị Secretary', count: 3 },
    { id: '2', name: 'Phòng Hành chính', code: 'PHC', manager: 'Trần Văn Trưởng Phòng', secretary: 'Phạm Thị Thư Ký', count: 12 },
    { id: '3', name: 'Phòng Kế toán', code: 'PKT', manager: 'Nguyễn Kế Toán Trưởng', secretary: 'Lê Kế Toán Viên', count: 6 },
    { id: '4', name: 'Phòng Kỹ thuật', code: 'PKH', manager: 'Trần Văn Tech Lead', secretary: 'Lê Văn Dev', count: 25 },
    { id: '5', name: 'Phòng Kinh doanh', code: 'PKD', manager: 'Lê Thị Sales Manager', secretary: 'Phạm Văn Sales Staff', count: 15 }
  ];

  return (
    <>
      <Header title="Danh sách phòng ban" />
      <main className="flex-1 p-8 space-y-6 max-w-7xl mx-auto w-full">
        
        {/* Controls */}
        <div className="flex justify-between items-center bg-card p-4 border border-border rounded-xl transition-all duration-200">
          <input
            type="text"
            placeholder="Tìm phòng ban..."
            className="w-64 px-3 py-2 border border-border bg-muted rounded-lg text-sm focus:outline-none"
          />
          <button className="px-4 py-2.5 bg-primary text-primary-foreground hover:opacity-90 rounded-lg text-sm font-semibold transition-colors shadow-sm">
            Tạo phòng ban mới
          </button>
        </div>

        {/* Grid of Departments */}
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          {depts.map((d) => (
            <div key={d.id} className="p-6 bg-card border border-border rounded-2xl space-y-4 shadow-[0_4px_20px_-4px_rgba(0,0,0,0.02)] transition-all duration-200">
              <div className="flex justify-between items-start">
                <div>
                  <h4 className="font-bold text-foreground text-base">{d.name}</h4>
                  <span className="text-[10px] px-2 py-0.5 bg-muted text-muted-foreground rounded font-bold uppercase tracking-wider">{d.code}</span>
                </div>
                <span className="text-[10px] text-muted-foreground font-bold uppercase tracking-wider">{d.count} thành viên</span>
              </div>
              <div className="space-y-2.5 text-xs border-t border-border pt-4 font-medium">
                <div className="flex justify-between">
                  <span className="text-muted-foreground">Trưởng phòng:</span>
                  <span className="font-bold text-foreground">{d.manager}</span>
                </div>
                <div className="flex justify-between">
                  <span className="text-muted-foreground">Thư ký phòng:</span>
                  <span className="font-semibold text-muted-foreground">{d.secretary}</span>
                </div>
              </div>
            </div>
          ))}
        </div>

      </main>
    </>
  );
}
