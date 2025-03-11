using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;
using System;
using System.IO;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("OPC UA Client başlatılıyor...");

        // **Yapılandırma dosyasının yolu**
        string configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OpcUaClient.Config.xml");

        // **Config.xml Dosyasını Önce Oluştur**
        await EnsureConfigurationFileExists(configFilePath);

        // **OPC UA uygulama örneği**
        ApplicationInstance application = new ApplicationInstance
        {
            ApplicationName = "OpcUaClient",
            ApplicationType = ApplicationType.Client
        };

        try
        {
            // **XML Yapılandırmasını Yükle**
            ApplicationConfiguration config = await application.LoadApplicationConfiguration(configFilePath, silent: false);
            if (config == null)
            {
                throw new Exception("Yapılandırma dosyası yüklenemedi!");
            }

            // **ApplicationType'ı Elle Düzelt**
            if (config.ApplicationType != ApplicationType.Client)
            {
                Console.WriteLine($"ApplicationType hatalı: {config.ApplicationType}, Client olarak güncellendi.");
                config.ApplicationType = ApplicationType.Client;
            }

            // **Sertifikayı kontrol et veya oluştur**
            bool certOK = await application.CheckApplicationInstanceCertificates(false, 2048);
            if (!certOK)
            {
                throw new Exception("İstemci sertifikası oluşturulamadı veya geçersiz!");
            }

            application.ApplicationConfiguration = config;

            // **OPC UA Sunucusuna Bağlan**
            string serverUrl = "opc.tcp://localhost:4840/UA/OpcUaServer";
            var endpoint = CoreClientUtils.SelectEndpoint(serverUrl, false, 15000);
            var configEndpoint = new ConfiguredEndpoint(null, endpoint, EndpointConfiguration.Create(config));
            Session session = await Session.Create(config, configEndpoint, false, "ChatSession", 60000, null, null);

            Console.WriteLine("OPC UA sunucusuna bağlandı!");
            Subscription subscription = new Subscription(session.DefaultSubscription) { PublishingInterval = 1000 };
            session.AddSubscription(subscription);
            subscription.Create();

            MonitoredItem monitoredItem = new MonitoredItem(subscription.DefaultItem)
            {
                DisplayName = "ServerMessageMonitor",
                StartNodeId = "ns=2;s=MessageFromServer",
                AttributeId = Attributes.Value
            };
            monitoredItem.Notification += (item, args) =>
            {
                foreach (var value in item.DequeueValues())
                    Console.WriteLine("Sunucudan Gelen Mesaj: " + value.Value?.ToString());
            };

            subscription.AddItem(monitoredItem);
            subscription.ApplyChanges();

            Console.WriteLine("Sunucu mesajları dinleniyor...");
            Console.ReadLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Hata: {ex.Message}");
        }
    }

    // **Config.xml Dosyasının Varlığını Kontrol Edip, Yoksa Oluşturan Metot**
    static async Task EnsureConfigurationFileExists(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Console.WriteLine("İstemci yapılandırma dosyası bulunamadı, oluşturuluyor...");
            try
            {
                string xmlContent = GetClientConfigXml();
                await File.WriteAllTextAsync(filePath, xmlContent);
                Console.WriteLine("Yapılandırma dosyası başarıyla oluşturuldu!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Yapılandırma dosyası oluşturulamadı: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine("Yapılandırma dosyası zaten mevcut.");
        }
    }

    // **OPC UA Client XML Yapılandırması (Hatasız)**
    static string GetClientConfigXml()
    {
        return @"<?xml version=""1.0"" encoding=""utf-8""?>
        <ApplicationConfiguration xmlns=""http://opcfoundation.org/UA/SDK/Configuration.xsd"">
            <ApplicationName>OpcUaClient</ApplicationName>
            <ApplicationUri>urn:localhost:OpcUaClient</ApplicationUri>
            <ApplicationType>Client_1</ApplicationType>
            <SecurityConfiguration>
                <ApplicationCertificate>
                    <StoreType>Directory</StoreType>
                    <StorePath>C:/ProgramData/OPC Foundation/CertificateStores/MachineDefault</StorePath>
                    <SubjectName>CN=OpcUaClient</SubjectName>
                </ApplicationCertificate>
                <AutoAcceptUntrustedCertificates>true</AutoAcceptUntrustedCertificates>
            </SecurityConfiguration>
            <TransportQuotas>
                <OperationTimeout>15000</OperationTimeout>
            </TransportQuotas>
            <ClientConfiguration>
                <DefaultSessionTimeout>60000</DefaultSessionTimeout>
                <MinSubscriptionLifetime>5000</MinSubscriptionLifetime>
            </ClientConfiguration>
        </ApplicationConfiguration>";
    }
}
