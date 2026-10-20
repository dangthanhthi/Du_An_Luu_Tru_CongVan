# Phương án ghép backend chung của thành viên 1 và 3

Ngày: 09/10/2026. Nguồn chuẩn: code hiện có trong `DAS-Collaboration`. Đối chiếu backend cũ tại official main `36f8f0f240214f2d50a93cc86bee34d880d3c250`.

**Mục tiêu:** ghép database, DTO, numbering và các luồng backend thành một nguồn compile thống nhất, mỗi class có một nơi định nghĩa và mỗi service có một nơi đăng ký DI.

**Quyết định:** thành viên 1 và 3 làm chung trên nguồn local; giữ cách đặt tên dễ hiểu hiện tại. Không chờ một bản Relations khác: `V2DocumentRelations` đã có trên máy. Bản này là phương án ghép, chưa thực hiện merge/cherry-pick, commit/push, chạy host hoặc thay schema database.

**Công nghệ:** .NET 10, ASP.NET Core, EF Core, SQL Server/SQLite; source workflow được MSBuild link vào service sở hữu. Quy ước kiểu dữ liệu xem [Namespace/DTO](NAMESPACE-DTO-CONVENTIONS.md), dependency xem [Dependency map](MEMBER1-DEPENDENCY-MAP.md).

## 1. Chọn một layout đích

Dùng layout hiện tại trên máy cho phần backend chung:

```text
backend/
  Directory.Build.props
  services/document-service/       # DocumentService.csproj
  services/partner-service/        # PartnerService.csproj
  services/files-service/          # FileService.API.csproj
  services/notification-service/   # NotificationService.csproj
  shared/PdfProtocol/              # PdfProtocol.csproj
workflows/business/
  document-service/               # numbering, registration, editing, relations...
  notification-service/           # durable notification workflow
database/                        # model/package documentation; migration xử lý riêng
```

`backend/services/<service>/...` thay vai trò của `services/<service>/...` trong backend cũ đối với bốn service trên. Khi ghép, sửa solution/project/deployment reference sang đường dẫn đích; không giữ cả hai project cũ/mới cùng chạy hoặc compile. Các service Auth/EAP của thành viên 2 và Email/OCR không được chuyển/thay hàng loạt trong lượt ghép này.

Solution local `backend/DocumentAdministration.slnx` có cả project kiểm thử và Auth/Email. Đây là tài liệu đối chiếu layout; không copy toàn bộ solution đó vào repo chính nếu những project tương ứng chưa được chuyển. Thêm đúng các project runtime ở bảng trên vào solution đích của nhóm, giữ project ngoài phạm vi đang dùng. Docker/CI phải đổi build context, csproj path và mount tương ứng trong lần thực hiện ghép; không lấy cấu hình local để thay cấu hình triển khai của nhóm.

Những file tham chiếu layout trên main cũ phải review khi thực hiện:

| File đích của nhóm | Cách ghép |
|---|---|
| `DocumentAdministration.slnx` | Đổi bốn project Document/Partner/Files/Notification sang `backend/services/...`; thêm `backend/shared/PdfProtocol/PdfProtocol.csproj`; giữ project của TV2 và tests hiện có |
| `docker-compose.yml`, `compose.production.yml` | Đổi đường dẫn Dockerfile/build context của bốn service; context phải chứa cả `backend/shared`, `workflows/business` và tập migrations đã review; bảo toàn ports/env/volumes của nhóm |
| `services/<service>/Dockerfile` và các `Dockerfile.prod` tương ứng | Khi chuyển sang `backend/services/...`, sửa COPY/project restore path để lấy được PdfProtocol và MSBuild-linked sources; không lấy nguyên Dockerfile cũ chỉ copy thư mục service |
| `gateway/ocelot.json`, `deploy/ocelot.Production.json` | Merge route từ `backend/gateway/ocelot.json` local theo host/port của môi trường; giữ route và authentication integration của TV2 |
| `deploy/Dockerfile.migrations`, `deploy/migrate.sh` | Chỉ ghi nhận phụ thuộc đường dẫn; chưa đổi/chạy migration trong lượt này, cập nhật cùng kế hoạch schema được chốt riêng |

