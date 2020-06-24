using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Morphic.Settings.Files;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.IO.Compression;

namespace Morphic.Settings.Tests
{

#nullable enable

    class MockFileManager : IFileManager
    {

        private Dictionary<string, byte[]> filesByPath = new Dictionary<string, byte[]>();

        public Task Delete(string path)
        {
            filesByPath.Remove(path);
            return Task.CompletedTask;
        }

        public bool Exists(string path)
        {
            return filesByPath.ContainsKey(path) || directoryPaths.Contains(path);
        }

        public string[] FilenamesInDirectory(string directoryPath)
        {
            var matches = new List<string>();
            foreach (var path in filesByPath.Keys)
            {
                if (Path.GetDirectoryName(path) == directoryPath)
                {
                    matches.Add(Path.GetFileName(path));
                }
            }
            return matches.ToArray();
        }

        public Task<byte[]> ReadAllBytes(string path)
        {
            if (filesByPath.TryGetValue(path, out var contents))
            {
                return Task.FromResult(contents);
            }
            throw new Exception("File not found");
        }

        public Task WriteAllBytes(string path, byte[] contents)
        {
            var dirname = Path.GetDirectoryName(path);
            if (!directoryPaths.Contains(dirname))
            {
                throw new Exception("Cannot create file without first creating parent directory");
            }
            filesByPath[path] = contents;
            return Task.CompletedTask;
        }

        HashSet<string> directoryPaths = new HashSet<string>();

        public void CreateDirectory(string path)
        {
            if (path != "" && !directoryPaths.Contains(path))
            {
                directoryPaths.Add(path);
                if (Path.GetDirectoryName(path) is string parent)
                {
                    CreateDirectory(parent);
                }
            }
        }
    }

#nullable disable

    public class FilesSettingHandlerTests
    {

