# CẨM NANG XÂY DỰNG BACKEND SOURCE BASE CHUẨN

Tài liệu này là bộ khung kiến trúc, quy chuẩn và best practices được đúc kết từ quá trình phân tích một hệ thống backend phức tạp (Clean Architecture). Mục tiêu là tạo ra một "bản thiết kế" chuẩn mực để tái sử dụng, giúp khởi tạo và maintain các dự án backend mới một cách đồng bộ, an toàn và dễ mở rộng.

---

## 1. Mục tiêu của source base
- **Tái sử dụng cao**: Có thể dùng làm template gốc cho bất kỳ dự án RESTful API nào trong tương lai.
- **Tính bảo trì (Maintainability)**: Mã nguồn dễ đọc, tách biệt trách nhiệm rõ ràng, dễ dàng sửa đổi lỗi mà không ảnh hưởng toàn hệ thống.
- **Khả năng mở rộng (Scalability)**: Dễ dàng thêm tính năng mới, đổi database, tích hợp third-party hoặc scale ngang (horizontal scaling).
- **Làm việc nhóm hiệu quả (Collaboration)**: Cấu trúc rõ ràng tạo ra ranh giới công việc, giảm thời gian onboarding cho người mới và hạn chế code conflict.

## 2. Nguyên tắc thiết kế kiến trúc
- Sử dụng **Clean Architecture** kết hợp với các nguyên lý SOLID.
- **Quy tắc phụ thuộc (Dependency Rule)**: Mũi tên phụ thuộc chỉ đi từ ngoài vào trong. Layer bên trong (Domain) không bao giờ được biết đến hoặc gọi trực tiếp layer bên ngoài.
  *(API → Application → Domain ← Infrastructure)*
- Áp dụng các Design Pattern cốt lõi: **Repository, Unit of Work, Specification, DTO, Result Object, Fluent Validation**.
- Tách biệt hoàn toàn giữa Data Model (Entity trong DB) và Communication Model (Request/Response API).

## 3. Cấu trúc thư mục chuẩn
Cấu trúc chuẩn dựa trên 4 lớp (có thể map sang Java, Node.js, Go,...):

```text
/src
  /Domain            (Lõi: Entities, Interfaces, Enums, Constants, Specifications)
  /Application       (Nghiệp vụ: Services, DTOs, Validations, Mappings, Events)
  /Infrastructure    (Triển khai: DbContext, Repositories, External APIs, Cache)
  /API               (Giao tiếp: Controllers, Middlewares, Extensions, Payloads)
/tests
  /Application.Tests (Unit tests cho logic nghiệp vụ)
  /API.Tests         (Integration tests)
```
- *Dự án nhỏ*: Có thể gộp Domain và Application thành một layer `Core`.
- *Quy tắc thép*: Luôn phải tách biệt API (Endpoints/Controllers) và Logic (Services).

## 4. Các layer / module chính
- **Domain Layer**: Chứa Entities (kế thừa `BaseEntity` gồm `Id`, `CreatedAt`, `UpdatedAt`), các Interface (`IRepository`, `IService`). Tầng này độc lập hoàn toàn, không chứa framework logic như EF Core hay HTTP.
- **Application Layer**: Nơi chứa logic nghiệp vụ cốt lõi. Chứa các Service, thao tác qua DTO. Nơi định nghĩa các Validator (FluentValidation), cấu hình AutoMapper/Mapster.
- **Infrastructure Layer**: Nơi giao tiếp với thế giới bên ngoài. Triển khai DbContext, Repository thao tác với database thực tế, call các API bên thứ 3 (Cloudinary, Gemini, Redis).
- **API Layer**: Điểm vào của ứng dụng. Nhận request, map payload, gọi Service, trả về HTTP status. Nơi đăng ký cấu hình Dependency Injection, Middlewares.

## 5. Quy ước đặt tên
- **Class / Interface**: PascalCase. Interface luôn bắt đầu bằng chữ `I` (vd: `IUserService`).
- **Service & Repository**: Hậu tố tương ứng `UserService`, `UserRepository`.
- **DTO**: Hậu tố `Dto` (vd: `UserDto`).
- **API Request Payload**: Hậu tố `Request` (vd: `CreateUserRequest`).
- **Biến private/readonly**: Có tiền tố `_` (vd: `_userRepository`).
- **Hàm bất đồng bộ**: Có hậu tố `Async` (vd: `CreateAsync()`).
- **Routing**: Định nghĩa dưới dạng hằng số tập trung (vd: `APIRoute.User.Create`), không hardcode string trong Controller.