Mốc main đối chiếu chưa có thư mục `.github` chứa workflow CI. Nếu nhóm thêm CI sau mốc này, review đường dẫn theo commit đích lúc ghép; không bịa ra một workflow đã tồn tại.

## 2. Danh sách chính xác file bổ sung/thay thế

Đọc [bảng từng file](MEMBER1-MEMBER3-FILE-MAP.md) hoặc [JSON tương ứng](MEMBER1-MEMBER3-FILE-MAP.json). Mỗi dòng có nguồn local, vị trí đích, vị trí trên main cũ, hành động và giai đoạn ghép. Đây là danh sách chuyển source; không phải script tự copy/xóa.

| Hành động | Cách thực hiện |
|---|---|
| `ADD` | Bổ sung file tại đúng đường dẫn đích; chưa có file tương ứng trên main cũ |
| `REPLACE` | Dùng nội dung local thay vai trò file cũ trong module này; loại bản cũ khỏi compilation của service sau khi chuyển |
| `MOVE_ONLY` | Nội dung tương đương sau chuẩn hóa xuống dòng; chuyển một bản tới layout mới, không tạo thêm khai báo |
| `MERGE_PROJECT` | Ghép TargetFramework/package/ProjectReference vào csproj đích, kiểm lại các mục của nhóm; không ghi đè deployment/custom build target |
| `ADD_BUILD_RULES` | Thêm source ownership của layout mới; kiểm props cấp cha để tránh import/link hai lần |

### Những bộ file phải chuyển cùng nhau

1. **Document model:** `Models/Entities/*.cs`, `Data/DocumentDbContext.cs`, `DocumentV2Model.cs`, `BusinessCatalogSeed.cs`, `V2MutationLocks.cs`. `DocumentV2Persistence.cs` chỉ chứa entity; draft/identity/exception nằm ở `Models/DTOs/RegistrationContracts.cs`. Năm entity ledger nằm trong `DeliveryEntities.cs`; không lấy lại bản class còn nằm trong các workflow ở snapshot nhánh cũ.
2. **Document contracts:** toàn bộ `Models/DTOs/*.cs` và `Authorization/DocumentActor.cs`. Có cùng kiểu trên main thì thay định nghĩa đó; không thêm file kiểu `DocumentContractsNew.cs` song song. Giữ `DocumentAccessRules`, client, controller theo bản local tương ứng. `V2ReadContracts.cs` định nghĩa `V2KindDetailsView`; ghép cùng `V2HttpContracts.cs` và `Queries/V2DocumentQueries.cs` để response không trả EF entity. JSON vẫn có đúng các trường cũ.
3. **Numbering:** hai file `Numbering/DocumentNumberFormatter.cs`, `DocumentRegistrationWriter.cs` cùng `Relations/V2DocumentRelations.cs`, registration/editing contracts và context/locks. Writer không phải project độc lập.
4. **Workflow:** cả registration, editing/lifecycle, query, relations, completion, PDF, report, reminder, notification và tasks trong manifest; các controller và authority boundary tương ứng. Không ghép riêng controller nhưng để business service cũ.
5. **Partner:** context/entity, `Application/PartnerBusinessService.cs`, controller, error handler, database options và Program cùng phiên bản. Request/response Partner hiện được khai báo trong file business service; không tạo bản DTO khác trong DocumentService để thay chúng. `PartnerDto` phía Document chỉ là projection HTTP của consumer.
6. **Files + PDF:** FileDbContext/entities/DTOs/services/controllers + shared PdfProtocol + Document PDF workflow/controllers. File metadata, claim, current PDF và quyền tải phải cùng protocol; không dùng client metadata cũ để coi file bất kỳ là đã sẵn sàng.
7. **Notification:** context/entity/controller/hub, `ConfiguredDeliveryEmail`, template helper và `workflows/business/notification-service/DurableNotifications.cs`; transport và ledger phía Document chuyển cùng giao thức này. Chưa bật SMTP thật chỉ vì source đã ghép.