        private byte[] Decode(string b64gzip)
        {
            var compressed = Convert.FromBase64String(b64gzip);
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

        [Fact]
        public async Task TestCaptureExisting()
        {
            var setting = new Setting()
            {
                Name = "configuration",
                HandlerDescription = new FilesSettingHandlerDescription(@"C:\test\settings", new string[] {
                    @"test1.ini",
                    @"test2.ini",
                    @"subfolder\test1.ini"
                })
            };
            var logging = new LoggerFactory();
            var logger = logging.CreateLogger<FilesSettingHandler>();
            var fileManager = new MockFileManager();
            fileManager.CreateDirectory(@"C:\test\settings");
            fileManager.CreateDirectory(@"C:\test\settings\subfolder");
            await fileManager.WriteAllBytes(@"C:\test\settings\test1.ini", new byte[] { 0x00, 0x01, 0x02, 0x03 });
            await fileManager.WriteAllBytes(@"C:\test\settings\test2.ini", new byte[] { 0x01, 0x01, 0x01, 0x01 });
            await fileManager.WriteAllBytes(@"C:\test\settings\subfolder\test1.ini", new byte[] { 0x10, 0x11, 0x12, 0x13 });
            var handler = new FilesSettingHandler(setting, fileManager, logger);

            var result = await handler.Capture();
            Assert.True(result.Success);
            Assert.IsType<Dictionary<string, object>>(result.Value);
            var dictionaryValue = (Dictionary<string, object>)result.Value;
            Assert.True(dictionaryValue.TryGetValue("files", out var filesObject));
            Assert.IsType<Dictionary<string, object>[]>(filesObject);
            var filesArray = (Dictionary<string, object>[])filesObject;
            Assert.Equal(3, filesArray.Length);
            Assert.IsType<Dictionary<string, object>>(filesArray[0]);
            var fileDictionary = (Dictionary<string, object>)filesArray[0];
            Assert.True(fileDictionary.TryGetValue("relative_path", out var relativePathObject));
            Assert.IsType<string>(relativePathObject);
            Assert.Equal(@"test1.ini", (string)relativePathObject);
            Assert.True(fileDictionary.TryGetValue("b64gzip", out var b64gzip));
            Assert.IsType<string>(b64gzip);
            var contents = Decode((string)b64gzip);
            Assert.Equal(4, contents.Length);
            Assert.Equal(0x00, contents[0]);
            Assert.Equal(0x01, contents[1]);
            Assert.Equal(0x02, contents[2]);
            Assert.Equal(0x03, contents[3]);
            Assert.IsType<Dictionary<string, object>>(filesArray[1]);
            fileDictionary = (Dictionary<string, object>)filesArray[1];
            Assert.True(fileDictionary.TryGetValue("relative_path", out relativePathObject));
            Assert.IsType<string>(relativePathObject);
            Assert.Equal(@"test2.ini", (string)relativePathObject);
            Assert.True(fileDictionary.TryGetValue("b64gzip", out b64gzip));
            Assert.IsType<string>(b64gzip);
            contents = Decode((string)b64gzip);
            Assert.Equal(4, contents.Length);
            Assert.Equal(0x01, contents[0]);
            Assert.Equal(0x01, contents[1]);
            Assert.Equal(0x01, contents[2]);
            Assert.Equal(0x01, contents[3]);
            Assert.IsType<Dictionary<string, object>>(filesArray[2]);
            fileDictionary = (Dictionary<string, object>)filesArray[2];
            Assert.True(fileDictionary.TryGetValue("relative_path", out relativePathObject));
            Assert.IsType<string>(relativePathObject);
            Assert.Equal(@"subfolder\test1.ini", (string)relativePathObject);
            Assert.True(fileDictionary.TryGetValue("b64gzip", out b64gzip));
            Assert.IsType<string>(b64gzip);
            contents = Decode((string)b64gzip);
            Assert.Equal(4, contents.Length);
            Assert.Equal(0x10, contents[0]);
            Assert.Equal(0x11, contents[1]);
            Assert.Equal(0x12, contents[2]);
            Assert.Equal(0x13, contents[3]);
        }

        [Fact]
        public async Task TestCaptureMissingFiles()
        {
            var setting = new Setting()
            {
                Name = "configuration",
                HandlerDescription = new FilesSettingHandlerDescription(@"C:\test\settings", new string[] {
                    @"test1.ini",
                    @"test2.ini",
                    @"subfolder\test1.ini"
                })
            };
            var logging = new LoggerFactory();
            var logger = logging.CreateLogger<FilesSettingHandler>();
            var fileManager = new MockFileManager();
            fileManager.CreateDirectory(@"C:\test\settings");
            await fileManager.WriteAllBytes(@"C:\test\settings\test1.ini", new byte[] { 0x00, 0x01, 0x02, 0x03 });
            await fileManager.WriteAllBytes(@"C:\test\settings\test2.ini", new byte[] { 0x01, 0x01, 0x01, 0x01 });
            var handler = new FilesSettingHandler(setting, fileManager, logger);

            var result = await handler.Capture();
            Assert.True(result.Success);
            Assert.IsType<Dictionary<string, object>>(result.Value);
            var dictionaryValue = (Dictionary<string, object>)result.Value;
            Assert.True(dictionaryValue.TryGetValue("files", out var filesObject));
            Assert.IsType<Dictionary<string, object>[]>(filesObject);
            var filesArray = (Dictionary<string, object>[])filesObject;
            Assert.Equal(2, filesArray.Length);
            Assert.IsType<Dictionary<string, object>>(filesArray[0]);
            var fileDictionary = (Dictionary<string, object>)filesArray[0];
            Assert.True(fileDictionary.TryGetValue("relative_path", out var relativePathObject));
            Assert.IsType<string>(relativePathObject);
            Assert.Equal(@"test1.ini", (string)relativePathObject);
            Assert.True(fileDictionary.TryGetValue("b64gzip", out var b64gzip));
            Assert.IsType<string>(b64gzip);
            var contents = Decode((string)b64gzip);
            Assert.Equal(4, contents.Length);
            Assert.Equal(0x00, contents[0]);
            Assert.Equal(0x01, contents[1]);
            Assert.Equal(0x02, contents[2]);
            Assert.Equal(0x03, contents[3]);
            Assert.IsType<Dictionary<string, object>>(filesArray[1]);
            fileDictionary = (Dictionary<string, object>)filesArray[1];
            Assert.True(fileDictionary.TryGetValue("relative_path", out relativePathObject));
            Assert.IsType<string>(relativePathObject);
            Assert.Equal(@"test2.ini", (string)relativePathObject);
            Assert.True(fileDictionary.TryGetValue("b64gzip", out b64gzip));
            Assert.IsType<string>(b64gzip);
            contents = Decode((string)b64gzip);
            Assert.Equal(4, contents.Length);
            Assert.Equal(0x01, contents[0]);
            Assert.Equal(0x01, contents[1]);
            Assert.Equal(0x01, contents[2]);
            Assert.Equal(0x01, contents[3]);
        }

        [Fact]
        public async Task TestCaptureMissingFolder()
        {
            var setting = new Setting()
            {
                Name = "configuration",
                HandlerDescription = new FilesSettingHandlerDescription(@"C:\test\settings", new string[] {
                    @"test1.ini",
                    @"test2.ini",
                    @"subfolder\test1.ini"
                })
            };
            var logging = new LoggerFactory();
            var logger = logging.CreateLogger<FilesSettingHandler>();
            var fileManager = new MockFileManager();
            var handler = new FilesSettingHandler(setting, fileManager, logger);

            var result = await handler.Capture();
            Assert.False(result.Success);
        }

        [Fact]
        public async Task TestCaptureWildcards()
        {
            var setting = new Setting()
            {
                Name = "configuration",
                HandlerDescription = new FilesSettingHandlerDescription(@"C:\test\settings", new string[] {
                    @"*",
                })
            };
            var logging = new LoggerFactory();
            var logger = logging.CreateLogger<FilesSettingHandler>();
            var fileManager = new MockFileManager();
            fileManager.CreateDirectory(@"C:\test\settings");
            fileManager.CreateDirectory(@"C:\test\settings\subfolder");
            await fileManager.WriteAllBytes(@"C:\test\settings\test1.ini", new byte[] { 0x00, 0x01, 0x02, 0x03 });
            await fileManager.WriteAllBytes(@"C:\test\settings\test2.ini", new byte[] { 0x01, 0x01, 0x01, 0x01 });
            await fileManager.WriteAllBytes(@"C:\test\settings\subfolder\test1.ini", new byte[] { 0x10, 0x11, 0x12, 0x13 });
            var handler = new FilesSettingHandler(setting, fileManager, logger);

            var result = await handler.Capture();
            Assert.True(result.Success);
            Assert.IsType<Dictionary<string, object>>(result.Value);
            var dictionaryValue = (Dictionary<string, object>)result.Value;
            Assert.True(dictionaryValue.TryGetValue("files", out var filesObject));
            Assert.IsType<Dictionary<string, object>[]>(filesObject);
            var filesArray = (Dictionary<string, object>[])filesObject;
            Assert.Equal(2, filesArray.Length);
            Assert.IsType<Dictionary<string, object>>(filesArray[0]);
            var fileDictionary = (Dictionary<string, object>)filesArray[0];
            Assert.True(fileDictionary.TryGetValue("relative_path", out var relativePathObject));
            Assert.IsType<string>(relativePathObject);
            Assert.Equal(@"test1.ini", (string)relativePathObject);
            Assert.True(fileDictionary.TryGetValue("b64gzip", out var b64gzip));
            Assert.IsType<string>(b64gzip);
            var contents = Decode((string)b64gzip);
            Assert.Equal(4, contents.Length);
            Assert.Equal(0x00, contents[0]);
            Assert.Equal(0x01, contents[1]);
            Assert.Equal(0x02, contents[2]);
            Assert.Equal(0x03, contents[3]);
            Assert.IsType<Dictionary<string, object>>(filesArray[1]);
            fileDictionary = (Dictionary<string, object>)filesArray[1];
            Assert.True(fileDictionary.TryGetValue("relative_path", out relativePathObject));
            Assert.IsType<string>(relativePathObject);
            Assert.Equal(@"test2.ini", (string)relativePathObject);
            Assert.True(fileDictionary.TryGetValue("b64gzip", out b64gzip));
            Assert.IsType<string>(b64gzip);
            contents = Decode((string)b64gzip);
            Assert.Equal(4, contents.Length);
            Assert.Equal(0x01, contents[0]);
            Assert.Equal(0x01, contents[1]);
            Assert.Equal(0x01, contents[2]);
            Assert.Equal(0x01, contents[3]);


            setting = new Setting()
            {
                Name = "configuration",
                HandlerDescription = new FilesSettingHandlerDescription(@"C:\test\settings", new string[] {
                    @"*.ini",
                    @"subfolder\test*"
                })
            };
            logging = new LoggerFactory();
            logger = logging.CreateLogger<FilesSettingHandler>();
            fileManager = new MockFileManager();
            fileManager.CreateDirectory(@"C:\test\settings");
            fileManager.CreateDirectory(@"C:\test\settings\subfolder");
            await fileManager.WriteAllBytes(@"C:\test\settings\test1.ini", new byte[] { 0x00, 0x01, 0x02, 0x03 });
            await fileManager.WriteAllBytes(@"C:\test\settings\test2.ini", new byte[] { 0x01, 0x01, 0x01, 0x01 });
            await fileManager.WriteAllBytes(@"C:\test\settings\test.xyz", new byte[] { 0x01, 0x01, 0x01, 0x01 });
            await fileManager.WriteAllBytes(@"C:\test\settings\subfolder\test1.ini", new byte[] { 0x10, 0x11, 0x12, 0x13 });
            await fileManager.WriteAllBytes(@"C:\test\settings\nottest.xyz", new byte[] { 0x01, 0x01, 0x01, 0x01 });
            handler = new FilesSettingHandler(setting, fileManager, logger);

            result = await handler.Capture();
            Assert.True(result.Success);
            Assert.IsType<Dictionary<string, object>>(result.Value);
            dictionaryValue = (Dictionary<string, object>)result.Value;
            Assert.True(dictionaryValue.TryGetValue("files", out filesObject));
            Assert.IsType<Dictionary<string, object>[]>(filesObject);
            filesArray = (Dictionary<string, object>[])filesObject;
            Assert.Equal(3, filesArray.Length);
            Assert.IsType<Dictionary<string, object>>(filesArray[0]);
            fileDictionary = (Dictionary<string, object>)filesArray[0];
            Assert.True(fileDictionary.TryGetValue("relative_path", out relativePathObject));
            Assert.IsType<string>(relativePathObject);
            Assert.Equal(@"test1.ini", (string)relativePathObject);
            Assert.True(fileDictionary.TryGetValue("b64gzip", out b64gzip));
            Assert.IsType<string>(b64gzip);
            contents = Decode((string)b64gzip);
            Assert.Equal(4, contents.Length);
            Assert.Equal(0x00, contents[0]);
            Assert.Equal(0x01, contents[1]);
            Assert.Equal(0x02, contents[2]);
            Assert.Equal(0x03, contents[3]);
            Assert.IsType<Dictionary<string, object>>(filesArray[1]);
            fileDictionary = (Dictionary<string, object>)filesArray[1];
            Assert.True(fileDictionary.TryGetValue("relative_path", out relativePathObject));
            Assert.IsType<string>(relativePathObject);
            Assert.Equal(@"test2.ini", (string)relativePathObject);
            Assert.True(fileDictionary.TryGetValue("b64gzip", out b64gzip));
            Assert.IsType<string>(b64gzip);
            contents = Decode((string)b64gzip);
            Assert.Equal(4, contents.Length);
            Assert.Equal(0x01, contents[0]);
            Assert.Equal(0x01, contents[1]);
            Assert.Equal(0x01, contents[2]);
            Assert.Equal(0x01, contents[3]);
            Assert.IsType<Dictionary<string, object>>(filesArray[2]);
            fileDictionary = (Dictionary<string, object>)filesArray[2];
            Assert.True(fileDictionary.TryGetValue("relative_path", out relativePathObject));
            Assert.IsType<string>(relativePathObject);
            Assert.Equal(@"subfolder\test1.ini", (string)relativePathObject);
            Assert.True(fileDictionary.TryGetValue("b64gzip", out b64gzip));
            Assert.IsType<string>(b64gzip);
            contents = Decode((string)b64gzip);
            Assert.Equal(4, contents.Length);
            Assert.Equal(0x10, contents[0]);
            Assert.Equal(0x11, contents[1]);
            Assert.Equal(0x12, contents[2]);
            Assert.Equal(0x13, contents[3]);
        }

        [Fact]
        public async Task TestApplyExisting()
        {
            var setting = new Setting()
            {
                Name = "configuration",
                HandlerDescription = new FilesSettingHandlerDescription(@"C:\test\settings", new string[] {
                    @"test1.ini",
                    @"test2.ini",
                    @"subfolder\test1.ini"
                })
            };
            var logging = new LoggerFactory();
            var logger = logging.CreateLogger<FilesSettingHandler>();
            var fileManager = new MockFileManager();
            fileManager.CreateDirectory(@"C:\test\settings");
            fileManager.CreateDirectory(@"C:\test\settings\subfolder");
            await fileManager.WriteAllBytes(@"C:\test\settings\test1.ini", new byte[] { 0x00, 0x01, 0x02, 0x03 });
            await fileManager.WriteAllBytes(@"C:\test\settings\test2.ini", new byte[] { 0x01, 0x01, 0x01, 0x01 });
            await fileManager.WriteAllBytes(@"C:\test\settings\subfolder\test1.ini", new byte[] { 0x10, 0x11, 0x12, 0x13 });
            var handler = new FilesSettingHandler(setting, fileManager, logger);

            var value = new FilesValue()
            {
                Files = new FileValue[]
                {
                    new FileValue
                    {
                        RelativePath = @"test1.ini",
                        Contents = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 }
                    },
                    new FileValue
                    {
                        RelativePath = @"test2.ini",
                        Contents = new byte[] { 0x02, 0x02, 0x02 }
                    },
                    new FileValue
                    {
                        RelativePath = @"subfolder\test1.ini",
                        Contents = new byte[] { 0x11, 0x12, 0x13, 0x14 }
                    }
                }
            }.ToDictionary();

            var success = await handler.Apply(value);
            Assert.True(success);
            var contents = await fileManager.ReadAllBytes(@"C:\test\settings\test1.ini");
            Assert.Equal(5, contents.Length);
            Assert.Equal(0x01, contents[0]);
            Assert.Equal(0x02, contents[1]);
            Assert.Equal(0x03, contents[2]);
            Assert.Equal(0x04, contents[3]);
            Assert.Equal(0x05, contents[4]);
            contents = await fileManager.ReadAllBytes(@"C:\test\settings\test2.ini");
            Assert.Equal(3, contents.Length);
            Assert.Equal(0x02, contents[0]);
            Assert.Equal(0x02, contents[1]);
            Assert.Equal(0x02, contents[2]);
            contents = await fileManager.ReadAllBytes(@"C:\test\settings\subfolder\test1.ini");
            Assert.Equal(4, contents.Length);
            Assert.Equal(0x11, contents[0]);
            Assert.Equal(0x12, contents[1]);
            Assert.Equal(0x13, contents[2]);
            Assert.Equal(0x14, contents[3]);
        }

