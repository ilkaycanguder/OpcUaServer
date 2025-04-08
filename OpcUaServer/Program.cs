using Opc.Ua;
using Opc.Ua.Configuration;
using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            // Çalışma dizinini yazdır
            Console.WriteLine($"✓ Çalışma Dizini: {Directory.GetCurrentDirectory()}");

            // Kesin yollarla sertifika dosyası tanımlama
            string projectDir = AppDomain.CurrentDomain.BaseDirectory; // EXE dosyasının bulunduğu dizin
            string certDir = Path.Combine(projectDir, "Certificates");

            // Sertifika dizinini oluştur (yoksa)
            if (!Directory.Exists(certDir))
            {
                Directory.CreateDirectory(certDir);
                Console.WriteLine($"✓ Sertifika dizini oluşturuldu: {certDir}");
            }

            // PFX dosyası için kesin yol tanımla
            string customCertificatePath = Path.Combine(certDir, "your_certificate.pfx");
            Console.WriteLine($"✓ Sertifika Aranacak Yol: {customCertificatePath}");

            // Dosya var mı kontrol et
            bool certExists = File.Exists(customCertificatePath);
            Console.WriteLine($"✓ Sertifika Dosyası Mevcut: {certExists}");

            // Eğer dosya yoksa, PFX dosyasını dosya sisteminden kopyala
            if (!certExists)
            {
                // Not: Buraya sertifika dosyanızın tam yolunu yazın
                string sourcePfxFile = @"C:\Users\ILKAY\Desktop\OPC-UA-Server\OpcUaSolution\OpcUaServer\Certificates\your_certificate.pfx";
                if (File.Exists(sourcePfxFile))
                {
                    File.Copy(sourcePfxFile, customCertificatePath, true);
                    Console.WriteLine($"✓ Sertifika dosyası kopyalandı: {sourcePfxFile} -> {customCertificatePath}");
                    certExists = true;
                }
                else
                {
                    Console.WriteLine($"❌ Kaynak sertifika dosyası bulunamadı: {sourcePfxFile}");
                }
            }

            // Öncelikle uygulamayı başlat
            ApplicationInstance application = new ApplicationInstance
            {
                ApplicationName = "OpcUaServer",
                ApplicationType = ApplicationType.Server,
            };

            // Yapılandırmayı oluştur
            ApplicationConfiguration config = await CreateApplicationConfiguration();

            // Yapılandırmayı ayarla
            application.ApplicationConfiguration = config;

            // Sertifika doğrulamasını yapılandır
            config.SecurityConfiguration.AutoAcceptUntrustedCertificates = true;
            config.SecurityConfiguration.RejectSHA1SignedCertificates = false;

            // Daha detaylı sertifika doğrulama ayarları - hatalı satırları kaldırdım
            //config.SecurityConfiguration.AddTrustedPeer = true;
            //config.SecurityConfiguration.AddTrustedIssuer = true;
            config.SecurityConfiguration.RejectUnknownRevocationStatus = false;
            config.SecurityConfiguration.MinimumCertificateKeySize = 1024; // Minimum anahtar boyutunu azalt

            // Bu olay dinleyicisi tüm sertifikaları kabul etmeyi sağlar
            config.CertificateValidator.CertificateValidation += (sender, e) =>
            {
                e.Accept = true;
                Console.WriteLine($"✓ İstemci sertifikası kabul edildi: {e.Certificate.Subject}");
            };

            // Özel sertifikayı kullanma
            if (certExists)
            {
                Console.WriteLine("🔐 Özel SSL sertifikası kullanılıyor...");
                try
                {
                    // Sertifika şifresi - OpenSSL ile PFX oluştururken girdiğiniz şifre
                    string certificatePassword = "123456";

                    // Sertifikayı yükle
                    X509Certificate2 applicationCertificate = new X509Certificate2(
                        customCertificatePath,
                        certificatePassword,
                        X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable
                    );

                    // Sertifika hakkında bilgileri yazdır
                    Console.WriteLine($"✓ Sertifika Yüklendi: {applicationCertificate.Subject}");
                    Console.WriteLine($"✓ Sertifika Geçerlilik: {applicationCertificate.NotBefore} - {applicationCertificate.NotAfter}");

                    // ÖNEMLİ: Sertifikayı OPC UA uygulamasına doğru şekilde tanımla
                    config.SecurityConfiguration.ApplicationCertificate.Certificate = applicationCertificate;

                    // ApplicationUri'yi sertifikadan al
                    string subjectName = applicationCertificate.Subject;
                    string commonName = subjectName.Contains("CN=")
                        ? subjectName.Split(new[] { "CN=" }, StringSplitOptions.None)[1].Split(',')[0].Trim()
                        : "localhost";

                    // ApplicationUri'yi manuel olarak ayarla
                    config.ApplicationUri = $"urn:{commonName}:OpcUaServer";
                    Console.WriteLine($"✓ Uygulama URI'si güncellendi: {config.ApplicationUri}");

                    // Alternatif Yöntem: Sertifikayı OPC UA Store'a kaydet
                    // Bu, sertifikanın OPC UA tarafından bulunmasını sağlar
                    string certStorePath = config.SecurityConfiguration.ApplicationCertificate.StorePath;
                    Directory.CreateDirectory(certStorePath);
                    string certFile = Path.Combine(certStorePath, "cert.der");
                    File.WriteAllBytes(certFile, applicationCertificate.RawData);
                    Console.WriteLine($"✓ Sertifika OPC UA sertifika deposuna kopyalandı: {certFile}");

                    // Özel sertifikayı DER formatında da dışa aktar (UaExpert için)
                    string derFile = Path.Combine(certDir, "server_cert.der");
                    File.WriteAllBytes(derFile, applicationCertificate.RawData);
                    Console.WriteLine($"✓ Özel sertifika DER formatında dışa aktarıldı: {derFile}");

                    Console.WriteLine("✅ Özel SSL sertifikası başarıyla yüklendi.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Özel sertifika yüklenirken hata oluştu: {ex.Message}");
                    Console.WriteLine("⚠️ Otomatik oluşturulan sertifika kullanılacak.");
                    certExists = false;
                }
            }

            // Özel sertifika kullanılmıyorsa otomatik olarak oluştur
            if (!certExists)
            {
                Console.WriteLine("🔐 Otomatik sertifika kontrolü yapılıyor...");
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

                    // Oluşturulan sertifikayı DER formatında dışa aktar (UaExpert için)
                    var autoGenCert = config.SecurityConfiguration.ApplicationCertificate.Certificate;
                    if (autoGenCert != null)
                    {
                        string derFile = Path.Combine(certDir, "server_cert.der");
                        File.WriteAllBytes(derFile, autoGenCert.RawData);
                        Console.WriteLine($"✅ Otomatik oluşturulan sertifika DER formatında dışa aktarıldı: {derFile}");
                    }
                }
            }

            // Önemli: Yapılandırmayı doğrula
            await config.Validate(ApplicationType.Server);

            // Sunucuyu başlat
            Console.WriteLine("✓ Sunucu başlatılıyor...");
            try
            {
                MyServer server = new MyServer();
                await application.Start(server);
                Console.WriteLine("✅ OPC UA Server Başlatıldı!");
                Console.WriteLine("✅ Endpoint URLs:");
                foreach (var endpoint in config.ServerConfiguration.BaseAddresses)
                {
                    Console.WriteLine($"  - {endpoint}");
                }
                Console.WriteLine("Sunucuyu durdurmak için ENTER tuşuna basın...");
                Console.ReadLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Sunucu başlatılırken hata oluştu: {ex.Message}");
                Console.WriteLine($"❌ Detaylı hata: {ex}");

                // Son çare: Otomatik sertifika oluşturma ve sunucuyu başlatma
                Console.WriteLine("🔄 Otomatik sertifika oluşturma deneniyor...");

                // Yeni bir uygulama örneği oluştur
                ApplicationInstance alternativeApp = new ApplicationInstance
                {
                    ApplicationName = "OpcUaServer",
                    ApplicationType = ApplicationType.Server,
                    ApplicationConfiguration = config
                };

                await alternativeApp.CheckApplicationInstanceCertificate(true, 2048);
                MyServer newServer = new MyServer();
                await alternativeApp.Start(newServer);
                Console.WriteLine("✅ OPC UA Server alternatif yöntemle başlatıldı!");
                Console.ReadLine();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Hata: {ex.Message}");
            Console.WriteLine($"❌ Stack Trace: {ex.StackTrace}");
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
                    "opc.tcp://localhost:4840/UA/OpcUaServer",
                    // HTTPS bağlantısı için ek adres eklenebilir
                    "https://localhost:4843/UA/OpcUaServer"
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
                    },
                    new ServerSecurityPolicy
                    {
                        SecurityMode = MessageSecurityMode.SignAndEncrypt,
                        SecurityPolicyUri = SecurityPolicies.Aes256_Sha256_RsaPss
                    }
                },
                MinRequestThreadCount = 5,
                MaxRequestThreadCount = 100,
                MaxSessionCount = 50,

                UserTokenPolicies = new UserTokenPolicyCollection
                {
                    new UserTokenPolicy(UserTokenType.Anonymous),
                    new UserTokenPolicy(UserTokenType.Certificate),
                    new UserTokenPolicy(UserTokenType.UserName)
                }
            },

            TraceConfiguration = new TraceConfiguration
            {
                OutputFilePath = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "OpcUaServer.log"),
                DeleteOnLoad = true
            }
        };

        // Logs klasörünü oluştur
        Directory.CreateDirectory(Path.GetDirectoryName(config.TraceConfiguration.OutputFilePath));

        return config;
    }
}