### File cũ phải loại khỏi nguồn compile đích

| File main cũ | Nguồn thay vai trò / quyết định |
|---|---|
| `services/document-service/Services/DocumentBusinessService.cs` | `workflows/business/document-service/DocumentBusinessService.cs`; chỉ compile bản workflow local |
| `services/files-service/Services/DocumentAccessClient.cs` | Boundary PDF hiện tại ở `backend/shared/PdfProtocol/HttpClients.cs` và các service claim/authority; không import client legacy vào module mới |
| `services/notification-service/Services/EmailService.cs` | `Services/ConfiguredDeliveryEmail.cs` và durable workflow; không nối lại đường gửi trực tiếp cũ |
| Các `Program.cs` của bốn service cũ | Một `Program.cs` local cho mỗi host; không nối hai bộ top-level statements hoặc gọi Add... hai lần |
| Entity/DTO/context trùng trong `database/source/` hoặc `shared-contracts/` | Folder gói nguồn không tham gia compile; khai báo chạy chỉ nằm tại đường dẫn canonical trong manifest |

“Loại khỏi compile” không đồng nghĩa xóa ngay thư mục WIP hoặc history. Khi thực hiện, chuyển tham chiếu project trước; mọi thay đổi Git phải nằm trong diff để nhóm xem được. Các file main không có trong manifest ngoài những dòng nêu trên cần review theo đúng feature đang dùng, không xóa bằng thao tác dọn cả thư mục.

## 3. Thứ tự ghép các nhánh và nguồn local

Bốn nhánh đã có là các gói source theo chủ đề, không phải bốn backend chạy độc lập. Chúng cùng dựa trên backend cũ; merge nguyên nhánh không tự đưa file gói vào đúng project. Ghép theo đường dẫn của manifest vào một working tree tích hợp chung. Nhánh workflow mới chỉ tạo nếu nhóm yêu cầu; lượt này không tạo nhánh.

| Bước | Nguồn | Việc làm / điều kiện chuyển bước |
|---|---|---|
| 0 | `architecture` + layout local | Giữ namespace đang dùng; chuẩn bị một project cho mỗi service, source links và PdfProtocol project; chưa chạy host |
| 1 | `database` + entity/context local mới nhất | Chuyển model/context vào service sở hữu; giữ bảng legacy còn được dùng; Auth model chỉ tham khảo, không ghi đè TV2 |
| 2 | `shared-contracts` + DTO local mới nhất | Chuyển DTO/interface vào nơi canonical; lấy RegistrationContracts đã tách; khóa một nguồn định nghĩa cho mỗi kiểu |
| 3 | Workflow chung TV1+TV3 trên máy | Ghép Relations và các workflow hỗ trợ, authority boundary, Partner/Files/Notification implementations/controllers |
| 4 | `numbering` + hai file local mới nhất | Ghép formatter/writer cùng assembly DocumentService; loại đường cấp số cũ còn nằm trong source được chuyển |
| 5 | Program/csproj/client local | Ghép DI, middleware, package/project refs, gateway routes; review cấu hình triển khai của nhóm |
| 6 | Working tree sau ghép | Đối chiếu compilation/duplicate types/contracts trước khi chạy host; schema/runtime integration là gate riêng |

**Các bước 1–5 là một nhóm thay đổi phụ thuộc nhau.** Không yêu cầu backend build ở bước 1/2 khi writer/workflow chưa được đưa vào; chỉ nghiệm thu build sau khi dependency closure đầy đủ. `DocumentBusinessService` dùng writer, còn writer gọi Relations; vì vậy nhánh workflow và numbering không thể nghiệm thu độc lập bằng cách chép thiếu một phía.

