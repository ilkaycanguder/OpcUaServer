using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace OPCCommonLibrary
{
    public static class GuidHelper
    {
        private static readonly string guidFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Guid.Config.xml");
        private static int currentClientIndex = 0;
        private static List<(string name, Guid guid)> availableClients = new List<(string name, Guid guid)>();

        // Başlangıçta XML dosyasını kontrol et ve istemci listesini yükle
        static GuidHelper()
        {
            if (!File.Exists(guidFilePath))
            {
                Console.WriteLine($"⚠️ Guid.Config.xml bulunamadı! Varsayılan dosya oluşturuluyor...");
                CreateDefaultGuidXml();
            }
            else
            {
                // XML yapısını kontrol et, eksikse düzelt
                FixXmlStructure();
            }

            // İstemci listesini yükle
            LoadClientsFromXml();
        }

        // XML dosyasından tüm istemcileri yükle
        private static void LoadClientsFromXml()
        {
            try
            {
                XDocument doc = XDocument.Load(guidFilePath);
                XElement clientsElement = doc.Element("GuidConfig").Element("Clients");

                availableClients.Clear();
                foreach (var clientElement in clientsElement.Elements("Client"))
                {
                    string name = clientElement.Element("Name").Value;
                    Guid guid = Guid.Parse(clientElement.Element("ClientId").Value);
                    availableClients.Add((name, guid));
                }

                Console.WriteLine($"✅ {availableClients.Count} istemci yapılandırması yüklendi.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ İstemci yapılandırması yüklenirken hata: {ex.Message}");
                // Hata durumunda varsayılan oluştur
                CreateDefaultGuidXml();
                LoadClientsFromXml();
            }
        }

        private static void CreateDefaultGuidXml()
        {
            string defaultXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<GuidConfig>
    <Clients>
        <Client>
            <Name>Client_1</Name>
            <ClientId>550e8400-e29b-41d4-a716-446655440000</ClientId>
            <IsUsed>false</IsUsed>
        </Client>
        <Client>
            <Name>Client_2</Name>
            <ClientId>550e8400-e29b-41d4-a716-446655440001</ClientId>
            <IsUsed>false</IsUsed>
        </Client>
    </Clients>
</GuidConfig>";
            File.WriteAllText(guidFilePath, defaultXml);
            Console.WriteLine($"✅ Varsayılan Guid.Config.xml oluşturuldu: {guidFilePath}");
        }

        private static void FixXmlStructure()
        {
            try
            {
                XDocument doc = XDocument.Load(guidFilePath);
                if (doc.Element("GuidConfig") == null || doc.Element("GuidConfig").Element("Clients") == null)
                {
                    Console.WriteLine($"⚠️ XML Yapısı Eksik! Otomatik olarak düzeltiliyor...");
                    CreateDefaultGuidXml();
                }
            }
            catch
            {
                Console.WriteLine($"⚠️ XML Dosyası Hatalı! Yeniden oluşturuluyor...");
                CreateDefaultGuidXml();
            }
        }

        /// <summary>
        /// İstemciye XML dosyasından sırayla GUID atar
        /// </summary>
        /// <returns>İstemci adı ve GUID'i içeren tuple</returns>
        public static (string clientName, Guid clientGuid) GetNextClient()
        {
            if (availableClients.Count == 0)
            {
                // İstemci listesi boşsa yeniden yükle
                LoadClientsFromXml();

                if (availableClients.Count == 0)
                {
                    throw new Exception("Kullanılabilir istemci yapılandırması bulunamadı!");
                }
            }

            // Sırayla istemci seç
            var nextClient = availableClients[currentClientIndex];

            // Bir sonraki index için güncelle (döngüsel olarak)
            currentClientIndex = (currentClientIndex + 1) % availableClients.Count;

            Console.WriteLine($"✅ İstemci atandı: {nextClient.name}, GUID: {nextClient.guid}");
            return nextClient;
        }

        /// <summary>
        /// GUID'e göre istemci adını döndürür
        /// </summary>
        public static string GetClientNameByGuid(Guid clientGuid)
        {
            var client = availableClients.FirstOrDefault(c => c.guid == clientGuid);
            return client.name ?? "Bilinmeyen İstemci";
        }

        /// <summary>
        /// İstemcinin atanmış GUID'ini döndürür, yoksa yeni bir istemci atar
        /// </summary>
        public static (string clientName, Guid clientGuid) GetOrCreateClient()
        {
            // Eski fonksiyonun yerine geçecek, ama artık sadece sırayla GUID atıyor
            return GetNextClient();
        }
        public static Guid GetClientGuidFromConfig()
        {
            string configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Guid.Config.xml");

            if (!File.Exists(configFilePath))
            {
                Console.WriteLine("⚠️ Guid.Config.xml bulunamadı! Varsayılan dosya oluşturuluyor...");
                CreateDefaultGuidXml();
            }

            XDocument doc = XDocument.Load(configFilePath);
            XElement clientsElement = doc.Element("GuidConfig").Element("Clients");

            var availableClient = clientsElement.Elements("Client")
                .Where(c => c.Element("IsUsed").Value == "false")
                .FirstOrDefault();

            if (availableClient == null)
            {
                Console.WriteLine("⚠️ Kullanılabilir istemci GUID bulunamadı! Yeni bir GUID oluşturuluyor...");
                Guid newGuid = Guid.NewGuid();
                XElement newClient = new XElement("Client",
                    new XElement("Name", $"Client_{clientsElement.Elements().Count()}"),
                    new XElement("ClientId", newGuid.ToString()),
                    new XElement("IsUsed", "false")
                );
                clientsElement.Add(newClient);
                doc.Save(configFilePath);
                return newGuid;
            }

            // GUID'i al ve kullanılabilirliği güncelle
            Guid clientGuid = Guid.Parse(availableClient.Element("ClientId").Value);
            availableClient.Element("IsUsed").Value = "true";

            try
            {
                doc.Save(configFilePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Hata: GUID kaydedilemedi: {ex.Message}");
            }

            return clientGuid;
        }


        /// <summary>
        /// Tüm tanımlı istemcileri listeler
        /// </summary>
        public static List<(string name, Guid guid)> ListAllClients()
        {
            if (availableClients.Count == 0)
            {
                LoadClientsFromXml();
            }

            return availableClients;
        }
    }
}