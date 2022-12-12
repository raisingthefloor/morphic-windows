﻿// Copyright 2020 Raising the Floor - International
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
using System.Net.Http;
using System.Text.Json;
using System.Text;
using Morphic.Core;
using Morphic.Core.Legacy;

namespace Morphic.Service
{
    internal static class HttpRequestMessageExtensions
    {
        internal static HttpRequestMessage Create(HttpService service, string path, HttpMethod method)
        {
            var uri = new Uri(service.Endpoint, path);
            var request = new HttpRequestMessage(method, uri);
            if (service.AuthToken is string token)
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
            return request;
        }

        internal static HttpRequestMessage Create<RequestBody>(HttpService service, string path, HttpMethod method, RequestBody body)
        {
            var request = Create(service, path, method);
            var options = new JsonSerializerOptions();
            options.Converters.Add(new JsonElementInferredTypeConverter());
            var json = JsonSerializer.Serialize(body, options);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            return request;
        }
    }
}