Bốn nhánh đã được cập nhật ngày 09/10. DTO đọc V2KindDetailsView và V2HttpContracts tương ứng đã xuất bản trong shared-contracts; hướng dẫn và map 114 file đã xuất bản trong architecture ở lượt trước. Map local hiện có 120 file sau J21–J23; chưa xuất bản bản cập nhật này. Mapper `Queries/V2DocumentQueries.cs` mới vẫn ở workflow local, chưa đưa lên nhánh workflow. Khi ghép lấy cùng phiên bản source, không copy DTO mới rồi để mapper cũ. Chỉ push lần tiếp theo khi người dùng yêu cầu.

## 4. MSBuild: mỗi source chỉ compile một lần

Nguồn model/DTO/controller trong thư mục project được SDK compile bằng default glob. Workflow ở ngoài project được link bởi `backend/Directory.Build.props`:

```xml
<PropertyGroup Condition="'$(MSBuildProjectName)' == 'DocumentService'">
  <DasSourceOwner>document-service</DasSourceOwner>
</PropertyGroup>
<ItemGroup Condition="'$(DasSourceOwner)' != ''">
  <Compile Include="$(MSBuildThisFileDirectory)../workflows/business/$(DasSourceOwner)/**/*.cs"
           Link="Workflows/%(RecursiveDir)%(Filename)%(Extension)" />
</ItemGroup>
```

Giữ mapping `FileService.API` → `files-service`, `PartnerService` → `partner-service`, `NotificationService` → `notification-service`. Không thêm `Compile Include="Models/**/*.cs"` vì default glob đã lấy chúng. Không thêm một link `numbering/*.cs` khác nếu đã có link workflow ở trên. Không dùng wildcard compile toàn bộ `database/source` hoặc `shared-contracts`.

Document và Files đều có:

```xml
<ProjectReference Include="../../shared/PdfProtocol/PdfProtocol.csproj" />
```

Partner/Files/Notification vẫn là HTTP service riêng; không thêm ProjectReference từ Document sang các host đó để đọc EF entity. Relations và writer phải cùng assembly DocumentService để gọi được internal methods.

Props local còn link `database/migrations/<owner>/**/*.cs`. Không compile đồng thời migrations từ project cũ và thư mục này. **Chưa thay/chạy migration hoặc snapshot schema trong phạm vi hiện tại:** lần ghép nguồn phải kiểm compilation list và giữ một tập migration đã được review; chưa xác nhận schema thì chưa nghiệm thu khởi động production. Các design-time factory có connection string mẫu không nằm trong danh sách chuyển. `PartnerDbContext.cs` có factory ở cuối file nên chỉ lấy phần runtime context/entity như bảng file đã ghi.

## 5. Đăng ký service ở đâu

Đăng ký trong `backend/services/<service>/Program.cs`, trước `builder.Build()`. Bảng dưới phản ánh code local đang có; dùng một bản đăng ký, không dán thêm cả bảng lên Program đã đủ các mục này.

### DocumentService

| Nhóm | Đăng ký/lifetime hiện tại |
|---|---|
| Persistence/clock | `AddDbContext<DocumentDbContext>` scoped; `AddSingleton(TimeProvider.System)` |
| Legacy orchestration | `AddScoped<IDocumentBusinessService, DocumentBusinessService>` |
| Register/edit/status/query/history | `AddScoped<V2RegistrationService>`, `V2DocumentEditor`, `V2DocumentLifecycle`, `V2DocumentQueries`, `V2LifecycleHistory` |
| Authority | `AddScoped<IDocumentV2Authority, UnavailableDocumentV2Authority>`; boundary chưa có EAP adapter vẫn trả unavailable |
| Catalog | `AddScoped<CatalogService>`; policy `CatalogManage` yêu cầu `das_capability` |
| Partner/Files legacy clients | Typed HTTP clients `IPartnerServiceClient`, `IFilesServiceClient`; `ForwardUserTokenHandler` transient + HttpContextAccessor |
| Notification legacy client | Typed HTTP `INotificationServiceClient → NotificationServiceClient`; cấu hình `Services:NotificationService` riêng, không thay thế durable transport |
| PDF | HTTP `IPdfFilesClient → PdfFilesHttpClient`; scoped `CurrentPdfService`, `IPdfAuthority → DocumentV2PdfAuthority`, `IPdfMaintenance → PdfMaintenance` |
| Reports | Scoped `IReportAuthority → UnavailableReportAuthority`, `CurrentPdfAvailability`, `IncompleteReports` |
| Reminders | Scoped `IReminderDirectory → UnavailableReminderDirectory`, `IReminderTransport → DurableReminderTransport`, `WeeklyReminders`, `IReminderOperatorAuthority → UnavailableReminderOperatorAuthority`; HTTP `IReminderNotificationTransport → ConfiguredReminderNotificationTransport` |
| Staff/tasks | Scoped `IStaffAuthority → UnavailableStaffAuthority`, `ITmsConnector → UnavailableTmsConnector`, `MyStaffService`, `DocumentTasks` |
| Notifications | Scoped `IDocumentNotificationAudience → UnavailableDocumentNotificationAudience`, `DocumentNotifications`; HTTP `IDocumentNotificationTransport → ConfiguredNotificationTransport` |
| Middleware | ExceptionHandler/ProblemDetails, controllers; CORS/authentication/authorization trước MapControllers; giữ no-store và các startup guards của bản local |

