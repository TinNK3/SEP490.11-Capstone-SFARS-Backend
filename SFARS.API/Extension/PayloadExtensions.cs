using SFARS.API.Payloads.Request.Auth;
using SFARS.API.Payloads.Request.Snake;
using SFARS.API.Payloads.Request.User;
using SFARS.Application.Dtos;
using SFARS.Application.Dtos.Auth;
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
        #endregion


        #region Snake
        // Mpapping from typeof(CreatSnakeRequest) to typeof(SnakeDto)
        public static SnakeDto ToSnake(this CreateSnakeRequest req)
        {
            return new SnakeDto
            {
                CommonName = req.CommonName,
                ScientificName = req.ScientificName,
                ToxicityLevel = req.ToxicityLevel,
                Description = req.Description,
                Habitat = req.Habitat,
                IsActive = true
            };
        }

        // Mpapping from typeof(UpdateSnakeRequest) to typeof(SnakeDto)
        public static SnakeDto ToSnakeForUpdate(this UpdateSnakeRequest req)
        {
            return new SnakeDto
            {
                CommonName = req.CommonName,
                ScientificName = req.ScientificName,
                ToxicityLevel = req.ToxicityLevel,
                Description = req.Description,
                Habitat = req.Habitat,
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
    }
}