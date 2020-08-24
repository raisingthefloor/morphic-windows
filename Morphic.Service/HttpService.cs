// Copyright 2020 Raising the Floor - International
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/GPII/universal/blob/master/LICENSE.txt
//
// The R&D leading to these results received funding from the:
// * Rehabilitation Services Administration, US Dept. of Education under 
//   grant H421A150006 (APCP)
// * National Institute on Disability, Independent Living, and 
//   Rehabilitation Research (NIDILRR)
// * Administration for Independent Living & Dept. of Education under grants 
//   H133E080022 (RERC-IT) and H133E130028/90RE5003-01-00 (UIITA-RERC)
// * European Union's Seventh Framework Programme (FP7/2007-2013) grant 
//   agreement nos. 289016 (Cloud4all) and 610510 (Prosperity4All)
// * William and Flora Hewlett Foundation
// * Ontario Ministry of Research and Innovation
// * Canadian Foundation for Innovation
// * Adobe Foundation
// * Consumer Electronics Association Foundation

using System;

#nullable enable

namespace Morphic.Service
{
    /// <summary>
    /// An interface to the Morphic HTTP API
    /// </summary>
    /// <remarks>
    /// The service is implemented via extensions across multiple files
    /// </remarks>
    public class HttpService
    {

        /// <summary>
        /// Create a new service with the given endpoint and session
        /// </summary>
        /// <param name="endpoint">The root URL of the morphic server</param>
        /// <param name="session">The session in which to make requests</param>
        public HttpService(Uri endpoint, Session session)
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