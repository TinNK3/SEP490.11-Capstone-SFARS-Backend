using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SFARS.Domain.Common.Enum
{
    public enum Gender
    {
        [Description("Nam")]
        Male,
        [Description("Nữ")]
        Female,
        [Description("Khác")]
        Other
    }
}