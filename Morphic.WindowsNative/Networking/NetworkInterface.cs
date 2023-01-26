// Copyright 2022 Raising the Floor - US, Inc.
//
// Licensed under the New BSD license. You may not use this file except in
// compliance with this License.
//
// You may obtain a copy of the License at
// https://github.com/raisingthefloor/morphic-windowsnative-lib-cs/blob/master/LICENSE
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
using System.Collections.Generic;
using System.Text;
using Windows.Networking.Connectivity;

namespace Morphic.WindowsNative.Networking
{
    public class NetworkInterface
    {
        // NOTE: this function returns null if no Internet-connected network interface is found
        public static byte[]? GetMacAddressOfInternetNetworkInterface()
        {
            var internetConnectionProfile = NetworkInformation.GetInternetConnectionProfile();
            if (internetConnectionProfile is null)
            {
                return null;
            }

            // get the network adapter which provides connectivity for the Internet connection
            var internetConnectionNetworkAdapter = internetConnectionProfile.NetworkAdapter;
            //
            // get the network adapter's ID (guid) 
            var networkAdapterId = internetConnectionNetworkAdapter.NetworkAdapterId;
            // format the network adapter's ID in the format: {00000000-0000-0000-0000-000000000000}
            var networkAdapterIdAsString = networkAdapterId.ToString("B");

            // now search for this network adapter and then extract its MAC address
            var allNetworkInterfaces = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
            foreach (var networkInterface in allNetworkInterfaces)
            {
                if (networkInterface.Id.ToLowerInvariant() == networkAdapterIdAsString.ToLowerInvariant())
                {
                    // network ID GUID matches; retrieve the MAC address
                    var macAddressAsBytes = networkInterface.GetPhysicalAddress().GetAddressBytes();
                    if (NetworkInterface.IsEmptyOrAllZeroes(macAddressAsBytes) == false)
                    {
                        return macAddressAsBytes;
                    }
                }
            }

            // if we could not find the active network connection, return null
            return null;
        }

        // NOTE: this function returns null if no active (up) network interface is found
        public static byte[]? GetMacAddressOfHighestTrafficActiveNetworkInterface()
        {
            return GetMacAddressOfHighestTrafficNetworkInterface(mustBeActive: true);
        }

        // NOTE: this function returns null if no active (up) network interface is found
        public static byte[]? GetMacAddressOfHighestTrafficNetworkInterface()
        {
            return GetMacAddressOfHighestTrafficNetworkInterface(mustBeActive: false);
        }

        // NOTE: this function returns null if no network interface is found (or if 'mustBeActive' is set to true and no active (up) network interface is found)
        private static byte[]? GetMacAddressOfHighestTrafficNetworkInterface(bool mustBeActive)
        {
            byte[]? highestTrafficNetworkInterface = null;
            ulong? highestTotalBytesSentOrReceived = null;

            var allNetworkInterfaces = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
            foreach (var networkInterface in allNetworkInterfaces)
            {
                // skip read-only interfaces
                if (networkInterface.IsReceiveOnly == true)
                {
                    continue;
                }

                if (mustBeActive == true)
                {
                    // skip interfaces which aren't active
                    if (networkInterface.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up)
                    {
                        continue;
                    }
                }

                var ipStatistics = networkInterface.GetIPStatistics();
                var currentBytesSent = Math.Max(0, ipStatistics.BytesSent);
                var currentBytesReceived = Math.Max(0, ipStatistics.BytesReceived);
                ulong currentTotalBytesSentOrReceived = (ulong)ipStatistics.BytesSent + (ulong)ipStatistics.BytesReceived;

                if (highestTotalBytesSentOrReceived is null || currentTotalBytesSentOrReceived > highestTotalBytesSentOrReceived!)
                {
                    var currentNetworkInterface = networkInterface.GetPhysicalAddress().GetAddressBytes();
                    if (NetworkInterface.IsEmptyOrAllZeroes(currentNetworkInterface) == true)
                    {
                        continue;
                    }
                    //
                    highestTrafficNetworkInterface = currentNetworkInterface;
                    highestTotalBytesSentOrReceived = currentTotalBytesSentOrReceived;
                }
            }

            if (highestTrafficNetworkInterface is null || NetworkInterface.IsEmptyOrAllZeroes(highestTrafficNetworkInterface) == true)
            {
                return null;
            }
            return highestTrafficNetworkInterface;
        }

        // NOTE: this function returns null if no network interface is found
        public static byte[]? GetMacAddressOfFirstNetworkInterface()
        {
            var allNetworkInterfaces = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
            if (allNetworkInterfaces.Length == 0)
            {
                return null;
            }

            foreach (var networkInterface in allNetworkInterfaces)
            {
                var result = networkInterface.GetPhysicalAddress().GetAddressBytes();
                if (NetworkInterface.IsEmptyOrAllZeroes(result) == false)
                {
                    return result;
                }
            }

            return null;
        }

        private static bool IsEmptyOrAllZeroes(byte[] value)
        {
            if (value.Length == 0)
            {
                return true;
            }

            foreach (var b in value)
            {
                if (b != 0)
                {
                    return false;
                }
            }

            // if there was at least one byte and all bytes were zeroes, then return true
            return true;
        }
    }
}
