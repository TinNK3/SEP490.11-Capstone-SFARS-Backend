using SFARS.API.Payloads.Request.Auth;
using SFARS.API.Payloads.Request.Incident;
using SFARS.API.Payloads.Request.Snake;
using SFARS.API.Payloads.Request.User;
using SFARS.Application.Dtos;
using SFARS.Application.Dtos.Auth;
using SFARS.Application.Dtos.Incident;
using SFARS.Application.Dtos.User;

namespace SFARS.API.Extension
{
    public static class PayloadExtensions
    {
        #region Auth

        // Mapping from typeof(SignInWithPasswordRequest) to typeof(AuthenticateUserDto)
        public static AuthUserDto ToAuthenticatedUser(this SignInWithPasswordRequest req)
            => new AuthUserDto
            {
                Email = req.Email,
                Password = req.Password
            };

        // Mapping from typeof(SignUpRequest) to typeof(AuthenticateUserDto)
        public static AuthUserDto ToAuthenticatedUser(this SignUpRequest req)
            => new AuthUserDto
            {
                Email = req.Email,
                FirstName = req.FirstName,
                LastName = req.LastName,
                Password = req.Password,
                IsRescuer = false
            };

        // Mapping from typeof(SignInWithOtpRequest) to typeof(AuthenticateUserDto)
        public static AuthUserDto ToAuthenticatedUser(this SignInWithOtpRequest req)
            => new AuthUserDto
            {
                Email = req.Email,
                Password = null!
            };
        #endregion


        #region Snake
        // Mapping from typeof(CreateSnakeRequest) to typeof(SnakeDto)
        public static SnakeDto ToSnake(this CreateSnakeRequest req)
        {
            return new SnakeDto
            {
                CommonName = req.CommonName,
                ScientificName = req.ScientificName,
                ToxicityLevel = req.ToxicityLevel,
                ToxinGroup = req.ToxinGroup,
                Description = req.Description,
                KeyIdentifiers = req.KeyIdentifiers,
                TypicalSymptoms = req.TypicalSymptoms,
                Habitat = req.Habitat,
                DistributionNote = req.DistributionNote,
                Note = req.Note,
                IsActive = true
            };
        }

        // Mapping from typeof(UpdateSnakeRequest) to typeof(SnakeDto)
        public static SnakeDto ToSnakeForUpdate(this UpdateSnakeRequest req)
        {
            return new SnakeDto
            {
                CommonName = req.CommonName,
                ScientificName = req.ScientificName,
                ToxicityLevel = req.ToxicityLevel,
                ToxinGroup = req.ToxinGroup,
                Description = req.Description,
                KeyIdentifiers = req.KeyIdentifiers,
                TypicalSymptoms = req.TypicalSymptoms,
                Habitat = req.Habitat,
                DistributionNote = req.DistributionNote,
                Note = req.Note,
                IsActive = req.IsActive
            };
        }
        #endregion

        #region User
        // Mapping from typeof(UpdateProfileRequest) to typeof(UserDto)
        public static UserDto ToUserForUpdate(this UpdateProfileRequest req)
        {
            return new UserDto
            {
                FirstName = req.FirstName,
                LastName = req.LastName,
                Phone = req.Phone,
                Avatar = req.Avatar,
                Address = req.Address,
                Gender = req.Gender,
                Dob = req.Dob
            };
        }
        #endregion

        #region Incident
        // Mapping from typeof(CreateIncidentRequest) to typeof(IncidentDto)
        public static IncidentDto ToIncidentDto(this CreateIncidentRequest req)
        {
            return new IncidentDto
            {
                Latitude = req.Latitude,
                Longitude = req.Longitude,
                AddressString = req.AddressString,
                Description = req.Description,
                PriorityLevel = req.PriorityLevel
            };
        }
        #endregion
    }
}