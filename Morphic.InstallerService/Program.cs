using Morphic.InstallerService;
using IoDCLI;
using JKang.IpcServiceFramework.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Morphic.InstallerService.Contracts;
using Microsoft.AspNetCore.Hosting;
using System.Reflection;
using System.Net;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Text;
using System;
using System.Security;
using System.Linq;

//sc create "Moprhic Installer Service" binPath="C:\Users\codan\Downloads\IoDCLI\InstallerService\bin\Debug\net5.0-windows10.0.17763\InstallerService.exe C:\Users\codan\Downloads\IoDCLI\InstallerService\bin\Debug\net5.0-windows10.0.17763"

WindowsIdentityHelper.RegDisablePredefinedCacheEx();

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddHostedService<Worker>();
        services.AddScoped<IInstallerService, InstallerIpcService>();
        services.AddLogging(configure => configure.AddConsole());
        services.AddLogging(configure => configure.AddEventLog());
        services.AddTransient<PackageManagerService>();
        services.AddGrpc();
    })
    .ConfigureIpcHost(builder =>
    {
        builder.AddNamedPipeEndpoint<IInstallerService>("moprhicinstaller");
    })
    .ConfigureLogging(builder =>
    {
        builder.SetMinimumLevel(LogLevel.Debug);
    })
    .UseWindowsService()
    .ConfigureWebHostDefaults(webBuilder =>
    {
        webBuilder.UseStartup<Startup>().ConfigureKestrel((context, options) =>
        {
            options.Listen(IPAddress.Loopback, 5001, listenOptions =>
            {
                var assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

                if (assemblyLocation != null)
                {
                    var certificatePath = Path.Combine(assemblyLocation, "certificate.pfx");

                    if(File.Exists(certificatePath))
                    {
                        File.Delete(certificatePath);
                    }

                    var uniqueKey = KeyTools.GetUniqueKey(13);

                    var commonName = "localhost";

                    var subjectName = new CertificateDistinguishedName
                    {
                        CommonName = commonName
                    };

                    var certificate = new SelfSignedCertificate
                    {
                        SubjectName = subjectName,
                        FriendlyName = "Morphic AToD",
                        EnhancedKeyUsages = new[] { EnhancedKeyUsage.ServerAuthentication }
                    };

                    var cert2 = certificate.AsX509Certificate2();
                    var bytes = cert2.Export(X509ContentType.Pfx, uniqueKey);

                    try
                    {
                        File.WriteAllBytes(certificatePath, bytes);
                    }
                    finally
                    {
                        Array.Clear(bytes, 0, bytes.Length);
                    }

                    var store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
                    try
                    {
                        store.Open(OpenFlags.ReadWrite);

                        var oldCert = store.Certificates
                            .OfType<X509Certificate2>()
                            .FirstOrDefault(x => x.FriendlyName == "Morphic AToD");

                        if(oldCert != null)
                        {
                            store.Remove(oldCert);
                        }

                        store.Add(cert2);
                    }
                    finally
                    {
                        store.Close();
                    }

                    listenOptions.UseHttps(certificatePath, uniqueKey);
                }
            });
        });
    })
    .Build();

await host.RunAsync();

public static class KeyTools
{
    internal static readonly char[] chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890".ToCharArray();

    public static SecureString GetSecureUniqueKey(int size)
    {
        byte[] data = new byte[4 * size];
        using (var crypto = RandomNumberGenerator.Create())
        {
            crypto.GetBytes(data);
        }

        SecureString secureString = new SecureString();
        for (int i = 0; i < size; i++)
        {
            var rnd = BitConverter.ToUInt32(data, i * 4);
            var idx = rnd % chars.Length;

            secureString.AppendChar(chars[idx]);
        }

        secureString.MakeReadOnly();

        return secureString;
    }

    public static string GetUniqueKey(int size)
    {
        byte[] data = new byte[4 * size];
        using (var crypto = RandomNumberGenerator.Create())
        {
            crypto.GetBytes(data);
        }

        var sb = new StringBuilder();
        for (int i = 0; i < size; i++)
        {
            var rnd = BitConverter.ToUInt32(data, i * 4);
            var idx = rnd % chars.Length;

            sb.Append(chars[idx]);
        }

        return sb.ToString();
    }
}

public enum EnhancedKeyUsage
{
    ServerAuthentication,
    ClientAuthentication
}

class CertificateDistinguishedName
{
    public string? CommonName { get; set; }
    public string? Country { get; set; }
    public string? StateOrProvince { get; set; }
    public string? Locality { get; set; }
    public string? Organization { get; set; }
    public string? OrganizationUnit { get; set; }
    public string? EmailAddress { get; set; }

