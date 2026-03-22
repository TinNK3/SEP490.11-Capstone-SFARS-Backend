using SFARS.API.Payloads.Request.Admin;
using SFARS.API.Payloads.Request.Auth;
using SFARS.API.Payloads.Request.Facility;
using SFARS.API.Payloads.Request.Faq;
using SFARS.API.Payloads.Request.Incident;
using SFARS.API.Payloads.Request.Rescuer;
using SFARS.API.Payloads.Request.Snake;
using SFARS.API.Payloads.Request.Transaction;
using SFARS.API.Payloads.Request.User;
using SFARS.Application.Dtos;
using SFARS.Application.Dtos.Auth;
using SFARS.Application.Dtos.Facility;
using SFARS.Application.Dtos.Faq;
using SFARS.Application.Dtos.Incident;
using SFARS.Application.Dtos.Rescuer;
using SFARS.Application.Dtos.Transaction;
using SFARS.Application.Dtos.User;

namespace SFARS.API.Extensions
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

        #region Rescuer

        // Mapping from typeof(UpdateRescuerProfileRequest) to typeof(RescuerProfileDto)
        public static RescuerProfileDto ToRescuerProfileDto(this UpdateRescuerProfileRequest req)
            => new RescuerProfileDto
            {
                // User Info
                FirstName = req.FirstName,
                LastName = req.LastName,
                Phone = req.Phone,
                Avatar = req.Avatar,
                Address = req.Address,
                Gender = req.Gender,
                Dob = req.Dob,

                // Rescuer Profile
                ExperienceYears = req.ExperienceYears,
                VehicleType = req.VehicleType,
                LicensePlate = req.LicensePlate,
                CoverageRadiusKM = req.CoverageRadiusKM,
                IsAvailable = req.IsAvailable
            };

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
            };
        }
        #endregion

        #region Facility
        // Mapping from typeof(CreateFacilityRequest) to typeof(FacilityDto)
        public static FacilityDto ToFacilityDto(this CreateFacilityRequest req)
        {
            return new FacilityDto
            {
                Name = req.Name,
                FacilityType = req.FacilityType,
                Latitude = req.Latitude,
                Longitude = req.Longitude,
                Address = req.Address,
                Province = req.Province,
                PhoneNumber = req.PhoneNumber,
                Email = req.Email,
                Website = req.Website,
                OpenHours = req.OpenHours,
                CloseHours = req.CloseHours,
                EmergencyAvailable = req.EmergencyAvailable,
                HasAntivenom = req.HasAntivenom,
                Notes = req.Notes
            };
        }

        // Mapping from typeof(UpdateFacilityRequest) to typeof(FacilityDto)
        public static FacilityDto ToFacilityDto(this UpdateFacilityRequest req)
        {
            return new FacilityDto
            {
                Name = req.Name,
                FacilityType = req.FacilityType,
                Latitude = req.Latitude,
                Longitude = req.Longitude,
                Address = req.Address,
                Province = req.Province,
                PhoneNumber = req.PhoneNumber,
                Email = req.Email,
                Website = req.Website,
                OpenHours = req.OpenHours,
                CloseHours = req.CloseHours,
                EmergencyAvailable = req.EmergencyAvailable,
                HasAntivenom = req.HasAntivenom,
                Notes = req.Notes
            };
        }
        #endregion

        #region Admin
        // Mapping from typeof(CreateUserRequest) to typeof(UserDto)
        public static UserDto ToUserDto(this CreateUserRequest req)
        {
            return new UserDto
            {
                FirstName = req.FirstName,
                LastName = req.LastName,
                Email = req.Email,
                Password = req.Password,
                Phone = req.Phone,
                Role = req.Role.ToString()
            };
        }

        // Mapping from typeof(UpdateUserRequest) to typeof(UserDto)
        public static UserDto ToUserForUpdate(this UpdateUserRequest req)
        {
            return new UserDto
            {
                FirstName = req.FirstName,
                LastName = req.LastName,
                Phone = req.Phone,
                Avatar = req.Avatar,
                Address = req.Address,
                Gender = req.Gender,
                Dob = req.Dob,
                Status = req.Status ?? default,
                Role = req.Role?.ToString(),
                HasStatusUpdate = req.Status.HasValue,
                HasRoleUpdate = req.Role.HasValue
            };
        }
        #endregion

        #region FAQ
        // Mapping from typeof(CreateFaqRequest) to typeof(FaqDto)
        public static FaqDto ToFaqDto(this CreateFaqRequest req)
        {
            return new FaqDto
            {
                Question = req.Question,
                Answer = req.Answer,
                Order = req.Order,
                IsActive = req.IsActive
            };
        }

        // Mapping from typeof(UpdateFaqRequest) to typeof(FaqDto)
        public static FaqDto ToFaqDto(this UpdateFaqRequest req)
        {
            return new FaqDto
            {
                Question = req.Question,
                Answer = req.Answer,
                Order = req.Order,
                IsActive = req.IsActive
            };
        }
        #endregion

        #region Transaction
        // Mapping from typeof(CreateTransactionRequest) to typeof(TransactionDto)
        public static TransactionDto ToTransactionDto(this CreateTransactionRequest req)
        {
            return new TransactionDto
            {
                Amount = req.Amount,
                Description = req.Description
            };
        }
        #endregion
    }
}