        [Fact]
        public async Task TestApplyMissing()
        {
            var setting = new Setting()
            {
                Name = "configuration",
                HandlerDescription = new FilesSettingHandlerDescription(@"C:\test\settings", new string[] {
                    @"test1.ini",
                    @"test2.ini",
                    @"subfolder\test1.ini"
                })
            };
            var logging = new LoggerFactory();
            var logger = logging.CreateLogger<FilesSettingHandler>();
            var fileManager = new MockFileManager();
            fileManager.CreateDirectory(@"C:\test\settings");
            await fileManager.WriteAllBytes(@"C:\test\settings\test1.ini", new byte[] { 0x00, 0x01, 0x02, 0x03 });
            var handler = new FilesSettingHandler(setting, fileManager, logger);

            var value = new FilesValue()
            {
                Files = new FileValue[]
                {
                    new FileValue
                    {
                        RelativePath = @"test1.ini",
                        Contents = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 }
                    },
                    new FileValue
                    {
                        RelativePath = @"test2.ini",
                        Contents = new byte[] { 0x02, 0x02, 0x02 }
                    },
                    new FileValue
                    {
                        RelativePath = @"subfolder\test1.ini",
                        Contents = new byte[] { 0x11, 0x12, 0x13, 0x14 }
                    }
                }
            }.ToDictionary();