    public string Format()
    {
        return Format(';', true);
    }

    public string Format(char separator, bool useQuotes)
    {
        var sb = new StringBuilder();

        if(useQuotes)
        {
            sb.Append($"CN=\"{CommonName}\"");

            if (OrganizationUnit != null)
            {
                sb.Append(separator);
                sb.Append(" ");

                sb.Append($"OU=\"{OrganizationUnit}\"");
            }

            if (Organization != null)
            {
                sb.Append(separator);
                sb.Append(" ");

                sb.Append($"O=\"{Organization}\"");
            }

            if (Locality != null)
            {
                sb.Append(separator);
                sb.Append(" ");

                sb.Append($"L=\"{Locality}\"");
            }

            if (StateOrProvince != null)
            {
                sb.Append(separator);
                sb.Append(" ");

                sb.Append($"S=\"{StateOrProvince}\"");
            }

            if (Country != null)
            {
                sb.Append(separator);
                sb.Append(" ");

                sb.Append($"C=\"{Country}\"");
            }

            if (EmailAddress != null)
            {
                sb.Append(separator);
                sb.Append(" ");

                sb.Append($"E=\"{EmailAddress}\"");
            }
        }
        else
        {
            sb.Append($"CN={CommonName}");

            if (OrganizationUnit != null)
            {
                sb.Append(separator);
                sb.Append(" ");

                sb.Append($"OU={OrganizationUnit}");
            }

            if (Organization != null)
            {
                sb.Append(separator);
                sb.Append(" ");

                sb.Append($"O={Organization}");
            }

            if (Locality != null)
            {
                sb.Append(separator);
                sb.Append(" ");

                sb.Append($"L={Locality}");
            }

            if (StateOrProvince != null)
            {
                sb.Append(separator);
                sb.Append(" ");

                sb.Append($"S={StateOrProvince}");
            }

            if (Country != null)
            {
                sb.Append(separator);
                sb.Append(" ");

                sb.Append($"C={Country}");
            }

            if (EmailAddress != null)
            {
                sb.Append(separator);
                sb.Append(" ");

                sb.Append($"E={EmailAddress}");
            }
        }

        return sb.ToString();
    }

    public override string ToString()
    {
        return Format(',', false);
    }

    public X500DistinguishedName AsX500DistinguishedName()
    {
        return new X500DistinguishedName(Format());
    }
}

class SelfSignedCertificate
{
    public string FriendlyName { get; set; } = string.Empty;
    public CertificateDistinguishedName SubjectName { get; set; }

    public bool ForCertificateAuthority { get; set; }
    public EnhancedKeyUsage[] EnhancedKeyUsages { get; set; }

    Dictionary<EnhancedKeyUsage, Oid> supportedUsages;

    public SelfSignedCertificate()
    {
        supportedUsages = new Dictionary<EnhancedKeyUsage, Oid> {
            { EnhancedKeyUsage.ServerAuthentication, new Oid("1.3.6.1.5.5.7.3.1", "Server Authentication") },
            { EnhancedKeyUsage.ClientAuthentication, new Oid("1.3.6.1.5.5.7.3.2", "Client Authentication") }
        };
    }

    public X509Certificate2 AsX509Certificate2()
    {
        var keyLength = 2048;
        var certificateDuration = 365;

        var extensions = new List<X509Extension>();

        if(EnhancedKeyUsages != null)
        {
            var oidCollection = new OidCollection();
            foreach(var eku in EnhancedKeyUsages)
            {
                if(supportedUsages.ContainsKey(eku))
                    oidCollection.Add(supportedUsages[eku]);
            }

            var enhancedKeyUsages = new X509EnhancedKeyUsageExtension(oidCollection, false);

            extensions.Add(enhancedKeyUsages);
        }

        var basicConstraints = new X509BasicConstraintsExtension(ForCertificateAuthority, false, 0, false);
        extensions.Add(basicConstraints);

        var key = RSA.Create(keyLength);

        var subject = SubjectName.AsX500DistinguishedName();

        var certRequest = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        var subjectKeyIdentifier = new X509SubjectKeyIdentifierExtension(certRequest.PublicKey, false);
        extensions.Add(subjectKeyIdentifier);

        //if(ForCertificateAuthority)
        //{
        //    var authorityKey = 
        //}


        foreach(var extension in extensions)
        {
            certRequest.CertificateExtensions.Add(extension);
        }

        var cert = certRequest.CreateSelfSigned(DateTime.Now, DateTime.Now.AddDays(certificateDuration));
        cert.FriendlyName = FriendlyName;

        return cert;
    }
}