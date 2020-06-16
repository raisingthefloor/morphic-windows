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
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using System.Text.RegularExpressions;

namespace Morphic.Settings.Files
{
    public class FileManager : IFileManager
    {

        public bool Exists(string path)
        {
            return File.Exists(path) || Directory.Exists(path);
        }

        public string[] FilenamesInDirectory(string path)
        {
            return Directory.GetFiles(path);
        }

        public async Task<byte[]> ReadAllBytes(string path)
        {
            return await File.ReadAllBytesAsync(path);
        }

        public async Task WriteAllBytes(string path, byte[] contents)
        {
            await File.WriteAllBytesAsync(path, contents);
        }

        public Task Delete(string path)
        {
            File.Delete(path);
            return Task.CompletedTask;
        }

        public void CreateDirectory(string path)
        {
            Directory.CreateDirectory(path);
        }
    }
}
