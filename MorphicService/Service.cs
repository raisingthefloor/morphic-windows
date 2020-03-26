using System;

#nullable enable

namespace MorphicService
{
    /// <summary>
    /// An interface to the Morphic HTTP API
    /// </summary>
    /// <remarks>
    /// The service is implemented via extensions across multiple files
    /// </remarks>
    public class Service
    {

        /// <summary>
        /// Create a new service with the given endpoint and session
        /// </summary>
        /// <param name="endpoint">The root URL of the morphic server</param>
        /// <param name="session">The session in which to make requests</param>
        public Service(Uri endpoint, Session session)
        {
            Endpoint = endpoint;
            WeakSession = new WeakReference<Session>(session);
        }

        /// <summary>
        /// The root URL of the morphic server
        /// </summary>
        public Uri Endpoint { get; }

        /// <summary>
        /// The session in which to make requests
        /// </summary>
        public WeakReference<Session> WeakSession { get; }

        public Session Session
        {
            get
            {
                if (WeakSession.TryGetTarget(out var session))
                {
                    return session;
                }
#pragma warning disable CS8603 // Possible null reference return.
                return null;
#pragma warning restore CS8603 // Possible null reference return.
            }
        }
         
    }
}

#nullable disable