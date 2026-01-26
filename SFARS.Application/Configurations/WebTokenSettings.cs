using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SFARS.Application.Configurations
{
    public class WebTokenSettings
    {
        public bool ValidateIssuerSigningKey { get; set; }
        public string IssuerSigningKey { get; set; } = string.Empty;
        public bool ValidateIssuer { get; set; }
        public string ValidIssuer { get; set; } = string.Empty;
        public bool ValidateAudience { get; set; }
        public string ValidAudience { get; set; } = string.Empty;
        public bool RequireExpirationTime { get; set; }
        public bool ValidateLifetime { get; set; }
        public int TokenLifeTimeInMinutes { get; set; }
        public int MaxRefreshTokenLifeSpan { get; set; }
        public int RefreshTokenLifeTimeInMinutes { get; set; }
        public int RecoveryPasswordLifeTimeInMinutes { get; set; }
        public int MfaTokenLifeTimeInMinutes { get; set; }
        public int PaymentTokenLifeTimeInMinutes { get; set; }
    }
}
