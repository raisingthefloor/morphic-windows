using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
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

using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.IO;

namespace Morphic.Settings.Files
{
    public class FilesSettingHandler : SettingHandler
    {

        public Setting Setting { get; }

        public FilesSettingHandlerDescription Description
        {
            get
            {
                return (Setting.HandlerDescription as FilesSettingHandlerDescription)!;
            }
        }

        public FilesSettingHandler(Setting setting, IFileManager fileManager, ILogger<FilesSettingHandler> logger)
        {
            Setting = setting;
            this.fileManager = fileManager;
            this.logger = logger;
            root = ExpandedPath(Description.Root);
        }

        private readonly IFileManager fileManager;
        private readonly string root;
        private readonly ILogger<FilesSettingHandler> logger;

        public override async Task<bool> Apply(object? value)
        {
            try
            {
                if (fileManager.Exists(root))
                {
                    if (FilesValue.TryFromObject(value, out var filesValue))
                    {
                        var pathsToRemove = new HashSet<string>(expandedFilenames);
                        foreach (var fileValue in filesValue.Files)
                        {
                            var path = Path.Combine(root, fileValue.RelativePath);
                            pathsToRemove.Remove(path);
                            var dirname = Path.GetDirectoryName(path)!;
                            if (!fileManager.Exists(dirname))
                            {
                                fileManager.CreateDirectory(dirname);
                            }
                            await fileManager.WriteAllBytes(path, fileValue.Contents);
                        }
                        foreach (var path in pathsToRemove)
                        {
                            await fileManager.Delete(path);
                        }
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed to apply files");
            }
            return false;
        }

        public override async Task<CaptureResult> Capture()
        {
            var result = new CaptureResult();
            try
            {
                if (fileManager.Exists(root))
                {
                    var files = new List<FileValue>();
                    foreach (var path in expandedFilenames)
                    {
                        if (fileManager.Exists(path))
                        {
                            var file = new FileValue();
                            file.RelativePath = Path.GetRelativePath(root, path);
                            file.Contents = await fileManager.ReadAllBytes(path);
                            files.Add(file);
                        }
                    }
                    FilesValue value = new FilesValue();
                    value.Files = files.ToArray();
                    result.Value = value.ToDictionary();
                    result.Success = true;
                }
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed to capture files");
            }
            return result;
        }

        private string[] expandedFilenames
        {
            get
            {
                var expanded = new List<string>();
                foreach (var filename in Description.Files)
                {
                    var path = Path.Combine(root, filename);
                    var directory = Path.GetDirectoryName(path)!;
                    var basename = Path.GetFileName(path)!;
                    if (basename.EndsWith("*"))
                    {
                        expanded.AddRange(PathsMatchingTrailingWildcard(directory, basename.Substring(0, basename.Length - 1)));
                    }
                    else if (basename.StartsWith("*"))
                    {
                        expanded.AddRange(PathsMatchingLeadingWildcard(directory, basename.Substring(1)));
                    }
                    else
                    {
                        expanded.Add(path);
                    }
                }
                return expanded.ToArray();
            }
        }

        string[] PathsMatchingTrailingWildcard(string directoryPath, string prefix)
        {
            var matches = new List<string>();
            foreach (var filename in fileManager.FilenamesInDirectory(directoryPath))
            {
                if (prefix == "" || filename.StartsWith(prefix))
                {
                    matches.Add(Path.Combine(directoryPath, filename));
                }
            }
            return matches.ToArray();
        }

        string[] PathsMatchingLeadingWildcard(string directoryPath, string suffix)
        {
            var matches = new List<string>();
            foreach (var filename in fileManager.FilenamesInDirectory(directoryPath))
            {
                if (suffix == "" || filename.EndsWith(suffix))
                {
                    matches.Add(Path.Combine(directoryPath, filename));
                }
            }
            return matches.ToArray();
        }
    }
}
