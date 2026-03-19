using FluentValidation;
using Mapster;
using MapsterMapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SFARS.Application.Dtos;
using SFARS.Application.Dtos.AiInference;
using SFARS.Application.Dtos.AiReview;
using SFARS.Application.Dtos.Auth;
using SFARS.Application.Dtos.Facility;
using SFARS.Application.Dtos.Faq;
using SFARS.Application.Dtos.Incident;
using SFARS.Application.Dtos.Role;
using SFARS.Application.Dtos.Transaction;
using SFARS.Application.Dtos.User;
using SFARS.Application.Services;
using SFARS.Application.Services.Auth;
using SFARS.Domain.Interfaces.Services;
using SFARS.Application.Utils;
using SFARS.Domain.Interfaces.Services.Base;
using System.Reflection;

namespace SFARS.Application;

public static class DependencyInjection
{

    //	Summary:
    //		This class is to configure services for application layer
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        // In-memory cache used by TokenBlacklistService (must be Singleton)
        services.AddMemoryCache();
        services.AddSingleton<ITokenBlacklistService, TokenBlacklistService>();

        // Register application services
        services.AddScoped<ISystemMessageService, SystemMessageService>();
        services.AddScoped(typeof(IGenericService<,,>), typeof(GenericService<,,>));
        services.AddScoped(typeof(IReadOnlyService<,,>), typeof(ReadOnlyService<,,>));
        services.AddScoped<ISnakeService<SnakeDto>, SnakeService>();

        // Auth services
        services.AddScoped<IJwtUtils, JwtUtils>();
        services.AddScoped<IUserService<UserDto>, UserService>();
        services.AddScoped<ISystemRoleService<SystemRoleDto>, SystemRoleService>();
        services.AddScoped<IRefreshTokenService<RefreshTokenDto>, RefreshTokenService>();
        services.AddScoped<IAuthService<AuthUserDto>, AuthService>();

        // Incident services
        services.AddScoped<IIncidentService<IncidentDto>, IncidentService>();
        services.AddScoped<IAiInferenceService, AiInferenceService>();
        services.AddScoped<IAiReviewService<SubmitAiReviewRequestDto, FirstAidStepDto>, AiReviewService>();
        services.AddScoped<IDispatchService, DispatchService>();
        services.AddScoped<IMissionService, MissionService>();
        services.AddScoped<IDeviceService, DeviceService>();

        // Admin services — methods added directly to IUserService / UserService
        services.AddScoped<IAdminAuditLogService, AdminAuditLogService>();
        services.AddScoped<IDataSetExportService, DataSetExportService>();
        services.AddScoped<IRetrainOrchestrationService, RetrainOrchestrationService>();

        // Chat services
        services.AddScoped<IChatService, ChatService>();

        // Facility services
        services.AddScoped<IMedicalFacilityService<FacilityDto>, MedicalFacilityService>();

        // Rescuer services
        services.AddScoped<IRescuerService, RescuerService>();

        // Community Post services
        services.AddScoped<ICommunityPostService, CommunityPostService>();

        // FAQ services
        services.AddScoped<IFaqService<FaqDto>, Services.Faq.FaqService>();

        // Transaction / Donation services
        services.AddScoped<ITransactionService<TransactionDto>, TransactionService>();

        // Register all validators from this assembly
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        // Register MediatR (IPublisher, IMediator, and all handlers from this assembly)
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));

        services.ConfigureMapster();


        //services
        //    .ConfigureCloudinary(); // Add cloudinary


        return services;
    }

    // Configure Mapster
    public static IServiceCollection ConfigureMapster(this IServiceCollection services)
    {
        TypeAdapterConfig.GlobalSettings.Default
            .MapToConstructor(true)
            .PreserveReference(true);
        // Get Mapster GlobalSettings
        var typeAdapterConfig = TypeAdapterConfig.GlobalSettings;
        // Scans the assembly and gets the IRegister, adding the registration to the TypeAdapterConfig
        typeAdapterConfig.Scan(Assembly.GetExecutingAssembly());

        // Register the mapper as Singleton service for my application
        var mapperConfig = new Mapper(typeAdapterConfig);
        services.AddSingleton<IMapper>(mapperConfig);

        return services;
    }

    public static IServiceCollection ConfigureCloudinary(this IServiceCollection services)
    {
        // Configure this later...

        return services;
    }
}