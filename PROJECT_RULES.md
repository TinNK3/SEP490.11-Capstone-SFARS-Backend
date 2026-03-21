# PROJECT_RULES

## 1. Project Overview

### Purpose of the system
SFARS Backend is the REST API core for an AI-assisted snakebite emergency platform. The backend supports:
- user authentication and profile management,
- SOS incident creation/tracking and rescue dispatch,
- snake knowledge/identification workflows (including AI inference/review),
- community and FAQ features,
- payment/donation flows,
- real-time updates via SignalR.

### High-level architecture
The project follows a **Clean Architecture / layered** style with four primary projects:
- **SFARS.API**: controllers, middleware, app bootstrap, auth wiring.
- **SFARS.Application**: service-level business logic, DTOs, validation, events.
- **SFARS.Domain**: entities, contracts/interfaces, specifications, constants/enums.
- **SFARS.Infrastructure**: EF Core, DbContext, repository + unit of work, external integrations.

Dependency flow is inward from API/Application/Infrastructure toward Domain abstractions.

### Tech stack
- .NET 9 / ASP.NET Core Web API
- EF Core 9 + SQL Server (+ NetTopologySuite for spatial types)
- Redis (cache/backplane), SignalR
- FluentValidation, Mapster, MediatR
- Serilog
- xUnit + Moq + FluentAssertions
- Hangfire for background jobs

---

## 2. Architecture Rules

### Layer responsibilities
- **API layer**
  - Defines HTTP endpoints and route constants (`APIRoute`).
  - Maps API request payloads to DTOs via extension methods (`PayloadExtensions`).
  - Returns results through `ToIActionResult` mapping from `ServiceResult.ResultCode`.
- **Application layer**
  - Orchestrates use-cases in services (`*Service` classes).
  - Uses `IUnitOfWork` + repositories through interfaces (not direct DbContext).
  - Uses DTOs for request/response boundaries.
  - Performs validation and standardized result shaping (`ServiceResult`).
- **Domain layer**
  - Owns entity models, specifications, enums/constants, and contracts.
  - Must stay framework-light and persistence-agnostic.
- **Infrastructure layer**
  - Implements repositories, data context/configurations, and external services.
  - Registers concrete implementations for interfaces in DI.

### Dependency rules
- Controllers should call services, not repositories/DbContext directly.
- Application should depend on Domain interfaces/contracts.
- Infrastructure should implement Domain interfaces and be injected through DI.
- Query construction should prefer Specification objects over ad-hoc query logic in services.

### Design principles used
- Repository + Unit of Work
- Specification pattern for query composition
- DTO pattern for API boundaries
- Extension-method based mapping in API
- Centralized response envelope (`ServiceResult`, `ServiceResult<T>`)

---

## 3. Coding Conventions

### Naming conventions
- Interfaces prefixed with `I` (e.g., `IUserService`, `IGenericRepository<,>`).
- Services suffixed with `Service` (e.g., `UserService`, `AuthService`).
- DTOs suffixed with `Dto` (e.g., `UserDto`, `IncidentDto`).
- Request payload models suffixed with `Request`.
- Async methods end with `Async`.

### File/folder structure conventions
- API request models: `SFARS.API/Payloads/Request/<Feature>/...`
- DTOs: `SFARS.Application/Dtos/<Feature>/...`
- Validators: `SFARS.Application/Validations/...`
- Specs: `SFARS.Domain/Specifications/...`
- EF entity configs: `SFARS.Infrastructure/Data/Configurations/...`
- Tests grouped by application service feature: `SFARS.Tests/Application/Services/<Feature>/...`

### Code style and implementation patterns
- Prefer `async/await` end-to-end for I/O.
- Use `ResultCodeConst` + `SystemMessageService` for service messages.
- Return `ServiceResult` envelopes for business outcomes; avoid raw primitives for controller responses.
- Use `ILogger<T>` injection and structured logging placeholders where context is needed.
- Apply pagination helpers from `BaseSpecParams` (`GetPage/GetTake/GetSkip`) in spec-driven list APIs.

