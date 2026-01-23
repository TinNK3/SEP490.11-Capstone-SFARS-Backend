using System.ComponentModel;

namespace SFARS.Domain.Common.Enum
{
    public enum VehicleType
    {
        [Description("Xe máy")]
        Motorbike,
        [Description("Ô tô")]
        Car,
        [Description("Xe cứu thương")]
        Ambulance,
        [Description("Xe chuyên dụng")]
        SpecializedVehicle,
        [Description("Khác")]
        Other
    }
}