**Không thêm DI cho writer/Relations trong phương án hiện tại.** `V2RegistrationService.RegisterAsync` tạo `new DocumentRegistrationWriter(db, clock)`. `DocumentBusinessService` cũng tạo writer với DbContext/clock của request; editor/query/writer tạo `new V2DocumentRelations(db)`. Chúng dùng context scoped của caller, không singleton và không sở hữu một DbContext riêng. Không refactor cách khởi tạo này chỉ để ghép source.

### Files, Partner, Notification

| Host | Nơi đăng ký / các mục phải có |
|---|---|
| Files Program | Scoped FileDbContext, `IFileStorageService → FileStorageService`, `PdfClaims`, `IPdfMaintenance → PdfMaintenance`, `PdfUploadMaintenance`; singleton TimeProvider và `IPdfThreatScanner → UnavailablePdfThreatScanner`; HTTP `IPdfDocumentClient → PdfDocumentHttpClient`; HttpContextAccessor; controllers/auth/CORS |
| Partner Program | Scoped PartnerDbContext, `IPartnerBusinessService → PartnerBusinessService`; GlobalExceptionHandler/ProblemDetails; policy CatalogManage; controllers/auth/CORS |
| Notification Program | Scoped NotificationDbContext, `DurableNotifications`, `DurableDelivery`, `IDeliveryEmail → ConfiguredDeliveryEmail`; singleton TimeProvider; controllers/SignalR; NotificationSend/NotificationAudit policies; MapHub `/hubs/notifications` |

Không đem route mint token `/api/dev/token` của Files main cũ vào host mới. Không thay authority unavailable bằng role Admin hoặc quyền giả để các endpoint trả 200.

### Hosted workers và runtime settings

| Worker/transport | Gate cấu hình hiện có |
|---|---|
| Document weekly reminder | `Reminders:Enabled`; transport còn kiểm `Reminders:TransportEnabled`, `Notifications:TransportEnabled` |
| Document notification worker | `Notifications:WorkerEnabled`; transport `Notifications:TransportEnabled` |
| PDF maintenance ở Document/Files | `PdfProtocolSettings.MaintenanceEnabled(...)` với peer Files/Documents tương ứng |
| Notification delivery worker | `Delivery:WorkerEnabled`; gửi SMTP còn cần `Smtp:DeliveryEnabled` và cấu hình được cấp riêng |

Ghép source không tự bật worker/SMTP/scanner. Nhắc công văn **quá 7 ngày**, chạy lịch **thứ Hai 08:00 Việt Nam**. Giữ clock server và timezone hiện tại; không sửa lại thành 14 ngày từ tài liệu cũ.

## 6. Producer/consumer cần ghép đồng phiên bản

