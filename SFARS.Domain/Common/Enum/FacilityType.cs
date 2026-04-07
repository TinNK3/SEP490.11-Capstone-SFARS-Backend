using System.ComponentModel;

namespace SFARS.Domain.Common.Enum
{
    public enum FacilityType
    {
        [Description("Bệnh viện")]
        Hospital,
        [Description("Phòng khám")]
        Clinic,
        [Description("Trung tâm y tế")]
        HealthCenter,
        [Description("Nhà thuốc")]
        Pharmacy,
        [Description("Trạm y tế")]
        MedicalStation
    }
}