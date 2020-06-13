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
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;
using System.IO.Compression;
using System.IO;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Linq;
using System.Buffers.Text;

namespace Morphic.Settings.Files
{
    public class FilesValue
    {
        public FileValue[] Files { get; set; } = null!;

        public static bool TryFromObject(object? value, out FilesValue result)
        {
            if (value is Dictionary<string, object> dictionaryValue)
            {
                var files = new List<FileValue>();
                if (dictionaryValue.TryGetValue("files", out var dictionaryFileValues))
                {
                    if (dictionaryFileValues is object?[] dictionaryFileValuesArray)
                    {
                        foreach (var dictionaryFileValue in dictionaryFileValuesArray)
                        {
                            if (FileValue.TryFromObject(dictionaryFileValue, out var file))
                            {
                                files.Add(file);
                            }
                        }
                    }
                    result = new FilesValue();
                    result.Files = files.ToArray();
                    return true;
                }
            }
            result = null!;
            return false;
        }

        public Dictionary<string, object?> ToDictionary()
        {
            return new Dictionary<string, object?>
            {
                { "files", Files.Select(file => file.ToDictionary()).ToArray() }
            };
        }
    }

    public class FileValue
    {
        public string RelativePath { get; set; } = null!;

        public string EncodedContents { get; set; } = null!;

        public byte[] Contents
        {
            get
            {
                var compressed = Convert.FromBase64String(EncodedContents);
                using (var compressedStream = new MemoryStream(compressed))
                {
                    using (var uncompressedStream = new MemoryStream())
                    {
                        using (var gzip = new GZipStream(compressedStream, CompressionMode.Decompress))
                        {
                            gzip.CopyTo(uncompressedStream);
                        }
                        var uncompressed = uncompressedStream.ToArray();
                        return uncompressed;
                    }
                }
            }
            set
            {
                var uncompressed = value;
                using (var uncompressedStream = new MemoryStream(uncompressed))
                {
                    using (var compressedStream = new MemoryStream())
                    {
                        using (var gzip = new GZipStream(compressedStream, CompressionMode.Compress))
                        {
                            uncompressedStream.CopyTo(gzip);
                        }
                        var compresssed = compressedStream.ToArray();
                        EncodedContents = Convert.ToBase64String(compresssed);
                    }
                }
            }
        }

        public static bool TryFromObject(object? value, out FileValue result)
        {
            result = new FileValue();
            if (value is Dictionary<string, object?> dictionaryValue)
            {
                object? relativePath;
                if (!dictionaryValue.TryGetValue("relative_path", out relativePath))
                {
                    return false;
                }
                if (relativePath is string relativePathString)
                {
                    result.RelativePath = relativePathString;
                }
                else
                {
                    return false;
                }
                object? b64Gzip;
                if (!dictionaryValue.TryGetValue("b64gzip", out b64Gzip))
                {
                    return false;
                }
                if (b64Gzip is string b64GzipString)
                {
                    result.EncodedContents = b64GzipString;
                }
                else
                {
                    return false;
                }
                return true;
            }
            return false;
        }

        public Dictionary<string, object?> ToDictionary()
        {
            return new Dictionary<string, object?>
            {
                { "relative_path", RelativePath },
                { "b64gzip", EncodedContents }
            };
        }
    }
}
