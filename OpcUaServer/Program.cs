using Opc.Ua;
using Opc.Ua.Configuration;
using Opc.Ua.Server;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            // Programatik olarak yapılandırma oluştur
            ApplicationConfiguration config = CreateApplicationConfiguration();

            ApplicationInstance application = new ApplicationInstance
            {
                ApplicationName = "OpcUaServer",
                ApplicationType = ApplicationType.Server,
                ApplicationConfiguration = config
            };

            // Sertifikaları kontrol et
            bool certOK = await application.CheckApplicationInstanceCertificate(false, 2048);
            if (!certOK)
            {
                Console.WriteLine("Sertifika oluşturuluyor...");
                certOK = await application.CheckApplicationInstanceCertificate(true, 2048);
                if (!certOK)
                {
                    throw new Exception("Sertifika oluşturulamadı!");
                }
            }

            // Sunucuyu başlat
            MyServer server = new MyServer();
            await application.Start(server);

            var nodeManager = server.CurrentInstance.NodeManager.NodeManagers
                .OfType<MyNodeManager>()
                .FirstOrDefault();
            if (nodeManager == null)
            {
                throw new Exception("❌ NodeManager bulunamadı! Server doğru başlatılmış mı?");
            }
           
            Console.ReadLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Hata: {ex.Message}");
            Console.WriteLine($"Detaylı hata: {ex}");
        }
    }

    private static ApplicationConfiguration CreateApplicationConfiguration()
    {
        // Sertifika dizini
        string certificateStorePath = Path.Combine(Directory.GetCurrentDirectory(), "CertificateStore");
        Directory.CreateDirectory(certificateStorePath);

        // Yapılandırmayı programatik olarak oluştur
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

            TransportConfigurations = new TransportConfigurationCollection(),

            TransportQuotas = new TransportQuotas
            {
                OperationTimeout = 120000,
                MaxStringLength = 1048576,
                MaxByteStringLength = 1048576,
                MaxArrayLength = 65535,
                MaxMessageSize = 4194304,
                MaxBufferSize = 65535,
                ChannelLifetime = 300000,
                SecurityTokenLifetime = 3600000
            },

            ServerConfiguration = new ServerConfiguration
            {
                BaseAddresses = new StringCollection
                {
                    "opc.tcp://0.0.0.0:4840/UA/OpcUaServer",
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
                MaxQueuedRequestCount = 2000
            },

            TraceConfiguration = new TraceConfiguration
            {
                OutputFilePath = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "OpcUaServer.txt"),
                DeleteOnLoad = true
            }
        };

        // Logs klasörünü oluştur
        Directory.CreateDirectory(Path.GetDirectoryName(config.TraceConfiguration.OutputFilePath));

        // Yapılandırmayı doğrula
        config.Validate(ApplicationType.Server);

        return config;
    }

}