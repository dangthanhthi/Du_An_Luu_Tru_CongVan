# Frontend

Next.js 16/React/MUI, Node 22. `src/app` chứa route; `src/views/apps` chứa màn hình nghiệp vụ; `src/services` chứa API client; `src/types/das` chứa typed contracts. Test ở `tests/frontend`.

Các phần đã có: danh sách/đăng ký/sửa/chi tiết công văn, PDF có kiểm quyền, quan hệ và vòng đời, đối tác/tổ chức, báo cáo/XLSX, task và My Staff, menu/tìm chức năng DAS và login vi/en. Các route/demo template đã bị chặn trong nguồn hiện tại. Menu chỉ phản ánh capability; backend vẫn quyết định quyền.

```sh
npm ci --ignore-scripts --no-audit --no-fund
npm run generate:prisma
npm exec next typegen
npm test
npm run typecheck
npm run lint
npm run build
```

Schema Prisma cũ cho phần template auth ở `../database/prisma/schema.prisma`, client được sinh vào `node_modules/.prisma/client`. Database công văn do các backend service quản lý, không phải schema Prisma này. Không chạy `migrate` hoặc seed lên DB công ty như một bước build.

Tạo `.env.local` từ `.env.example`, đặt secret riêng rồi `npm run dev`. Không đưa token/credential vào URL, source hoặc commit. Login vẫn là adapter credential hiện có; EAP BFF chưa triển khai. Lỗi login trả về sau logout đã được ghi trong backlog, không coi authentication thật đã nghiệm thu.

`generate:prisma` tạo schema tạm dưới `.artifacts/` chỉ để Prisma6 tìm đúng dependency đã khóa của frontend; chỉ sửa đường dẫn output trong bản tạm rồi dọn sau chạy. Schema chính ở `database/` không đổi; client nằm trong `node_modules/.prisma/client`. Tự cài dependency khi generate bị tắt. Không gọi `prisma generate` trực tiếp với schema ngoài frontend hoặc chạy migration như bước build.
