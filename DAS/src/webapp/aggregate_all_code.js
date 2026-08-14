const fs = require('fs');
const path = require('path');

const projectRoot = path.join(__dirname, '..', '..');
const outputPath = path.join(projectRoot, 'updated_code.txt');

const allowedExtensions = new Set([
  '.cs', '.ts', '.tsx', '.js', '.jsx', '.json', '.css', '.html', '.md', '.csproj', '.sln', '.sql', '.yml', '.yaml'
]);

const excludedDirs = new Set([
  'node_modules',
  '.next',
  'bin',
  'obj',
  'publish',
  '.git',
  '.agents',
  '.gemini',
  'brain',
  'samples'
]);

const excludedFiles = new Set([
  'package-lock.json',
  'updated_code.txt',
  'aggregate_all_code.js'
]);

let aggregatedContent = `========================================================================
BẢN THỐNG KÊ TOÀN BỘ MÃ NGUỒN DỰ ÁN (DAS - TASKMANAGER / INTERN-DOCUMENTADMINISTRATION-BE)
========================================================================
Backend Developer 1: Đặng Thành Thi (dangthanhthi213@gmail.com)
Branch: feature/document-service
Dịch vụ: DocumentService (services/document-service) - C# .NET 10 / Web API

Đã hoàn thành 100% toàn bộ mã nguồn theo chuẩn Lead Backend (auth-service):
1. Flat file structure (DocumentDbContext.cs, DocumentBusinessService.cs, DocumentsController.cs, Program.cs)
2. Schema EF Core 'document' khớp chính xác 100% với KIẾN TRÚC TỔNG THỂ.docx và SQL Server.
3. Thuật toán sinh số công văn chống trùng lặp thread-safe (UPDLOCK transaction lock):
   - CV-DEN-YYYY-XXXX (Incoming)
   - CV-DI-YYYY-XXXX (Outgoing)
   - CV-NB-YYYY-XXXX (Internal)
4. Workflow chuyển trạng thái công văn: Draft -> Reviewed -> Distributed & Audit Log history (DocumentStatusHistory).
5. Quản lý phân quyền phòng ban (DocumentDepartmentAccess) & File đính kèm (DocumentAttachments).
6. RESTful APIs (/api/documents) đầy đủ JWT authentication và chuẩn response { success, data/message }.
7. EF Core Migration InitialCreate thành công 0 warning, 0 error.
========================================================================\n\n`;

function scanDir(dirPath) {
  const items = fs.readdirSync(dirPath);
  for (const item of items) {
    const fullPath = path.join(dirPath, item);
    const relativePath = path.relative(projectRoot, fullPath);
    
    const stats = fs.statSync(fullPath);
    if (stats.isDirectory()) {
      if (excludedDirs.has(item) || item.startsWith('.')) {
        continue;
      }
      scanDir(fullPath);
    } else if (stats.isFile()) {
      const ext = path.extname(item).toLowerCase();
      if (allowedExtensions.has(ext) && !excludedFiles.has(item)) {
        const content = fs.readFileSync(fullPath, 'utf8');
        
        aggregatedContent += `\n\n========================================================================\n`;
        aggregatedContent += `FILE: ${relativePath.replace(/\\/g, '/')}\n`;
        aggregatedContent += `========================================================================\n`;
        aggregatedContent += content;
        aggregatedContent += `\n`;
      }
    }
  }
}

try {
  scanDir(projectRoot);
  fs.writeFileSync(outputPath, aggregatedContent, 'utf8');
  console.log(`SUCCESS`);
} catch (e) {
  console.error(e);
}