| Boundary | Cặp source cần thống nhất |
|---|---|
| File metadata | Files `Models/DTOs/ManagedFileInfo.cs` + FileStorageService/FilesController ↔ Document `Models/DTOs/ServiceClientContracts.cs` + `Services/Integration/InterServiceClients.cs`; đủ state/hash/canAttach/canDownload, không tự gán quyền khi thiếu field |
| PDF protocol | Shared `Contracts.cs`, HTTP clients/settings/internal key attribute ↔ Document PDF controllers/workflow ↔ Files claim/upload/maintenance/controllers |
| Partner | Partner request/view/filter/service/controller ↔ Document PartnerDto/client; giữ ShortName nullable, active/deleted/version và envelope |
| Document HTTP | `V2HttpContracts.cs`, editing/status/kind/relation contracts ↔ DocumentsV2Controller/workflow ↔ frontend `types/das/document-v2.ts` và service parser hiện có |
| Notifications | Document transports/ledger ↔ Notification durable inbox/controller; giữ dedup/receipt, không đưa đường gửi cũ vào |

Các quy tắc giữ nguyên khi ghép: ba counter theo loại/năm dùng chung company/phòng; D4 mặc định và chỉ thành năm chữ số khi vượt 9999; số thứ tự không đổi khi sửa company/phòng; registration date lấy ngày hiện tại Việt Nam, issued date có thể quá khứ; restore về trạng thái trước khi hủy; ExpectedVersion/idempotency/transaction/authority checks không bỏ đi.

**Điểm cấu hình cụ thể đã thấy:** legacy `Services:NotificationService` trong Document Program mặc định trỏ `localhost:5007`, trong khi Notification local dùng 5005 và Email Worker dùng 5007. Khi chạy ghép phải provision địa chỉ Notification đúng theo host thực tế, không dựa vào fallback đó. PDF và durable transports có settings riêng; không coi chỉnh một legacy URL là đã cấu hình tất cả transport. Task này ghi nhận điểm cần xử lý khi chạy, chưa sửa config hoặc bật host.

Gateway giữ Ocelot routing; ghép các route Document V2/PDF, Partner và Notification từ `backend/gateway/ocelot.json` local vào config đích, bảo toàn route Auth của TV2. Không replace cả config gateway có Auth route tùy biến. Provision JWT/capabilities/internal credentials riêng; không đưa chúng vào manifest/source package.

## 7. Checklist thực hiện khi nhóm bắt đầu ghép

- [ ] Lấy đúng commit đích của nhóm và so sánh lại nếu khác mốc main ở đầu tài liệu; lưu WIP riêng, không reset/clean.
- [ ] Chuyển từng file theo manifest; không compile folder gói hoặc bản legacy trùng. Đối chiếu local mới nhất thay vì snapshot GitHub cũ.
- [ ] Ghép csproj, source links và PdfProtocol refs; sửa solution/deployment references cho bốn service, bảo toàn Auth/Email ngoài phạm vi.
- [ ] Ghép dependency của các bước 1–5 đầy đủ rồi kiểm compilation; không chạy schema/host để thử chữa lỗi compile.
- [ ] Đối chiếu một DI registration cho mỗi boundary, writer/Relations dùng context của caller; workers vẫn tắt khi thiếu cấu hình.
- [ ] Kiểm DTO producer/consumer, version/idempotency/authority/transaction; thiếu quyền vẫn từ chối.
- [ ] Chỉ chạy môi trường khi database/schema đã được review và credentials/settings được cấp ngoài Git; EAP/OCR/TMS/SMTP thật vẫn hoãn.
- [ ] Chỉ commit/push khi người dùng yêu cầu riêng, đúng nhánh/phạm vi; không kèm dữ liệu, migration chưa chốt, file PDF, test output hoặc tài liệu bàn giao ngoài yêu cầu.

Lệnh kiểm compilation list (không khởi động host/database), từ root repo:

```powershell
dotnet msbuild backend/services/document-service/DocumentService.csproj -getItem:Compile -getItem:ProjectReference
dotnet msbuild backend/services/files-service/FileService.API.csproj -getItem:Compile -getItem:ProjectReference
dotnet msbuild backend/services/partner-service/PartnerService.csproj -getItem:Compile
dotnet msbuild backend/services/notification-service/NotificationService.csproj -getItem:Compile
```

