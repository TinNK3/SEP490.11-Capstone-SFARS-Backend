using Mapster;
using SFARS.Application.Dtos;
using SFARS.Application.Dtos.Incident;
using SFARS.Application.Dtos.Role;
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

            // Incident mapping: Point → Lat/Lon, Victim → VictimName
            config.NewConfig<Incident, IncidentDto>()
                .Map(dest => dest.Latitude, src => src.Location != null ? src.Location.Y : 0)
                .Map(dest => dest.Longitude, src => src.Location != null ? src.Location.X : 0)
                .Map(dest => dest.VictimName, src => src.Victim != null ? src.Victim.FullName : null);
        }
    }
}