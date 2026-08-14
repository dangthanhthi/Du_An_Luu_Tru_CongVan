/* ============================================================
   SQL SCRIPT KHỎI TẠO SCHEMA DỮ LIỆU BẢNG 'document' 
   DÀNH CHO BACKEND DEVELOPER 1 (DAS.DocumentService)
   ============================================================ */

IF DB_ID('DocumentManagementDb') IS NULL
BEGIN
    CREATE DATABASE DocumentManagementDb;
END
GO

USE DocumentManagementDb;
GO

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'document')
BEGIN
    EXEC('CREATE SCHEMA document');
END
GO

-- Bảng đếm số công văn (dùng transaction + UPDLOCK để tránh trùng số)
IF OBJECT_ID('document.document_number_counters', 'U') IS NULL
BEGIN
    CREATE TABLE document.document_number_counters (
        doc_type         NVARCHAR(20)    NOT NULL,   -- INCOMING / OUTGOING / INTERNAL
        year            INT             NOT NULL,
        current_value    INT             NOT NULL DEFAULT 0,
        PRIMARY KEY (doc_type, year)
    );
END
GO

-- Bảng lưu trữ chính thông tin công văn
IF OBJECT_ID('document.documents', 'U') IS NULL
BEGIN
    CREATE TABLE document.documents (
        id                  UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        document_number      NVARCHAR(50)    NOT NULL UNIQUE,   -- VD: CV-DEN-2026-0001
        doc_type             NVARCHAR(20)    NOT NULL,           -- INCOMING / OUTGOING / INTERNAL
        status              NVARCHAR(20)    NOT NULL DEFAULT 'Draft', -- Draft/Reviewed/Distributed
        title               NVARCHAR(500)   NOT NULL,
        summary             NVARCHAR(MAX)   NULL,
        partner_id           UNIQUEIDENTIFIER NULL,     -- tham chiếu logic sang partner.external_entities
        sender_department_id  UNIQUEIDENTIFIER NULL,     -- tham chiếu logic sang auth.departments (công văn đi/nội bộ)
        created_by_user_id     UNIQUEIDENTIFIER NOT NULL, -- tham chiếu logic sang auth.users
        received_at          DATETIME2       NULL,       -- thời điểm nhận (đối với công văn đến)
        distributed_at       DATETIME2       NULL,       -- thời điểm chuyển trạng thái Distributed
        created_at           DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
        updated_at           DATETIME2       NULL
    );

    CREATE INDEX IX_Documents_DocType_Status ON document.documents(doc_type, status);
    CREATE INDEX IX_Documents_PartnerId ON document.documents(partner_id);
END
GO

-- File đính kèm của công văn (tham chiếu logic sang files.files)
IF OBJECT_ID('document.document_attachments', 'U') IS NULL
BEGIN
    CREATE TABLE document.document_attachments (
        id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        document_id      UNIQUEIDENTIFIER NOT NULL REFERENCES document.documents(id) ON DELETE CASCADE,
        file_id          UNIQUEIDENTIFIER NOT NULL,     -- tham chiếu logic sang files.files
        attachment_type  NVARCHAR(50)    NULL,           -- VD: Original, Scan, Reference
        created_at       DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME()
    );

    CREATE INDEX IX_DocumentAttachments_DocumentId ON document.document_attachments(document_id);
END
GO

-- Bảng phân quyền phòng ban truy cập công văn (many-to-many)
IF OBJECT_ID('document.document_department_accesses', 'U') IS NULL
BEGIN
    CREATE TABLE document.document_department_accesses (
        document_id      UNIQUEIDENTIFIER NOT NULL REFERENCES document.documents(id) ON DELETE CASCADE,
        department_id    UNIQUEIDENTIFIER NOT NULL,      -- tham chiếu logic sang auth.departments
        assigned_at      DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
        assigned_by_user_id UNIQUEIDENTIFIER NOT NULL,      -- tham chiếu logic sang auth.users
        PRIMARY KEY (document_id, department_id)
    );

    CREATE INDEX IX_DocumentDepartmentAccess_DepartmentId ON document.document_department_accesses(department_id);
END
GO

-- Bảng nhật ký thay đổi trạng thái công văn (audit trail)
IF OBJECT_ID('document.document_status_histories', 'U') IS NULL
BEGIN
    CREATE TABLE document.document_status_histories (
        id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        document_id      UNIQUEIDENTIFIER NOT NULL REFERENCES document.documents(id) ON DELETE CASCADE,
        old_status       NVARCHAR(20)    NULL,
        new_status       NVARCHAR(20)    NOT NULL,
        changed_by_user_id UNIQUEIDENTIFIER NOT NULL,       -- tham chiếu logic sang auth.users
        changed_at       DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
        note            NVARCHAR(500)   NULL
    );

    CREATE INDEX IX_DocumentStatusHistory_DocumentId ON document.document_status_histories(document_id);
END
GO
