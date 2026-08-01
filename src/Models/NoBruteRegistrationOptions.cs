namespace NoBrute.Models
{
    /// <summary>
    /// Options for configuring which NoBrute filters are registered in DI and, optionally,
    /// settings that would otherwise be read from the <c>NoBrute</c> configuration section.
    /// </summary>
    public class NoBruteRegistrationOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether to register the MVC action filter attribute.
        /// Default is <c>true</c>.
        /// </summary>
        public bool UseMvc { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to register the Razor Pages page filter.
        /// Default is <c>false</c>.
        /// </summary>
        public bool UseRazorPages { get; set; }

        /// <summary>
        /// Gets or sets the client IP resolution options. If <c>null</c>, the
        /// <c>NoBrute:ClientIp</c> configuration section is used.
        /// </summary>
        public NoBruteClientIpOptions ClientIp { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of clients tracked at the same time.
        /// <c>0</c> means unlimited. If <c>null</c>, <c>NoBrute:MaxTrackedEntries</c> is used.
        /// </summary>
        public int? MaxTrackedEntries { get; set; }

        /// <summary>
        /// Gets or sets the status code returned once <see cref="MaxTrackedEntries"/> is reached.
        /// If <c>null</c>, <c>NoBrute:BlockedStatusCode</c> is used (default <c>429</c>).
        /// </summary>
        public int? BlockedStatusCode { get; set; }

        /// <summary>
        /// Gets or sets the upper bound in milliseconds for the delay added to a single request.
        /// <c>0</c> means unlimited. If <c>null</c>, <c>NoBrute:MaxIncreaseRequestTime</c> is used.
        /// </summary>
        public int? MaxIncreaseRequestTime { get; set; }
    }
}
