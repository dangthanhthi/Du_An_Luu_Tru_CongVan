# API Contract — Hệ thống Quản lý Công văn

Response format chung cho **mọi** endpoint:
```json
{ "success": true, "data": { }, "message": null, "errors": [] }
```
Lỗi trả về:
```json
{ "success": false, "data": null, "message": "Mô tả lỗi", "errors": ["field: lý do"] }
```
Tất cả endpoint (trừ `/auth/login`) yêu cầu header `Authorization: Bearer <token>`.

---

## 1. AuthService — prefix `/api/auth`

### POST /api/auth/login
Request:
```json
{ "username": "string", "password": "string" }
```
Response:
```json
{
  "success": true,
  "data": {
    "accessToken": "string",
    "refreshToken": "string",
    "expiresIn": 3600,
    "user": { "id": "guid", "fullName": "string", "role": "string", "departmentId": "guid" }
  }
}
```

### POST /api/auth/refresh
Request: `{ "refreshToken": "string" }`
Response: `{ "success": true, "data": { "accessToken": "string", "refreshToken": "string" } }`

### POST /api/auth/logout
Request: `{ "refreshToken": "string" }` → Response: `{ "success": true, "data": null }`

### GET /api/auth/me
Response: `{ "success": true, "data": { "id", "fullName", "email", "role", "departmentId", "departmentName" } }`

### Users (Admin only)
| Method | Endpoint | Ghi chú |
|---|---|---|
| GET | /api/auth/users?page=1&pageSize=20&departmentId= | danh sách, filter theo phòng ban |
| GET | /api/auth/users/{id} | chi tiết |
| POST | /api/auth/users | tạo user: `{ username, password, fullName, email, departmentId, roleIds: [] }` |
| PUT | /api/auth/users/{id} | cập nhật thông tin |
| PUT | /api/auth/users/{id}/status | `{ isActive: bool }` |
| DELETE | /api/auth/users/{id} | vô hiệu hóa (không xóa cứng) |

### Roles / Departments
| Method | Endpoint |
|---|---|
| GET / POST | /api/auth/roles |
| GET / POST | /api/auth/departments |
| PUT / DELETE | /api/auth/departments/{id} |

---

## 2. PartnerService — prefix `/api/partners`

| Method | Endpoint | Request | Ghi chú |
|---|---|---|---|
| GET | /api/partners?keyword=&page=1&pageSize=20 | — | tìm theo tên/tên viết tắt |
| GET | /api/partners/{id} | — | |
| POST | /api/partners | `{ fullName, shortName, contactPerson, phone, email, address }` | |
| PUT | /api/partners/{id} | như trên | |
| DELETE | /api/partners/{id} | — | soft delete (`isDeleted = true`) |

Response item mẫu:
```json
{ "id": "guid", "fullName": "string", "shortName": "string", "contactPerson": "string", "phone": "string", "email": "string", "isDeleted": false }
```

---

## 3. FileService — prefix `/api/files`

### POST /api/files/upload
`multipart/form-data`, field `file`. Response:
```json
{ "success": true, "data": { "id": "guid", "originalName": "string", "sizeBytes": 12345, "contentType": "application/pdf" } }
```

### GET /api/files/{id}
Trả file binary (stream) để xem/tải.

### GET /api/files/{id}/info
Trả metadata (không tải file).

---

## 4. DocumentService — prefix `/api/documents`

### POST /api/documents/incoming
Gọi bởi EmailWorkerService khi có công văn mới từ fax.
```json
{ "title": "string", "partnerId": "guid|null", "fileId": "guid", "receivedAt": "datetime" }
```
Response: `{ "id", "documentNumber", "status": "Draft" }`

### POST /api/documents/outgoing
```json
{ "title": "string", "partnerId": "guid", "summary": "string" }
```

### POST /api/documents/internal
```json
{ "title": "string", "summary": "string" }
```
(Không cần `partnerId` — chỉ gửi nội bộ ban lãnh đạo.)

### GET /api/documents?docType=Incoming&status=&page=1&pageSize=20
Danh sách, lọc theo loại/trạng thái. Chỉ trả công văn mà user hiện tại có quyền xem (theo `DocumentDepartmentAccess`, hoặc do chính user tạo).

### GET /api/documents/{id}
Chi tiết công văn + danh sách file đính kèm + lịch sử trạng thái.

### PUT /api/documents/{id}
Cập nhật thông tin (title, summary, partnerId...) — dùng khi thư ký sửa sai sót.

### PUT /api/documents/{id}/assign-departments
```json
{ "departmentIds": ["guid", "guid"] }
```
Chỉ thư ký GĐ dùng — chọn phòng ban nhận công văn đến.

### PUT /api/documents/{id}/status
```json
{ "status": "Distributed", "note": "string" }
```

### POST /api/documents/{id}/attachments
```json
{ "fileId": "guid", "attachmentType": "Original" }
```

---

## 5. EmailWorkerService
Không có API public — chạy job nền (`BackgroundService`, mỗi 1 giờ), gọi nội bộ sang `FileService.upload` rồi `DocumentService.POST /incoming`. Có thể thêm 1 endpoint nội bộ để trigger thủ công lúc test:

### POST /api/email-worker/trigger (internal/dev only)
Chạy ngay job check email, không cần đợi lịch.

---

## 6. AiOcrService — prefix `/api/ai-ocr`

### POST /api/ai-ocr/analyze
Request: `{ "fileId": "guid" }`
Response:
```json
{ "success": true, "data": { "extractedText": "string", "matchedPartnerId": "guid|null", "confidence": 0.87 } }
```
Được gọi bởi EmailWorkerService hoặc DocumentService ngay sau khi công văn đến được tạo.

---

## 7. NotificationService — prefix `/api/notifications`

### POST /api/notifications/send (internal)
```json
{ "recipientUserId": "guid", "subject": "string", "body": "string", "relatedDocumentId": "guid|null" }
```
Gọi nội bộ từ DocumentService khi có công văn đến mới (báo thư ký GĐ).

### GET /api/notifications/logs?page=1 (Admin, để debug)
Danh sách log gửi email, trạng thái Sent/Failed.

---

## Mã lỗi HTTP dùng chung
| Code | Ý nghĩa |
|---|---|
| 400 | Request sai định dạng / thiếu field |
| 401 | Chưa đăng nhập / token hết hạn |
| 403 | Không có quyền (VD: phòng ban không được assign công văn này) |
| 404 | Không tìm thấy resource |
| 409 | Conflict (VD: trùng số công văn — hiếm khi xảy ra nếu counter đúng) |
| 500 | Lỗi server |