## 6. Quy ước xử lý Request / Response / Error
- **Request**: Client gửi data qua Payload (Request Object). Controller sẽ dùng extension method map Request này sang DTO trước khi đẩy vào Service. KHÔNG BAO GIỜ truyền thẳng Request Object vào hàm của DB.
- **Response Wrapper (Result Object Pattern)**: Mọi service trả về object `ServiceResult` (hoặc `ServiceResult<T>`) gồm `ResultCode`, `Message`, và `Data` để đảm bảo API format nhất quán.
- **Error Handling**: 
  - Trả về mã lỗi HTTP chuẩn (200, 400, 401, 403, 404, 500) tự động thông qua extension của Controller (`this.ToIActionResult(result)`).
  - Lỗi nghiệp vụ (Business error) xử lý bằng cách trả về `ServiceResult(Warning)` thay vì dùng `throw Exception` để kiểm soát luồng điều khiển tốt hơn.

## 7. Cách tổ chức Config / Env / Constants
- **Config (Options Pattern)**: Biến toàn bộ setting trong `appsettings.json` thành các POCO class (vd: `JwtSettings`, `DbSettings`) và inject qua cấu hình của framework.
- **Env Variables**: Thông tin nhạy cảm (API Keys, Connection Strings, JWT Secrets) BẮT BUỘC lưu trong biến môi trường hoặc Secret Manager. Không commit lên Git.
- **Constants**: Nhóm các hằng số logic theo file (vd: `ResultCodeConst`, `RoleConstants`) và đặt ở tầng Domain. Cấm sử dụng "Magic Strings" rải rác trong code.

## 8. Cách tổ chức Middleware / Auth / Permission
- **Middleware**: Sử dụng Global Exception Handler Middleware bắt các Exception không lường trước (Unhandled) để chặn sập app, log lỗi, và trả về một response 500 an toàn, đồng nhất cho client.
- **Auth**: Dùng JWT Bearer Token. Triển khai phân quyền qua thuộc tính của framework trên các controller (vd: `[Authorize(Roles="Admin")]`).
- **Permission & Security**: Nếu hệ thống cần tracking session/blacklist token, đẩy token đó vào Redis Cache qua Middleware hoặc filter để kiểm tra sớm.

## 9. Cách tổ chức Validation / DTO / Mapper
- **Validation**: Đặt ở Application Layer. Tách rời hoàn toàn khỏi model. Dùng thư viện (như FluentValidation) để code clean hơn.
- **DTO**: Bức tường lửa bảo vệ Entity. API chỉ được xuất / nhập DTO.
- **Mapper**: Gom cấu hình mapping vào một file đăng ký duy nhất (ví dụ Mapster Register). Chuyển đổi qua lại giữa DTO và Entity tự động để tránh boilerplate code `a.Name = b.Name`.

## 10. Cách tổ chức Repository / Service / Controller
- **Repository**: Triển khai mẫu `GenericRepository<T>` bao trọn các hàm CRUD cơ bản. Kết hợp **Specification Pattern** để bóc tách logic query (lọc, phân trang, sort) thành các class độc lập thay vì nhồi nhét LINQ vào Service.
- **Service**: Tiêm (Inject) `IUnitOfWork` (quản lý transaction). Service gọi logic nghiệp vụ -> thao tác DB qua UnitOfWork -> Mapping trả về DTO.
- **Controller (Thin Controller)**: Càng mỏng càng tốt. Chỉ nhận Request -> Validate format -> Map ra DTO -> Gọi Service -> Đóng gói HTTP Response. Cấm tuyệt đối chèn DbContext hay logic tính toán phức tạp vào Controller.

## 11. Cách Logging / Exception Handling / Monitoring
- **Logging**: Dùng Serilog ghi log có cấu trúc (Structured Logging). Chỉ log những sự kiện thực sự cần (Error, Warning, Audit trail). Inject `ILogger<T>` vào nơi cần log.
- **Monitoring (Health Checks)**: Mở endpoint `/health` trả về trạng thái của Database, Redis, Third-party APIs. Giúp DevOps dễ cấu hình auto-scaling và load balancer.

## 12. Cách viết test
- **Mức ưu tiên**: Ưu tiên viết Unit Test cho Application Layer (nơi chứa business logic thực sự).
- **Phương pháp**: Mock toàn bộ Repository, IUnitOfWork và các External Services (như `Moq`). Không gọi trực tiếp DB thật khi chạy Unit test.
- **Naming Convention**: Đặt tên test method mô tả kịch bản rõ ràng: `MethodName_TrạngThái_KếtQuảKỳVọng` (vd: `CreateSnakeAsync_ValidData_ReturnsSuccess`).