### Noted inconsistencies (documented exceptions)
- Dominant pagination model is 1-based (`BaseSpecParams.Page` default 1), but some endpoints/services still use custom integer page arguments (e.g., incident list action comment shows 0-based).

---

## 4. API Rules

### Request/response format
- Most endpoints return data wrapped in:
  - `resultCode`
  - `message`
  - `data`
- Controllers typically:
  1. receive payload/query,
  2. map request to DTO via extension method (if needed),
  3. call service,
  4. return `this.ToIActionResult(result)`.

### Route conventions
- Routes are centralized in `SFARS.API/Payloads/APIRoute.cs`.
- Dominant pattern is `api/<resource>` with subresource actions (`/admin/...`, `/me/...`, `/incidents/{id}/...`).

### Status code mapping
Mapped in `ControllerExtensions.ToIActionResult`:
- Success codes (`.Success`) → **200 OK**
- Specific auth warning codes → **401 Unauthorized**
- Forbidden warning codes → **403 Forbidden**
- Not-found warning codes → **404 Not Found**
- Other warning codes → **400 Bad Request**
- Fail/default → **500 Internal Server Error**

### Validation patterns
- Validation is primarily **FluentValidation in Application layer** (`ValidatorExtensions.ValidateAsync`).
- Validation is typically executed in service methods before persistence operations.
- Validation failures are often surfaced as warning results or exception flows (depending on service implementation).

### Error response standard
- Unhandled exceptions are returned by middleware as:
  - `resultCode = SYS_Fail0001`
  - message from system message service
  - HTTP 500

---

## 5. Data & Database Rules

### Entity design
- Entities generally inherit `BaseEntity` with audit fields:
  - `Id` (Guid),
  - `CreatedAt/CreatedBy`,
  - `UpdatedAt/UpdatedBy`.
- Spatial/location entities use NetTopologySuite `Point` and SQL `geography` type.

### Relationships
- Relationships are configured via EF fluent configuration classes per entity.
- Indexes and constraints are explicitly configured in `Data/Configurations/*`.
- String enum persistence is used in multiple entities via `.HasConversion<string>()`.

### Query patterns
- Use `GenericRepository` methods:
  - basic reads/writes,
  - spec-based reads (`GetWithSpecAsync`, `GetAllWithSpecAsync`),
  - utility checks (`AnyAsync`, `CountAsync`).
- Use specifications (`BaseSpecification<T>`) for:
  - criteria,
  - includes,
  - sorting,
  - pagination,
  - split query mode.

### Performance practices
- Prefer `AsNoTracking` for read-only scenarios.
- Use `EnableSplitQuery()` in specs with multi-include graphs.
- Use `ExecuteDeleteAsync` paths for efficient delete operations.
- Avoid query transformations that break indexes (e.g., lowercasing/concatenation in predicate paths).

---

## 6. Error Handling & Logging

### Exception strategy
- Business flow commonly returns `ServiceResult` warnings/failures.
- Custom exceptions exist (`UnauthorizedException`, `ForbiddenException`, `NotFoundException`, `UnprocessableEntityException`) and may be thrown in services.
- Middleware currently guarantees standardized handling for unhandled exceptions into 500 responses.

### Logging conventions
- Serilog is configured at startup via `ConfigureSerilog`.
- Middleware logs unhandled exceptions with request path/method.
- Services use `ILogger<T>` and log failures in catch blocks.

---

## 7. Testing Strategy

### Test structure
- Test project: `SFARS.Tests` (xUnit).
- Focus is primarily Application service unit tests.
- Dependencies mocked with Moq; assertions via FluentAssertions.

### Naming conventions
- Common pattern: `MethodName_Scenario_ExpectedBehavior`.
- Tests include Arrange/Act/Assert blocks and scenario comments.

