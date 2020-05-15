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

using System.Net.Http;
using System.Linq;
using System;
using System.Text.Json;
using System.Threading.Tasks;
using Morphic.Core;

#nullable enable

namespace Morphic.Service
{
    internal static class HttpResponseMessageExtensions
    {
        internal static async Task<T?> GetObject<T>(this HttpResponseMessage response) where T : class
        {
            if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                var errorJson = await response.Content.ReadAsStreamAsync();
                try
                {
                    var options = new JsonSerializerOptions();
                    options.Converters.Add(new JsonElementInferredTypeConverter());
                    var error = await JsonSerializer.DeserializeAsync<Session.BadRequestException>(errorJson, options);
                    throw error;
                }
                catch (JsonException)
                {
                    return null;
                }
            }
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }
            if (response.Content.Headers.ContentType.MediaType != "application/json")
            {
                return null;
            }
            if (response.Content.Headers.ContentType.CharSet != "utf-8")
            {
                return null;
            }
            var json = await response.Content.ReadAsStreamAsync();
            try
            {
                var options = new JsonSerializerOptions();
                options.Converters.Add(new JsonElementInferredTypeConverter());
                return await JsonSerializer.DeserializeAsync<T>(json, options);
            }
            catch
            {
                return null;
            }
        }

        internal static bool RequiresMorphicAuthentication(this HttpResponseMessage response)
        {
            return response.StatusCode == System.Net.HttpStatusCode.Unauthorized;
        }
    }
}

#nullable disable