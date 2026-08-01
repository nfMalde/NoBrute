using System;

namespace NoBrute.Models
{
    public class NoBruteRequestCheck
    {
        public bool IsGreenRequest { get; set; }

        public int AppendRequestTime { get; set; }

        public string RemoteAddr { get; set; }

        public int RequestNum { get; set; }

        public DateTime ResetTime { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the request must be rejected immediately because the
        /// global entry limit (<c>NoBrute:MaxTrackedEntries</c>) is reached.
        /// The filters answer with <see cref="BlockedStatusCode"/> and never invoke the action.
        /// </summary>
        public bool IsBlocked { get; set; }

        /// <summary>
        /// Gets or sets the status code to answer with when <see cref="IsBlocked"/> is <c>true</c>.
        /// Default is <c>429</c>.
        /// </summary>
        public int BlockedStatusCode { get; set; } = 429;
    }
}
