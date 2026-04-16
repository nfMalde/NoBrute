namespace NoBrute.Models
{
    /// <summary>
    /// Options for configuring which NoBrute filters are registered in DI.
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
    }
}
