// Copyright 2020-2023 Raising the Floor - US, Inc.
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/raisingthefloor/morphic-windows/blob/master/LICENSE.txt
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

using Morphic.Client.Config;
using Morphic.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Morphic.Client.Utils;

internal class TelemetryUtils
{
     internal struct TelemetryIdComponents
     {
          public string CompositeId;
          public string? SiteId;
          public string DeviceUuid;
     }
     // NOTE: in this implementation, we must have already populated ConfigurableFeatures with its site ID before calling this function
     internal static async Task<TelemetryIdComponents> GetOrCreateTelemetryIdComponentsAsync()
     {
          var telemetrySiteId = ConfigurableFeatures.TelemetrySiteId;
          var hasValidTelemetrySiteId = (telemetrySiteId is not null) && (telemetrySiteId != String.Empty);

          // retrieve the telemetry device ID for this device; if it doesn't exist then create a new one
          var telemetryCompositeId = AppOptions.Current.TelemetryDeviceUuid;
          if ((telemetryCompositeId is null) || (telemetryCompositeId == string.Empty) || (telemetryCompositeId.IndexOf("D_") < 0))
          {
               Guid anonDeviceUuid;
               // if the configuration file has a telemetry site id, hash the MAC address to derive a one-way hash for pseudonomized device telemetry; note that this will only happen when sites opt-in to site grouping by specifying the site id
               if (hasValidTelemetrySiteId == true)
               {
                    // NOTE: this derivation is used because sites often reinstall computers frequently (sometimes even daily), so this provides some pseudonomous stability with the site's telemetry data
                    var hashedMacAddressGuid = TelemetryUtils.GetHashedMacAddressForSiteTelemetryId();
                    anonDeviceUuid = hashedMacAddressGuid ?? Guid.NewGuid();
               }
               else
               {
                    // for non-siteID computers, just generate a GUID
                    anonDeviceUuid = Guid.NewGuid();
               }

               telemetryCompositeId = "D_" + anonDeviceUuid.ToString();
               AppOptions.Current.TelemetryDeviceUuid = telemetryCompositeId;
          }

          // if a site id is (or is not) configured, modify the telemetry device uuid accordingly
          // NOTE: we handle cases of site ids changing, site IDs being added post-deployment, and site IDs being removed post-deployment
          var unmodifiedTelemetryDeviceCompositeId = telemetryCompositeId;
          if (hasValidTelemetrySiteId == true)
          {
               // NOTE: in the future, consider reporting or throwing an error if the site id required sanitization (i.e. wasn't valid)
               var sanitizedTelemetrySiteId = TelemetryUtils.SanitizeSiteId(telemetrySiteId!);
               if (sanitizedTelemetrySiteId != "")
               {
                    // we have a telemetry site id; prepend it
                    telemetryCompositeId = TelemetryUtils.PrependSiteIdToTelemetryCompositeId(telemetryCompositeId, sanitizedTelemetrySiteId);
               }
               else
               {
                    // the supplied site id isn't valid; strip off the site id; In the future consider logging/reporting an error
                    telemetryCompositeId = TelemetryUtils.RemoveSiteIdFromTelemetryCompositeId(telemetryCompositeId);
               }
          }
          else
          {
               // no telemetry site id is configured; strip off any site id which might have already been part of our telemetry id
               // TODO: in the future, make sure that the telemetry ID wasn't _just_ the site id (as it cannot be allowed to be empty)
               telemetryCompositeId = TelemetryUtils.RemoveSiteIdFromTelemetryCompositeId(telemetryCompositeId);
          }
          // if the telemetry uuid has changed (because of the site id), update our stored telemetry uuid now
          if (telemetryCompositeId != unmodifiedTelemetryDeviceCompositeId)
          {
               AppOptions.Current.TelemetryDeviceUuid = telemetryCompositeId;
          }

          // capture the raw device UUID
          var indexOfTelemetryDeviceUuid = telemetryCompositeId.IndexOf("D_") + 2;
          var telemetryDeviceUuid = telemetryCompositeId.Substring(indexOfTelemetryDeviceUuid);

          return new TelemetryIdComponents() { CompositeId = telemetryCompositeId, SiteId = telemetrySiteId, DeviceUuid = telemetryDeviceUuid };
     }

     private static string PrependSiteIdToTelemetryCompositeId(string value, string telemetrySiteId)
     {
          var telemetryDeviceUuid = value;

          if (telemetryDeviceUuid.StartsWith("S_"))
          {
               // if the telemetry device uuid already starts with a site id, strip it off now
               telemetryDeviceUuid = telemetryDeviceUuid.Remove(0, 2);
               var indexOfForwardSlash = telemetryDeviceUuid.IndexOf('/');
               if (indexOfForwardSlash >= 0)
               {
                    // strip the site id off the front
                    telemetryDeviceUuid = telemetryDeviceUuid.Substring(indexOfForwardSlash + 1);
               }
               else
               {
                    // the site ID was the only contents; return null
                    telemetryDeviceUuid = "";
               }
          }

          // prepend the site id to the telemetry device uuid
          telemetryDeviceUuid = "S_" + telemetrySiteId + "/" + telemetryDeviceUuid;
          return telemetryDeviceUuid;
     }

