using NoBrute.Models;

namespace NoBrute.Domain
{
    public interface INoBrute
    {
        /// <summary>
        /// Checks the request.
        /// </summary>
        /// <param name="requestName">Name of the request.</param>
        /// <returns></returns>
        NoBruteRequestCheck CheckRequest(string requestName = null);

        /// <summary>
        /// Releases the request.
        /// </summary>
        /// <param name="requestName">Name of the request.</param>
        /// <returns></returns>
        bool ReleaseRequest(string requestName = null);

        /// <summary>
        /// Automatics the process request release.
        /// </summary>
        /// <param name="status">The status.</param>
        /// <param name="requestName">Name of the request.</param>
        /// <returns></returns>
        bool AutoProcessRequestRelease(int status, string requestName = null);
    }
}