## 13. Các rule bắt buộc khi phát triển feature mới
Tuân thủ luồng làm việc sau, từ trong ra ngoài:
1. **Domain Layer**: Bắt đầu bằng việc định nghĩa Entity và các Specification (logic query).
2. **Infrastructure Layer**: Cấu hình Entity vào database, chạy Migration.
3. **Application Layer**: Viết DTOs, cấu hình AutoMapper, viết Validator, viết Business Service trả về `ServiceResult`.
4. **API Layer**: Tạo API Request Payload, đăng ký Route constant, gọi Controller, trả về API kết quả.

## 14. Các anti-pattern cần tránh
- **Fat Controller**: Tội ác lớn nhất. Không bao giờ viết logic IF/ELSE kiểm tra nghiệp vụ hay truy vấn DB trực tiếp trong Controller.
- **Bypass DTO**: Dùng chung một class Entity cho cả DB, DTO và API Payload. Hành động này sẽ gây rò rỉ dữ liệu nhạy cảm (như Password Hash) ra ngoài client.
- **Scattered Route / Magic string**: Hardcode url string `"api/users/{id}"` trong Controller thay vì khai báo hằng số tập trung.
- **Logic rò rỉ (Infrastructure Leakage)**: Thả `IQueryable` cho phép Controller thao tác LINQ chọc thẳng xuống DB.
- **Chặn async (Blocking Async)**: Dùng `.Result` hoặc `.Wait()` trong flow async. Điều này gây deadlocks và giảm performance triệt để.

## 15. Checklist khi khởi tạo project backend mới
- [ ] Phân rã thư mục rõ ràng theo 4 layer (Domain, App, Infra, API).
- [ ] Thiết lập `BaseEntity` chuẩn (Guid Id, CreatedAt, UpdatedAt, By).
- [ ] Cài đặt Global Exception Middleware để bắt mọi lỗi 500.
- [ ] Triển khai format API Response chuẩn (`ServiceResult`).
- [ ] Khởi tạo `GenericRepository` & `UnitOfWork`.
- [ ] Tích hợp tool AutoMapping (Mapster/AutoMapper).
- [ ] Cấu hình FluentValidation.
- [ ] Tích hợp Serilog và viết HealthCheck.
- [ ] Thiết lập các hằng số Route, Error Code, Roles.

---

# 🤖 PROMPT TEMPLATE DÀNH CHO AI (DÙNG KHI START PROJECT MỚI)

Bạn có thể copy đoạn dưới đây làm prompt mỗi khi cần AI tạo một tính năng / dự án backend mới theo đúng chuẩn đã định ra:

> **Role & Context**: Bạn là một Senior Backend Architect. Hãy tạo cho tôi code cho tính năng `[TÊN TÍNH NĂNG - vd: Quản lý Sản phẩm]` dựa theo tiêu chuẩn "Clean Architecture" như sau:
> 
> **Kiến trúc & Cấu trúc**:
> 1. Gồm 4 layer: Domain, Application, Infrastructure, API.
> 2. Controller chỉ nhận request, map payload sang DTO, gọi Service và trả ra HTTP status qua wrapper `ServiceResult`. Không chứa business logic.
> 3. Tầng Application xử lý nghiệp vụ, giao tiếp qua DTO, sử dụng FluentValidation và Mapster.
> 4. Tầng Infrastructure dùng EF Core, Repository Pattern và UnitOfWork. Sử dụng cấu hình Entity bằng Fluent API (IEntityTypeConfiguration).
> 5. Tầng Domain chứa Entity kế thừa từ `BaseEntity` (Guid Id, CreatedAt, UpdatedAt) và các Interface, Specification.
> 
> **Yêu cầu đầu ra (Cho từng class)**:
> Bắt đầu từ trong ra ngoài theo thứ tự:
> - **Domain**: Entity, IRepository interface, Specification (nếu cần filter/sort).
> - **Infrastructure**: Configuration EF Core.
> - **Application**: DTOs, Request/Response map, FluentValidator, Service interface, Implementation trả về `ServiceResult`.
> - **API**: Route constants, Request payload model, Controller.
> 
> Hãy sinh code tuân thủ nghiêm ngặt Dependency Rule, không dùng "magic string", dùng async/await đúng chuẩn, và đảm bảo handle lỗi qua các Result Code thay vì throw exception. Bắt đầu sinh code cho tính năng `[TÊN TÍNH NĂNG]` ngay.
