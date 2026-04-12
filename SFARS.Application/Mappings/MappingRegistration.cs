using Mapster;
using SFARS.Application.Dtos;
using SFARS.Application.Dtos.Admin;
using SFARS.Application.Dtos.Report;
using SFARS.Application.Dtos.Facility;
using SFARS.Application.Dtos.Incident;
using SFARS.Application.Dtos.Rescuer;
using SFARS.Application.Dtos.Role;
using SFARS.Application.Dtos.Transaction;
using SFARS.Application.Dtos.User;
using SFARS.Domain.Entities;

namespace SFARS.Application.Mappings
{
    public class MappingRegistration : IRegister
    {
        public void Register(TypeAdapterConfig config)
        {
            // From [Entity] to [Dto]
            config.NewConfig<Snake, SnakeDto>();
            config.NewConfig<Role, SystemRoleDto>();
            config.NewConfig<User, UserDto>()
            .Map(dest => dest.Role,
                 src => src.UserRoles != null
                     ? src.UserRoles.Select(x => x.Role.RoleName).FirstOrDefault()
                     : null);

            // AdminAuditLog mapping: Admin navigation → AdminName, enum Description → ActionDisplay
            config.NewConfig<AdminAuditLog, AdminAuditLogDto>()
                .Map(dest => dest.AdminName,
                     src => src.Admin != null
                         ? src.Admin.FirstName + " " + src.Admin.LastName
                         : null)
                .Map(dest => dest.ActionDisplay,
                     src => src.Action.ToString());

            // Incident mapping: Point → Lat/Lon, Victim → VictimName
            config.NewConfig<Incident, IncidentDto>()
                .Map(dest => dest.Latitude, src => src.Location != null ? src.Location.Y : 0)
                .Map(dest => dest.Longitude, src => src.Location != null ? src.Location.X : 0)
                .Map(dest => dest.VictimName, src => src.Victim != null ? src.Victim.FullName : null)
                .Map(dest => dest.SymptomAudioUrl, src => src.SymptomAudioUrl)
                .Map(dest => dest.MinutesSinceBite, src => src.MinutesSinceBite)
                .Map(dest => dest.ExtractedSymptoms, src => src.ExtractedSymptoms)
                .Ignore(dest => dest.SymptomText); // Handled by service projection to enforce Admin-only rule


            // RescuerProfile → RescuerProfileDto: include User fields via navigation property
            config.NewConfig<RescuerProfile, RescuerProfileDto>()
                .Map(dest => dest.UserId, src => src.UserId)
                .Map(dest => dest.FirstName, src => src.User != null ? src.User.FirstName : null!)
                .Map(dest => dest.LastName, src => src.User != null ? src.User.LastName : null!)
                .Map(dest => dest.Phone, src => src.User != null ? src.User.Phone : null)
                .Map(dest => dest.Avatar, src => src.User != null ? src.User.Avatar : null)
                .Map(dest => dest.Address, src => src.User != null ? src.User.Address : null)
                .Map(dest => dest.Gender, src => src.User != null ? src.User.Gender : null)
                .Map(dest => dest.Dob, src => src.User != null ? src.User.Dob : null);

            // MedicalFacility mapping: Point → Lat/Lon
            config.NewConfig<MedicalFacility, FacilityDto>()
                .Map(dest => dest.Latitude, src => src.Location != null ? src.Location.Y : 0)
                .Map(dest => dest.Longitude, src => src.Location != null ? src.Location.X : 0)
                .Map(dest => dest.FacilityType, src => src.Type);

            // Transaction mapping
            config.NewConfig<Transaction, TransactionDto>()
                .Map(dest => dest.CheckoutUrl, src => (string?)null); // populated by service after PayOS call

            // Report mapping
            config.NewConfig<Report, ReportResponse>()
                .Map(dest => dest.ReporterName,
                     src => src.Reporter != null
                         ? src.Reporter.FullName
                         : "Unknown");
        }
    }
}