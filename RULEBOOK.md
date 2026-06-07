# 📘 SFARS Backend - Developer Onboarding & Contribution Rulebook

Welcome to the SFARS Backend! This rulebook is designed to get you up to speed quickly and ensure our codebase remains clean, maintainable, and highly consistent.

---

## ⚡ Quick Start Guide (Understand the project in 30-60 minutes)

1. **Understand the Architecture (10 mins)**: We strictly follow **Clean Architecture**. Read Section 1 to understand the 4 layers: `API`, `Application`, `Domain`, `Infrastructure`.
2. **Understand the Flow (15 mins)**: 
   `Client Request` → `API Controller` → `Payload mapped to DTO` → `Application Service (Business Logic)` → `Domain (Specs)` → `Infrastructure (Repo)` → `Service returns ServiceResult` → `Controller maps to HTTP Status`.
3. **Explore Key Files (20 mins)**:
    - Base Service Template: `SFARS.Application/Services/GenericService.cs`
    - Base Repository: `SFARS.Infrastructure/Repositories/GenericRepository.cs`
    - Controller Example: `SFARS.API/Controllers/UserController.cs`
    - Result Envelope: `SFARS.Application/Services/ServiceResult.cs`
4. **Run and Debug (15 mins)**:
    - Set `SFARS.API` as your Startup Project.
    - Put a breakpoint in any Controller (e.g., `UserController`).
    - Make a request via Swagger and step through the `Controller` → `Service` → `Repository` flow.

---

## 🧱 1. Architecture Rules

We use a strict **Clean Architecture** with 4 layers. The core dependency rule is: **Inner layers CANNOT depend on outer layers.** (Dependency flows inward).

### Layer Responsibilities
| Layer | Can Do | Cannot Do |
| --- | --- | --- |
| **Domain** (`SFARS.Domain`) | Define Entities (`BaseEntity`), Interfaces, Enums, Constants, Specifications. | NO database logic, NO HTTP, NO DTOs. Zero dependencies on other layers. |
| **Application** (`SFARS.Application`) | Business logic (`*Service`), DTOs, FluentValidation, Mapster mappings, MediatR events. | NO direct `DbContext` calls, NO HTTP/Controller logic. Must use `IUnitOfWork` and interfaces. |
| **Infrastructure** (`SFARS.Infrastructure`)| EF Core `DbContext`, Repositories, Redis Cache, SignalR Hubs, External APIs. | NO business rules. Implements what Application/Domain needs. |
| **API** (`SFARS.API`) | Controllers, Middlewares, DI Config, Route constants (`APIRoute.cs`), Request payloads. | NO business logic, NO direct database queries. Must call Application Services. |

### 🚫 Strict Anti-Patterns to Avoid
1. **Bypassing `ServiceResult`**: Controllers returning primitive types or throwing business exceptions. ALWAYS return `ServiceResult` from Services and use `.ToIActionResult()` in Controllers.
2. **Infrastructure Leakage**: Don't use Entity Framework `IQueryable` in Controllers or the Application layer. Use Domain Specifications and Repositories.
3. **Scattered Routes**: Hardcoding strings like `[Route("api/users")]` in Controllers. ALWAYS use `APIRoute` constants.
4. **Blocking Async**: Using `.Result` or `.Wait()` in async flows.

---

## 📁 2. Folder & File Convention

### Where to put things:
- **New Entity**: `SFARS.Domain/Entities/` (Must inherit from `BaseEntity`)
- **New Interface**: `SFARS.Domain/Interfaces/`
- **New Specification**: `SFARS.Domain/Specifications/`
- **New Service**: `SFARS.Application/Services/<Feature>/` (Suffix with `Service`)
- **New DTO**: `SFARS.Application/Dtos/<Feature>/` (Suffix with `Dto`)
- **New Validator**: `SFARS.Application/Validations/`
- **New Mapping**: `SFARS.Application/Mappings/MappingRegistration.cs`
- **New API Payload**: `SFARS.API/Payloads/Request/<Feature>/` (Suffix with `Request`)
- **New Route Def**: `SFARS.API/Payloads/APIRoute.cs`

### Naming Rules:
- **Interfaces**: Prefix with `I` (e.g., `IIncidentService`).
- **Classes**: PascalCase.
- **Payloads**: Request models from clients suffix with `Request`.
- **DTOs**: Application layer data objects suffix with `Dto`.
- **Async Methods**: End with `Async` (e.g., `CreateUserAsync`).

---

## 🔄 3. Feature Development Workflow (CRITICAL)

When adding a new feature (e.g., "Manage Snakes"), follow this exact order:

**1. Domain Layer (Start here)**
- Create the Entity `Snake` inheriting `BaseEntity`.
- Create `SnakeSpecification` if custom query logic (filters/includes) is needed.

**2. Infrastructure Layer**
- Create `SnakeConfiguration` implementing `IEntityTypeConfiguration<Snake>`.
- Add `DbSet<Snake>` to `ApplicationDbContext`.
- Run EF Core Migration.

