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
using MorphicCore;
using System.Security.Cryptography;

namespace MorphicWin
{

    class DataProtector : IDataProtection
    {

        public byte[] Protect(byte[] userData)
        {
            var generator = RandomNumberGenerator.Create();
            var entropy = new byte[64];
            generator.GetBytes(entropy);
            var encrypted = ProtectedData.Protect(userData, entropy, DataProtectionScope.CurrentUser);
            var payload = new byte[1 + entropy.Length + encrypted.Length];
            payload[0] = (byte)entropy.Length;
            entropy.CopyTo(payload, 1);
            encrypted.CopyTo(payload, entropy.Length);
            return payload;
        }

        public byte[] Unprotect(byte[] payload)
        {
            var entropyLength = payload[0];
            if (entropyLength >= payload.Length - 1)
            {
                throw new System.Exception("Invalid entropy length");
            }
            var entropy = new byte[entropyLength];
            Array.Copy(payload, 1, entropy, 0, entropyLength);
            var encrypted = new byte[payload.Length - 1 - entropyLength];
            Array.Copy(payload, 1 + entropyLength, encrypted, 0, encrypted.Length);
            return ProtectedData.Unprotect(encrypted, entropy, DataProtectionScope.CurrentUser);
        }

    }

}