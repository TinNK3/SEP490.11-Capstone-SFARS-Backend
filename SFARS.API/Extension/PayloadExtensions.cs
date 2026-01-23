using SFARS.API.Payloads.Request.Snake;
using SFARS.Application.Dtos;

namespace SFARS.API.Extension
{
    public static class PayloadExtensions
    {
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
    }
}