**3. Application Layer**
- Create `SnakeDto`, `CreateSnakeDto`.
- Configure Mapster mapping in `MappingRegistration.cs`.
- Create Validator (e.g., `CreateSnakeValidator`).
- Create `ISnakeService` and `SnakeService`.
- Implement business logic returning `IServiceResult` (using `IUnitOfWork.Repository<Snake, Guid>()`).

**4. API Layer**
- Create Request payload `CreateSnakeRequest`.
- Create extension method to map `Request` -> `Dto`.
- Add endpoints in `APIRoute.cs`.
- Create `SnakeController`, inject `ISnakeService`.
- Map payload, call service, return `this.ToIActionResult(result)`.

---

## ✅ 4. Mandatory Developer Checklist (AUTO-CHECK)

Before opening a Pull Request, you **MUST** verify:
- [ ] **Architecture**: Is the file in the correct layer?
- [ ] **API Logic**: Is the Controller completely free of business logic and DB queries?
- [ ] **Domain Purity**: Is the Domain layer free of EF Core/HTTP concepts?
- [ ] **Dependency Injection**: Did I register my new service in `DependencyInjection.cs`?
- [ ] **Route Definitions**: Are my endpoint routes defined globally in `APIRoute.cs`?
- [ ] **Return Types**: Does my Service return a `ServiceResult` wrapper?
- [ ] **Validation**: Are business rules validated via FluentValidation in the Application layer?
- [ ] **Mapping**: Is Mapster configured instead of manual field mapping?
- [ ] **Testing**: Have I written Application layer unit tests covering happy paths & warning codes?
- [ ] **Error Handling**: Did I use `ResultCodeConst` and `SystemMessageService` instead of hardcoding error strings?

---

## 🔍 5. Impact Analysis Rule

When modifying existing files, trace dependencies safely:
1. **Modifying an Entity**:
   - Update `IEntityTypeConfiguration` in Infrastructure.
   - Run EF Core migrations.
   - Check related DTOs and Mapster configs to ensure fields align.
2. **Modifying a Specification**:
   - Will it break DB indexes? Avoid query transformations that negate index usage (e.g., lowercasing in predicates).
   - Check if `EnableSplitQuery()` is needed if you added multiple `.Include()` statements.
3. **Modifying Base Classes (`GenericService`, `BaseEntity`)**:
   - 🚨 STOP! Base classes impact the entire system. Consult the lead architect before proceeding.
   - Run ALL unit tests before and after.

---

## 🧪 6. Testing Rules

- **What must be unit tested**: Application Services are the primary target. You MUST test the core business logic, validation branches, and `ServiceResult` outputs (Success vs. Warning codes).
- **Where to place tests**: `SFARS.Tests/Application/Services/<Feature>/<Feature>ServiceTests.cs`.
- **Mocking**: Use `Moq` for `IUnitOfWork`, Repositories, and External Services. NEVER connect to the real database in unit tests.
- **Naming Convention**: `MethodName_Scenario_ExpectedBehavior` 
  *(e.g., `CreateIncidentAsync_ValidData_ReturnsSuccessResult`)*

---

## 🚨 7. Common Mistakes (FROM THIS CODEBASE)

1. **Mixing Pagination Semantics**: 
   - *Risk*: Client bugs.
   - *Why*: Our standard is 1-based pagination (`BaseSpecParams.Page` defaults to 1). Do not write custom 0-based integer pages in new endpoints.
2. **Leaking Entity Framework into Application**: 
   - *Risk*: Un-testable code, scattered logic. 
   - *Why*: Writing `.Where(x => ...).Include(...)` directly in the service instead of using a `BaseSpecification<T>`. 
3. **Hardcoding Error Messages**: 
   - *Risk*: Inconsistent localization and messy client parsing.
   - *Why*: Do not hardcode "User not found" in services. Use `ResultCodeConst.SYS_Warning0004` and fetch it via `SystemMessageService`.
4. **Skipping the Request -> DTO Map**: 
   - *Risk*: Coupling API to Application.
   - *Why*: Passing the API payload (`[FromBody] Request`) directly into the Application service breaks boundaries. Always map to a `Dto` via an extension method first.

---

## 💡 BONUS: Developer Templates

### Feature Template (Controller)
```csharp
[Authorize]
[ApiController]
public class FeatureController : ControllerBase
{
    private readonly IFeatureService _featureService;

    public FeatureController(IFeatureService featureService) => _featureService = featureService;

    [HttpPost(APIRoute.Feature.Create, Name = nameof(CreateFeatureAsync))]
    public async Task<IActionResult> CreateFeatureAsync([FromBody] CreateFeatureRequest req)
    {
        // 1. Map API Request to Application DTO
        var dto = req.ToDto();
        
        // 2. Call Service
        var result = await _featureService.CreateAsync(dto);
        
        // 3. Return Standardized API Response
        return this.ToIActionResult(result);
    }
}
```

### Code Review (Reviewer) Checklist
- [ ] Does it pass all existing unit tests?
- [ ] Is `ServiceResult` used for all business flow returns?
- [ ] Are EF Core queries efficient? (Are we using `AsNoTracking` for reads?)
- [ ] Is there ANY business logic in the Controller? *(Reject if yes)*
- [ ] Are magic strings avoided? (Use Enums/Constants/Route constants)
