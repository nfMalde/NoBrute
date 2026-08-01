using NoBrute.Models;
using System.Threading;
using System.Threading.Tasks;

namespace NoBrute.Domain
{
    public interface INoBrute
    {
        /// <summary>
        /// Checks the request.
        /// </summary>
        /// <param name="requestName">Name of the request.</param>
        /// <returns></returns>
        /// <remarks>
        /// Prefer <see cref="CheckRequestAsync(string, CancellationToken)"/> in request pipelines:
        /// with an <c>IDistributedCache</c> this overload blocks the calling thread while waiting for the cache.
        /// </remarks>
        NoBruteRequestCheck CheckRequest(string requestName = null);

        /// <summary>
        /// Checks the request without blocking the calling thread.
        /// </summary>
        /// <param name="requestName">Name of the request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        /// <remarks>
        /// The default implementation falls back to the synchronous overload so that existing custom
        /// implementations keep working. Override it to benefit from the non blocking cache access.
        /// </remarks>
        Task<NoBruteRequestCheck> CheckRequestAsync(string requestName = null, CancellationToken cancellationToken = default)
            => Task.FromResult(CheckRequest(requestName));

        /// <summary>
        /// Releases the request.
        /// </summary>
        /// <param name="requestName">Name of the request.</param>
        /// <returns></returns>
        bool ReleaseRequest(string requestName = null);

        /// <summary>
        /// Releases the request without blocking the calling thread.
        /// </summary>
        /// <param name="requestName">Name of the request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        /// <remarks>
        /// The default implementation falls back to the synchronous overload so that existing custom
        /// implementations keep working. Override it to benefit from the non blocking cache access.
        /// </remarks>
        Task<bool> ReleaseRequestAsync(string requestName = null, CancellationToken cancellationToken = default)
            => Task.FromResult(ReleaseRequest(requestName));

        /// <summary>
        /// Automatics the process request release.
        /// </summary>
        /// <param name="status">The status.</param>
        /// <param name="requestName">Name of the request.</param>
        /// <returns></returns>
        bool AutoProcessRequestRelease(int status, string requestName = null);

        /// <summary>
        /// Automatics the process request release without blocking the calling thread.
        /// </summary>
        /// <param name="status">The status.</param>
        /// <param name="requestName">Name of the request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns></returns>
        /// <remarks>
        /// The default implementation falls back to the synchronous overload so that existing custom
        /// implementations keep working. Override it to benefit from the non blocking cache access.
        /// </remarks>
        Task<bool> AutoProcessRequestReleaseAsync(int status, string requestName = null, CancellationToken cancellationToken = default)
            => Task.FromResult(AutoProcessRequestRelease(status, requestName));
    }
}