     private static string RemoveSiteIdFromTelemetryCompositeId(string value)
     {
          var telemetryDeviceUuid = value;

          if (telemetryDeviceUuid.StartsWith("S_"))
          {
               // if the telemetry device uuid starts with a site id, strip it off now
               telemetryDeviceUuid = telemetryDeviceUuid.Remove(0, 2);
               var indexOfForwardSlash = telemetryDeviceUuid.IndexOf('/');
               if (indexOfForwardSlash >= 0)
               {
                    // strip the site id off the front
                    telemetryDeviceUuid = telemetryDeviceUuid.Substring(indexOfForwardSlash + 1);
               }
               else
               {
                    // the site ID is the only contents
                    telemetryDeviceUuid = "";
               }
          }

          return telemetryDeviceUuid;
     }

     internal static string SanitizeSiteId(string siteId)
     {
          var siteIdAsCharacters = siteId.ToCharArray();
          var resultAsCharacters = new List<char>();
          foreach (var character in siteIdAsCharacters)
          {
               if ((character >= 'a' && character <= 'z') ||
                   (character >= 'A' && character <= 'Z') ||
                   (character >= '0' && character <= '9'))
               {
                    resultAsCharacters.Add(character);
               }
               else
               {
                    // filter out this character
               }

          }

          return new string(resultAsCharacters.ToArray());
     }

     // NOTE: this function returns null if no network interface MAC could be determined
     private static Guid? GetHashedMacAddressForSiteTelemetryId()
     {
          // get the MAC address of the network interface which is handling Internet traffic
          var macAddressAsByteArray = Morphic.WindowsNative.Networking.NetworkInterface.GetMacAddressOfInternetNetworkInterface();
          //
          // if we couldn't find the MAC address of a network card currently connected to the Internet, gracefully degrade and select the "most used" active network interface instead
          if (macAddressAsByteArray is null)
          {
               macAddressAsByteArray = Morphic.WindowsNative.Networking.NetworkInterface.GetMacAddressOfHighestTrafficActiveNetworkInterface();
          }
          // if we couldn't find the MAC address of a network card which is currently active, gracefully degrade and select the "most used" network interface (presumably inactive, since we already checked for active ones) instead
          if (macAddressAsByteArray is null)
          {
               macAddressAsByteArray = Morphic.WindowsNative.Networking.NetworkInterface.GetMacAddressOfHighestTrafficNetworkInterface();
          }
          // if we couldn't find the MAC address of _any_ network card, it might be because there are _no_ RX/TX cards available; in that scenario, look for _any_ network card (even an RX-only one)
          if (macAddressAsByteArray is null)
          {
               macAddressAsByteArray = Morphic.WindowsNative.Networking.NetworkInterface.GetMacAddressOfFirstNetworkInterface();
          }
          //
          // if we couldn't find any network interface with a non-zero MAC, then return null
          if (macAddressAsByteArray is null)
          {
               return null;
          }

          StringBuilder macAddressAsHexStringBuilder = new();
          foreach (var element in macAddressAsByteArray)
          {
               var elementAsHexString = element.ToString("X2");
               macAddressAsHexStringBuilder.Append(elementAsHexString);
          }
          var macAddressAsHexString = macAddressAsHexStringBuilder.ToString();

          // at this point, we have a network MAC address which is reasonably stable (i.e. is suitable to derive a telemetry GUID-sized value from)
          // convert the mac address (hex string) to a type 3 UUID (MD5-hashed); note that we pre-pend "MAC_" before the mac address to avoid internal collissions from other types of potentially-derived site telemetry ids
          var createUuidResult = TelemetryUtils.CreateVersion3Uuid(macAddressAsHexString);
          if (createUuidResult.IsError == true)
          {
               return null;
          }
          var macAddressAsMd5HashedGuid = createUuidResult.Value!;

          return macAddressAsMd5HashedGuid;
     }

     private static MorphicResult<Guid, MorphicUnit> CreateVersion3Uuid(string value)
     {
          var Namespace_MorphicMAC = new Guid("472c19e2-b87f-47c2-b7d3-dd9c175a5cfa");

          var valueToHash = Namespace_MorphicMAC.ToString("B") + value;

          // NOTE: type 3 GUIDs have 122 bits of "random" data; in this case, it'll be a one-way hash derived from a MAC address (so that we aren't capturing the raw mac addresses of site computers)
          var md5 = MD5.Create();
          var buffer = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(valueToHash));

          // NOTE: MD5 should create 16-byte hashes; if it created a longer array then cut it down to size; if it created a shorter array then return an error
          if (buffer.Length > 16)
          {
               Array.Resize(ref buffer, 16);
          }
          else if (buffer.Length < 16)
          {
               return MorphicResult.ErrorResult();
          }

          // clear the fields where version and variant will live
          buffer[6] &= 0b0000_1111;
          buffer[8] &= 0b0011_1111;
          //
          // set the version and variant bits
          buffer[6] |= 0b0011_0000; // version 3
          buffer[8] |= 0b1000_0000; // 0b10 represents an RFC 4122 UUID

          // turn the buffer into a guid
          var bufferAsGuid = new Guid(buffer);

          return MorphicResult.OkResult(bufferAsGuid);
     }
}
