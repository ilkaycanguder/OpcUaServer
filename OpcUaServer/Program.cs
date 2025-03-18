using Opc.Ua;
using Opc.Ua.Configuration;
using System;
using System.IO;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            ApplicationInstance application = new ApplicationInstance
            {
                ApplicationName = "OpcUaServer",
                ApplicationType = ApplicationType.Server,
                ApplicationConfiguration = await CreateApplicationConfiguration()
            };

            // Sertifikaları kontrol et ve gerekirse oluştur
            bool certOK = await application.CheckApplicationInstanceCertificate(false, 2048);
            if (!certOK)
            {
                Console.WriteLine("⏳ Sertifika oluşturuluyor...");
                certOK = await application.CheckApplicationInstanceCertificate(true, 2048);
                if (!certOK)
                {
                    throw new Exception("Sertifika oluşturulamadı!");
                }
                Console.WriteLine("✅ Sertifika başarıyla oluşturuldu.");
            }

            MyServer server = new MyServer();
            await application.Start(server);
            Console.WriteLine("✅ OPC UA Server Başlatıldı!");
            Console.WriteLine("Sunucuyu durdurmak için ENTER tuşuna basın...");
            Console.ReadLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Hata: {ex.Message}");
            Console.ReadLine(); // Konsol hemen kapanmasın diye
        }
    }

    private static async Task<ApplicationConfiguration> CreateApplicationConfiguration()
    {
        // Sertifika dizini
        string certificateStorePath = Path.Combine(Directory.GetCurrentDirectory(), "CertificateStore");
        Directory.CreateDirectory(certificateStorePath);

        var config = new ApplicationConfiguration
        {
            ApplicationName = "OpcUaServer",
            ApplicationUri = "urn:localhost:OpcUaServer",
            ProductUri = "uri:opcfoundation.org:OpcUaServer",
            ApplicationType = ApplicationType.Server,

            SecurityConfiguration = new SecurityConfiguration
            {
                ApplicationCertificate = new CertificateIdentifier
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = certificateStorePath,
                    SubjectName = "CN=OpcUaServer"
                },
                TrustedPeerCertificates = new CertificateTrustList
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = certificateStorePath
                },
                TrustedIssuerCertificates = new CertificateTrustList
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = certificateStorePath
                },
                RejectedCertificateStore = new CertificateTrustList
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = certificateStorePath
                },
                AutoAcceptUntrustedCertificates = true,
                RejectSHA1SignedCertificates = false,
                MinimumCertificateKeySize = 1024
            },

            TransportQuotas = new TransportQuotas
            {
                OperationTimeout = 120000,
                MaxStringLength = 1048576,
                MaxByteStringLength = 1048576,
                MaxArrayLength = 65535,
                MaxMessageSize = 4194304,
                MaxBufferSize = 65535
            },

            ServerConfiguration = new ServerConfiguration
            {
                BaseAddresses = new StringCollection
                {
                    "opc.tcp://localhost:4840/UA/OpcUaServer"
                },
                SecurityPolicies = new ServerSecurityPolicyCollection
                {
                    new ServerSecurityPolicy
                    {
                        SecurityMode = MessageSecurityMode.None,
                        SecurityPolicyUri = SecurityPolicies.None
                    },
                    new ServerSecurityPolicy
                    {
                        SecurityMode = MessageSecurityMode.SignAndEncrypt,
                        SecurityPolicyUri = SecurityPolicies.Basic256Sha256
                    }
                },
                MinRequestThreadCount = 5,
                MaxRequestThreadCount = 100,
                MaxSessionCount = 50
            },

            TraceConfiguration = new TraceConfiguration
            {
                OutputFilePath = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "OpcUaServer.log"),
                DeleteOnLoad = true
            }
        };

        // Logs klasörünü oluştur
        Directory.CreateDirectory(Path.GetDirectoryName(config.TraceConfiguration.OutputFilePath));

        // Yapılandırmayı doğrula
        await config.Validate(ApplicationType.Server);

        return config;
    }
}