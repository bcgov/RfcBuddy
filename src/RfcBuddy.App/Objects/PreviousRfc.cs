namespace RfcBuddy.App.Objects
{
    /// <summary>
    /// Holds a single previously reviewed RFC, including hash values of its previous asset tags, description, and risk assessment.
    /// </summary>
    /// <remarks>
    /// Creates a previous RFC
    /// </remarks>
    /// <param name="rfcNumber">The RFC number</param>
    public class PreviousRfc(string rfcNumber)
    {
        /// <summary>
        /// The RFC number
        /// </summary>
        public string RfcNumber { get; set; } = rfcNumber;

        /// <summary>
        /// A hash of the RFC's asset tags
        /// </summary>
        public string AssetTagsHash { get; set; } = string.Empty;

        /// <summary>
        /// The start date and time
        /// </summary>
        public DateTime StartDate { get; set; }

        /// <summary>
        /// The end date and time
        /// </summary>
        public DateTime EndDate { get; set; }

        /// <summary>
        /// A hash of the RFC's description
        /// </summary>
        public string DescriptionHash { get; set; } = string.Empty;

        /// <summary>
        /// A hash of the RFC's risk assessment
        /// </summary>
        public string RiskAssessmentHash { get; set; } = string.Empty;
    }
}
