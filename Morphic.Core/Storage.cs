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
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Morphic.Core
{

    /// <summary>
    /// Options for a <code>Storage</code> instance
    /// </summary>
    /// <remarks>
    /// Designed so one or more options can be passed to the Storage constructor via
    /// dependency injection
    /// </remarks>
    public class StorageOptions
    {
        public string RootPath = "";
    }

    /// <summary>
    /// A Storage manager for Morphic objects
    /// </summary>
    public class Storage
    {

        /// <summary>
        /// Create a  new storage instance
        /// </summary>
        /// <remarks>
        /// Intended to be created via dependency injection
        /// </remarks>
        /// <param name="options">The options for this storage manager</param>
        /// <param name="logger">A logger for this storage manager</param>
        public Storage(StorageOptions options, ILogger<Storage> logger)
        {
            this.logger = logger;
            RootPath = options.RootPath;
        }

        /// <summary>
        /// The logger used by this storage manager
        /// </summary>
        private readonly ILogger<Storage> logger;

        /// <summary>
        /// The root path of the storage area
        /// </summary>
        private readonly string RootPath;

        /// <summary>
        /// Get a path for the given record identifier and type
        /// </summary>
        /// <param name="identifier">The record's unique identifier</param>
        /// <param name="type">The type of the record</param>
        /// <returns></returns>
        private string PathForRecord(string identifier, Type type)
        {
            return Path.Combine(new string[] { RootPath, type.Name, String.Format("{0}.json", identifier) });
        }

        /// <summary>
        /// Save a record to disk
        /// </summary>
        /// <typeparam name="RecordType">The class of record being saved</typeparam>
        /// <param name="record">The record being saved</param>
        /// <returns>Whether or not the save succeeded</returns>
        public async Task<IMorphicResult> SaveAsync<RecordType>(RecordType record) where RecordType: class, IRecord
        {
            var type = typeof(RecordType);
            var path = PathForRecord(record.Id, type);
            logger.LogInformation("Saving {0}/{1}", type.Name, record.Id);
            var parent = Path.GetDirectoryName(path);
            try
            {
                if (!Directory.Exists(parent))
                {
                    logger.LogInformation("Creating directory {0}", type.Name);
                    Directory.CreateDirectory(parent);
                }
                using (var stream = File.Open(path, File.Exists(path) ? FileMode.Truncate : FileMode.CreateNew, FileAccess.Write))
                {
                    await JsonSerializer.SerializeAsync<RecordType>(stream, record);
                }
                logger.LogInformation("Saved {0}/{1}", type.Name, record.Id);
                return IMorphicResult.SuccessResult;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed to save {0}/{1}", type.Name, record.Id);
                return IMorphicResult.ErrorResult;
            }
        }

        /// <summary>
        /// Load a record for the given identifier and type
        /// </summary>
        /// <typeparam name="RecordType">The type of record to load</typeparam>
        /// <param name="identifier">The record's unique identifier</param>
        /// <returns>The requested record, or <code>null</code> if no such record was found</returns>
        public async Task<RecordType?> LoadAsync<RecordType>(string identifier) where RecordType: class, IRecord
        {
            var type = typeof(RecordType);
            var path = PathForRecord(identifier, type);
            logger.LogInformation("Loading {0}/{1}", type.Name, identifier);
            try
            {
                if (File.Exists(path))
                {
                    var options = new JsonSerializerOptions();
                    options.Converters.Add(new JsonElementInferredTypeConverter());
                    using (var stream = File.OpenRead(path))
                    {
                        var record = await JsonSerializer.DeserializeAsync<RecordType>(stream, options);
                        return record;
                    }
                }
                logger.LogInformation("Not such record {0}/{1}", type.Name, identifier);
                return null;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed to read {0}/{1}", type.Name, identifier);
                return null;
            }
        }

        /// <summary>
        /// Check if a record exists
        /// </summary>
        /// <typeparam name="RecordType">The type of record to check</typeparam>
        /// <param name="identifier">The record's unique identifier</param>
        /// <returns>Whether the record is saved on disk or not</returns>
        public bool Exists<RecordType>(string identifier) where RecordType: class, IRecord
        {
            var path = PathForRecord(identifier, typeof(RecordType));
            return File.Exists(path);
        }
    }
}