            var success = await handler.Apply(value);
            Assert.True(success);
            var contents = await fileManager.ReadAllBytes(@"C:\test\settings\test1.ini");
            Assert.Equal(5, contents.Length);
            Assert.Equal(0x01, contents[0]);
            Assert.Equal(0x02, contents[1]);
            Assert.Equal(0x03, contents[2]);
            Assert.Equal(0x04, contents[3]);
            Assert.Equal(0x05, contents[4]);
            contents = await fileManager.ReadAllBytes(@"C:\test\settings\test2.ini");
            Assert.Equal(3, contents.Length);
            Assert.Equal(0x02, contents[0]);
            Assert.Equal(0x02, contents[1]);
            Assert.Equal(0x02, contents[2]);
            contents = await fileManager.ReadAllBytes(@"C:\test\settings\subfolder\test1.ini");
            Assert.Equal(4, contents.Length);
            Assert.Equal(0x11, contents[0]);
            Assert.Equal(0x12, contents[1]);
            Assert.Equal(0x13, contents[2]);
            Assert.Equal(0x14, contents[3]);
        }

        [Fact]
        public async Task TestApplyDelete()
        {
            var setting = new Setting()
            {
                Name = "configuration",
                HandlerDescription = new FilesSettingHandlerDescription(@"C:\test\settings", new string[] {
                    @"test1.ini",
                    @"test2.ini",
                    @"subfolder\test1.ini"
                })
            };
            var logging = new LoggerFactory();
            var logger = logging.CreateLogger<FilesSettingHandler>();
            var fileManager = new MockFileManager();
            fileManager.CreateDirectory(@"C:\test\settings");
            fileManager.CreateDirectory(@"C:\test\settings\subfolder");
            await fileManager.WriteAllBytes(@"C:\test\settings\test1.ini", new byte[] { 0x00, 0x01, 0x02, 0x03 });
            await fileManager.WriteAllBytes(@"C:\test\settings\test2.ini", new byte[] { 0x01, 0x01, 0x01, 0x01 });
            await fileManager.WriteAllBytes(@"C:\test\settings\subfolder\test1.ini", new byte[] { 0x10, 0x11, 0x12, 0x13 });
            var handler = new FilesSettingHandler(setting, fileManager, logger);

            var value = new FilesValue()
            {
                Files = new FileValue[]
                {
                    new FileValue
                    {
                        RelativePath = @"test2.ini",
                        Contents = new byte[] { 0x02, 0x02, 0x02 }
                    }
                }
            }.ToDictionary();

            var exists = fileManager.Exists(@"C:\test\settings\test1.ini");
            Assert.True(exists);
            exists = fileManager.Exists(@"C:\test\settings\subfolder\test1.ini");
            Assert.True(exists);
            var success = await handler.Apply(value);
            Assert.True(success);
            exists = fileManager.Exists(@"C:\test\settings\test1.ini");
            Assert.False(exists);
            var contents = await fileManager.ReadAllBytes(@"C:\test\settings\test2.ini");
            Assert.Equal(3, contents.Length);
            Assert.Equal(0x02, contents[0]);
            Assert.Equal(0x02, contents[1]);
            Assert.Equal(0x02, contents[2]);
            exists = fileManager.Exists(@"C:\test\settings\subfolder\test1.ini");
            Assert.False(exists);
        }

