# SFARS Backend – Architecture & Design Patterns

## Tổng quan kiến trúc (Architecture Overview)

Dự án được xây dựng theo **Clean Architecture** với 4 tầng tách biệt rõ ràng:

```
SFARS.Domain          ← Lõi nghiệp vụ, không phụ thuộc vào bất kỳ tầng nào
SFARS.Application     ← Use-cases, services, DTOs, validations, events
SFARS.Infrastructure  ← EF Core, repositories, SignalR hubs, external services
SFARS.API             ← Controllers, middlewares, DI composition root
```

Dependency rule: mũi tên phụ thuộc chỉ hướng vào trong (API → Application → Domain; Infrastructure → Domain).

---

## Các Design Pattern được sử dụng

### 1. Repository Pattern
**Vị trí:** `SFARS.Domain/Interfaces/Repositories/Base/IGenericRepository.cs` · `SFARS.Infrastructure/Repositories/GenericRepository.cs`

Tách biệt logic truy cập dữ liệu khỏi business logic. `GenericRepository<TEntity, TKey>` cung cấp đầy đủ CRUD và hỗ trợ Specification.

```csharp
IGenericRepository<TEntity, TKey> repo = _unitOfWork.Repository<Snake, Guid>();
var snakes = await repo.GetAllWithSpecAsync(new SnakeSpecification(specParams));
```

---

### 2. Unit of Work Pattern
**Vị trí:** `SFARS.Domain/Interfaces/IUnitOfWork.cs` · `SFARS.Infrastructure/Data/UnitOfWork.cs`

Quản lý transaction và đảm bảo tính nhất quán dữ liệu khi thao tác trên nhiều bảng cùng lúc.

```csharp
await _unitOfWork.SaveChangesWithTransactionAsync(); // wraps in DB transaction
```

---

### 3. Specification Pattern
**Vị trí:** `SFARS.Domain/Specifications/BaseSpecification.cs` · `SFARS.Domain/Specifications/Interfaces/ISpecification.cs`

Đóng gói logic truy vấn (filter, order, pagination, include) thành các đối tượng riêng biệt, có thể tái sử dụng và kết hợp với nhau.

```csharp
var spec = new SnakeSpecification(specParams);
spec.And(anotherSpec); // composable
```

Concrete specifications: `SnakeSpecification`, `UserSpecification`, `RoleSpecification`, `RefreshTokenSpecification`.

---

### 4. Service Layer Pattern (Generic Service)
**Vị trí:** `SFARS.Application/Services/GenericService.cs` · `SFARS.Application/Services/ReadOnlyService.cs`

`GenericService<TEntity, TDto, TKey>` cung cấp CRUD chuẩn; các service cụ thể kế thừa và override khi cần mở rộng.

```
ReadOnlyService  ←  GenericService  ←  SnakeService
                                    ←  IncidentService
                                    ←  UserService
```

---

### 5. Mediator Pattern (MediatR / Event-Driven)
**Vị trí:** `SFARS.Application/Events/LocationUpdatedEvent.cs` · `SFARS.Application/Events/Handlers/LocationUpdatedEventHandler.cs`

Dùng **MediatR** để publish/subscribe event. Khi vị trí người dùng được cập nhật, event `LocationUpdatedEvent` được publish; handler ghi cache Redis và broadcast qua SignalR mà không cần các service biết nhau.

```csharp
await _mediator.Publish(new LocationUpdatedEvent(userId, lat, lon, updatedAt, accuracy));
```

---

### 6. Dependency Injection / IoC Pattern
**Vị trí:** `SFARS.Application/DependencyInjection.cs` · `SFARS.Infrastructure/DependencyInjection.cs` · `SFARS.API/Program.cs`

Toàn bộ dependency được đăng ký tại composition root (`SFARS.API`). Mỗi layer có extension method `AddApplicationServices()` / `AddInfrastructureServices()` riêng.

---

### 7. Middleware Pipeline Pattern
**Vị trí:** `SFARS.API/Middlewares/ExceptionHandlingMiddleware.cs`

Xử lý exception tập trung trong ASP.NET Core middleware pipeline. Chuyển đổi các custom exception (`NotFoundException`, `UnauthorizedException`, v.v.) thành HTTP response tương ứng.

---

### 8. DTO (Data Transfer Object) Pattern
**Vị trí:** `SFARS.Application/Dtos/`

Tất cả dữ liệu truyền qua API đều sử dụng DTO, không expose trực tiếp entity. Mapping giữa Entity ↔ DTO được thực hiện bởi **Mapster** (`SFARS.Application/Mappings/MappingRegistration.cs`).

---

### 9. Options Pattern (Configuration)
**Vị trí:** `SFARS.Application/Configurations/` · `SFARS.Infrastructure/Configurations/`

Cấu hình ứng dụng được đóng gói trong các POCO class (`AppSettings`, `WebTokenSettings`, `CloudinarySettings`, `YoloModelOptions`, v.v.) và inject qua `IOptions<T>`.

---

### 10. Fluent Validation Pattern
**Vị trí:** `SFARS.Application/Validations/`

Tách logic validation ra khỏi controller/service, sử dụng **FluentValidation** để định nghĩa rule dạng fluent. Validator được đăng ký tự động và gọi qua `ValidatorExtensions.ValidateAsync(dto)`.

---

### 11. Hub Pattern (SignalR)
**Vị trí:** `SFARS.Infrastructure/Hubs/LocationTrackingHub.cs`

Cung cấp giao tiếp real-time hai chiều giữa server và client (mobile/web) cho tính năng theo dõi vị trí cứu hộ theo thời gian thực. Sử dụng **Redis backplane** để scale horizontally.

---

### 12. Result Object Pattern
**Vị trí:** `SFARS.Application/Services/ServiceResult.cs` · `SFARS.Domain/Interfaces/Services/Base/IServiceResult.cs`

Mỗi service method trả về `IServiceResult` bao gồm `ResultCode`, `Message`, và `Data` thay vì throw exception trực tiếp cho các trường hợp lỗi nghiệp vụ.

---

### 13. EF Core Fluent Configuration Pattern
**Vị trí:** `SFARS.Infrastructure/Data/Configurations/`

Cấu hình mapping database cho từng entity thông qua `IEntityTypeConfiguration<T>` riêng biệt, giữ `DbContext` gọn gàng.

---

### 14. Cache-Aside Pattern
**Vị trí:** `SFARS.Domain/Interfaces/Infrastructure/ILocationCacheService.cs` · `SFARS.Infrastructure/Services/`

Dữ liệu vị trí được ghi/đọc từ **Redis** trước khi truy vấn database, giảm tải cho SQL Server trong các thao tác real-time.

---

## Technology Stack

| Thành phần | Công nghệ |
|---|---|
| Framework | .NET 9 · ASP.NET Core 9 |
| ORM | Entity Framework Core 9 |
| Database | SQL Server (NetTopologySuite cho spatial data) |
| Cache / Message Broker | Redis (StackExchange.Redis 2.x) |
| Real-time | SignalR + Redis backplane |
| Event Aggregator | MediatR 12 |
| Object Mapping | Mapster 7 |
| Validation | FluentValidation 12 |
| Logging | Serilog 8 |
| Auth | JWT Bearer · Google OAuth 2.0 |
| File Storage | Cloudinary |
| AI / ML | YOLO v8 (ONNX Runtime) · Gemini AI |
| API Docs | Swagger / OpenAPI (Swashbuckle 9) |
| Testing | xUnit · Moq · FluentAssertions |
