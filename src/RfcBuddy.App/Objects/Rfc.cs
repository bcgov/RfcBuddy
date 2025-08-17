namespace RfcBuddy.App.Objects
{
    /// <summary>
    /// Holds a single RFC.
    /// </summary>
    /// <remarks>
    /// Creates a new RFC
    /// </remarks>
    /// <param name="rfcNumber">The RFC number</param>
    public class Rfc(string rfcNumber)
    {
        /// <summary>
        /// The RFC number
        /// </summary>
        public string RfcNumber { get; set; } = rfcNumber;

        /// <summary>
        /// The approval status, e.g. pending, approved, disapproved
        /// </summary>
        public string ApprovalStatus { get; set; } = string.Empty;

        /// <summary>
        /// The platform for this RFC
        /// </summary>
        public string Platform { get; set; } = string.Empty;

        /// <summary>
        /// Affected asset tags
        /// </summary>
        public string AssetTags { get; set; } = string.Empty;

        /// <summary>
        /// The start date and time
        /// </summary>
        public DateTime StartDate { get; set; }

        /// <summary>
        /// The end date and time
        /// </summary>
        public DateTime EndDate { get; set; }

        /// <summary>
        /// The RFC's description
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// The RFC's risk assessment
        /// </summary>
        public string RiskAssessment { get; set; } = string.Empty;

        /// <summary>
        /// A list of important keywords in this RFC. Those can be highlighted when generating user output.
        /// </summary>
        public List<string> Keywords { get; set; } = [];
    }
}
