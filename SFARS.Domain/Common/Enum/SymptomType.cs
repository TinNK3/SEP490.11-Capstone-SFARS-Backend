namespace SFARS.Domain.Common.Enum
{
    /// <summary>
    /// Represents the clinical symptoms captured via the victim's UI.
    /// Used for strict ENUM mapping to track patient statistics safely without relying on localized strings.
    /// </summary>
    public enum SymptomType
    {
        /// <summary>
        /// Victim reports feeling fine, zero symptoms.
        /// </summary>
        None = 0,

        /// <summary>
        /// Breathing difficulties, chest pain - critical.
        /// </summary>
        BreathingDifficulty = 1,

        /// <summary>
        /// Any form of bleeding (bite site, systemic).
        /// </summary>
        Bleeding = 2,

        /// <summary>
        /// Ptosis, blurred vision, jaw lock, facial paralysis (Neurotoxin flag).
        /// </summary>
        Ptosis = 3,

        /// <summary>
        /// Swelling, necrosis or compartment syndrome flags.
        /// </summary>
        Swelling = 4,

        /// <summary>
        /// Systemic nausea, vomiting, dizziness.
        /// </summary>
        VomitingDizziness = 5,

        /// <summary>
        /// Severe pain or burning sensation.
        /// </summary>
        Pain = 6
    }
}