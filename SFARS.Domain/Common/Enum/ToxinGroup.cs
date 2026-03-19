using System.ComponentModel;

namespace SFARS.Domain.Common.Enum
{
    public enum ToxinGroup
    {
        [Description("Không rõ")]
        Unknown = 0,

        [Description("Không độc")]
        NonVenomous = 1,

        [Description("Độc thần kinh (Neurotoxin)")]
        Neurotoxin = 2,

        [Description("Độc gây rối loạn đông máu / xuất huyết (Hemotoxin)")]
        Hemotoxin = 3,

        [Description("Độc hoại tử mô (Cytotoxin)")]
        Cytotoxin = 4,

        [Description("Độc gây tổn thương cơ (Myotoxin)")]
        Myotoxin = 5,

        // optional: nhiều loài có độc phối hợp
        [Description("Độc hỗn hợp")]
        Mixed = 6,

        [Description("Những điều không nên làm")]
        GeneralProhibition = 7
    }
}