Kiểm mỗi đường dẫn source chỉ xuất hiện một lần; Numbering/Relations có trong DocumentService, notification workflow chỉ nằm trong NotificationService, migrations không có hai bản. Sau khi package/lockfiles của project đích đã đầy đủ, restore/build từng project runtime theo thứ tự PdfProtocol → Partner/Files/Notification → Document; build không thay cho kiểm quyền hoặc kiểm schema trên môi trường thật.

## 8. Ranh giới chưa thay đổi

Auth soft-delete/password-reset và schema của thành viên 2 không bị thay bằng bản User/AuthDbContext local khác cấu trúc. EAP/OCR không thuộc lượt ghép. J21–J23 đã triển khai và kiểm local. Positive TMS, authority production và lịch sử legacy chưa xác minh không được ghi thành nghiệm thu môi trường thật. Migration/customer data/UAT thật có gate riêng; chúng không ngăn việc lập bản đồ dependency và source hiện tại.

Tài liệu này chốt cách ghép theo nguồn local chung của TV1+TV3. Nó không khẳng định backend chính đã được merge hoặc production đã sẵn sàng.


## 9. Ghép phần bổ sung J21–J23 từ nguồn local hiện tại

| Module | Producer / nơi compile | Consumer / DI / route |
|---|---|---|
| J21 | `Controllers/MyStaffController.cs`, `workflows/business/document-service/Tasks/MyStaff.cs` và contracts Tasks hiện có trong DocumentService | Gateway thêm GET/OPTIONS tasks riêng; frontend reports-staff/MyStaff dùng staff và task paging riêng. TMS connector thật chưa có; không thay authority bằng fixture. |
| J22 | `Services/CatalogAdminQuery.cs`, `CatalogService.cs`, `Controllers/CatalogsController.cs`; DTO admin hiện cùng Services | GIữ DI CatalogService; routes `/api/v2/admin/catalogs`, `/options`. Lookup active-only và admin paging là hai hợp đồng riêng, chuyển cả controller/query/service. |
| J23 Document | `Models/DTOs/V2HistoryContracts.cs`, `Queries/V2LifecycleHistory.cs`, `V2LifecycleHistoryParser.cs`, `Controllers/DocumentHistoryController.cs` | `AddScoped<V2LifecycleHistory>()`; gateway GET/OPTIONS `/api/v2/documents/{id}/history`; cùng authority và audit writer V2 hiện hành. |
| J23 Partner | `Application/PartnerAuditQuery.cs` (cả DTO metadata/query), `Controllers/PartnerHistoryController.cs` | `AddScoped<PartnerAuditQuery>()`; `/api/partners/{id}/audit` qua gateway route partner hiện có. Không tách DTO rồi compile một bản nữa. |
| No-store | Document/Partner Program middleware `/api` trước authentication challenge | Chuyển cùng controller/query; header không chỉ đặt cho response200. |

`V2HistoryContracts.cs` thuộc shared-contracts. Query/parser/controller và DTO đang khai báo cùng service implementation thuộc workflow chung TV1+TV3, chưa có nhánh workflow được yêu cầu. Không đặt các file này vào numbering/database cho đủ gói. Gateway config là thành phần tích hợp chung, merge các route theo host/port đích; không đưa cấu hình localhost của QA vào architecture package.

UI là consumer cần đồng phiên bản (`services/das/history.ts`, `HistoryPanel`, partner detail route, document detail, catalogs và MyStaff). Bốn nhánh TV1 không chứa frontend; không dùng việc upload bốn nhánh để khẳng định runtime/UI được ghép toàn bộ.

Các query history đọc watermark Int64 string và UTCZ, giữ snapshot bên trong EF execution strategy, fail-closed khi audit sai; Partner chỉ metadata, Document V2LifecycleOnly. Chuyển bản code writer/read contracts cùng nhau để giữ lý do từng chu kỳ hủy/khôi phục và không lẫn phiên bản number mutation với string history.