        [Fact]
        public async Task TestApplyWildcard()
        {
            var setting = new Setting()
            {
                Name = "configuration",
                HandlerDescription = new FilesSettingHandlerDescription(@"C:\test\settings", new string[] {
                    @"*.ini",
                    @"subfolder\*"
                })
            };
            var logging = new LoggerFactory();
            var logger = logging.CreateLogger<FilesSettingHandler>();
            var fileManager = new MockFileManager();
            fileManager.CreateDirectory(@"C:\test\settings");
            fileManager.CreateDirectory(@"C:\test\settings\subfolder");
            await fileManager.WriteAllBytes(@"C:\test\settings\test1.ini", new byte[] { 0x00, 0x01, 0x02, 0x03 });
            await fileManager.WriteAllBytes(@"C:\test\settings\test2.ini", new byte[] { 0x01, 0x01, 0x01, 0x01 });
            await fileManager.WriteAllBytes(@"C:\test\settings\subfolder\test1.ini", new byte[] { 0x10, 0x11, 0x12, 0x13 });
            var handler = new FilesSettingHandler(setting, fileManager, logger);

            var value = new FilesValue()
            {
                Files = new FileValue[]
                {
                    new FileValue
                    {
                        RelativePath = @"test2.ini",
                        Contents = new byte[] { 0x02, 0x02, 0x02 }
                    },
                    new FileValue
                    {
                        RelativePath = @"test3.ini",
                        Contents = new byte[] { 0x03, 0x03, 0x03 }
                    }
                }
            }.ToDictionary();

            var exists = fileManager.Exists(@"C:\test\settings\test1.ini");
            Assert.True(exists);
            exists = fileManager.Exists(@"C:\test\settings\subfolder\test1.ini");
            Assert.True(exists);
            var success = await handler.Apply(value);
            Assert.True(success);
            exists = fileManager.Exists(@"C:\test\settings\test1.ini");
            Assert.False(exists);
            var contents = await fileManager.ReadAllBytes(@"C:\test\settings\test2.ini");
            Assert.Equal(3, contents.Length);
            Assert.Equal(0x02, contents[0]);
            Assert.Equal(0x02, contents[1]);
            Assert.Equal(0x02, contents[2]);
            contents = await fileManager.ReadAllBytes(@"C:\test\settings\test3.ini");
            Assert.Equal(3, contents.Length);
            Assert.Equal(0x03, contents[0]);
            Assert.Equal(0x03, contents[1]);
            Assert.Equal(0x03, contents[2]);
            exists = fileManager.Exists(@"C:\test\settings\subfolder\test1.ini");
            Assert.False(exists);
        }
    }
}