### Coverage expectations (observed)
- Strong coverage on major services (auth, users, snakes, incidents, transactions, etc.).
- Tests validate result codes and service-level behavior (including warnings/errors), not only happy-path data.

---

## 8. Common Patterns Used

- **Repository + Unit of Work**
  - `IUnitOfWork.Repository<TEntity,TKey>()`
  - `SaveChangesAsync()` / transaction helpers.
- **Specification Pattern**
  - Encapsulate filtering/includes/paging/sorting in reusable specs.
- **DTO Mapping**
  - API request → DTO via payload extension methods.
  - Entity ↔ DTO via Mapster in Application mappings.
- **Result Code Driven API**
  - Services return `ServiceResult`; API maps to HTTP status centrally.
- **DI by layer extension**
  - `AddApplication(...)`, `AddInfrastructure(...)`, plus API-specific extension setup.

---

## 9. Do & Don’t

### Do
- **Do** add/modify routes through `APIRoute` constants.
- **Do** return `ServiceResult` from services and use `ToIActionResult` in controllers.
- **Do** place business validation in Application validators/services.
- **Do** use specifications for non-trivial query logic.
- **Do** keep API payload models separate from Application DTOs and map explicitly.
- **Do** use async repository/service methods for database or external I/O.
- **Do** preserve layer boundaries (no infrastructure/data logic in controllers).

### Don’t
- **Don’t** bypass `ServiceResult` conventions with ad-hoc controller response shapes.
- **Don’t** scatter hard-coded route strings in controllers.
- **Don’t** query DbContext directly from API/Application services when repository/spec abstractions already exist for the scenario.
- **Don’t** introduce blocking calls (`.Result`, `.Wait()`) in async flows.
- **Don’t** add query transformations that reduce index usage in hot paths.

### Existing anti-patterns to avoid extending
- Mixed pagination semantics (1-based spec params vs some endpoint-specific 0-based assumptions).
- Mixed error handling strategy (result-code flow + thrown exceptions); prefer consistent service-result flow for expected business errors.

---

## 10. Example Templates

### A. Sample API endpoint template
```csharp
[Authorize]
[HttpPut(APIRoute.User.UpdateMe, Name = nameof(UpdateMeAsync))]
public async Task<IActionResult> UpdateMeAsync([FromBody] UpdateProfileRequest req)
{
    var userId = User.GetUserId();
    var result = await _userService.UpdateMeAsync(userId, req.ToUserForUpdate());
    return this.ToIActionResult(result);
}
```

### B. Sample service template
```csharp
public async Task<IServiceResult> GetMeAsync(Guid userId)
{
    if (userId == Guid.Empty)
    {
        return new ServiceResult(
            ResultCodeConst.Auth_Warning0007,
            await _msgService.GetMessageAsync(ResultCodeConst.Auth_Warning0007));
    }

    var spec = UserSpecification.ById(userId);
    var user = await _unitOfWork.Repository<User, Guid>().GetWithSpecAsync(spec);
    if (user == null)
    {
        return new ServiceResult(
            ResultCodeConst.SYS_Warning0004,
            await _msgService.GetMessageAsync(ResultCodeConst.SYS_Warning0004));
    }

    return new ServiceResult(
        ResultCodeConst.SYS_Success0002,
        await _msgService.GetMessageAsync(ResultCodeConst.SYS_Success0002),
        _mapper.Map<UserDto>(user));
}
```

### C. Sample repository/spec usage template
```csharp
public static UserSpecification List(UserSpecParams p)
{
    var spec = new UserSpecification(BuildSearchCriteria(p.Search));
    spec.EnableSplitQuery();
    spec.ApplyInclude(q => q.Include(u => u.UserRoles).ThenInclude(ur => ur.Role));
    spec.AddOrderByDescending(u => u.CreatedAt);
    spec.ApplyPaging(p.GetTake(), p.GetSkip());
    return spec;
}
```
