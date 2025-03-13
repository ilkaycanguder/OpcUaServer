using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Collections.Generic;
using Npgsql;

namespace OPCCommonLibrary
{
    public static class GuidHelper
    {
        private static readonly string guidFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Guid.Config.xml");
        private static int currentClientIndex = 0;
        private static List<(string name, Guid guid, bool isUsed)> availableClients = new List<(string name, Guid guid, bool isUsed)>();

        // **Başlangıçta XML dosyasını kontrol et ve istemci listesini yükle**
        static GuidHelper()
        {
            if (!File.Exists(guidFilePath))
            {
                Console.WriteLine($"⚠️ Guid.Config.xml bulunamadı! Varsayılan dosya oluşturuluyor...");
                CreateDefaultGuidXml();
            }
            else
            {
                FixXmlStructure();
            }

            LoadClientsFromXml();
        }

        // **XML'den tüm istemcileri yükle**
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
                    bool isUsed = bool.Parse(clientElement.Element("IsUsed").Value.ToLower());
                    availableClients.Add((name, guid, isUsed));
                }

                Console.WriteLine($"✅ {availableClients.Count} istemci yapılandırması yüklendi.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ İstemci yapılandırması yüklenirken hata: {ex.Message}");
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

        /// **✅ Zaten açık olan istemcileri kontrol et ve tekrar bağlanmasını önle**
        public static (string clientName, Guid clientGuid) GetOrCreateClient()
        {
            XDocument doc = XDocument.Load(guidFilePath);
            XElement clientsElement = doc.Element("GuidConfig").Element("Clients");

            // 1️⃣ Kullanılabilir istemcileri al (`IsUsed = false`)
            XElement availableClient = clientsElement.Elements("Client")
                .FirstOrDefault(c => c.Element("IsUsed").Value == "false");

            if (availableClient == null)
            {
                throw new Exception("❌ Yeni istemci oluşturulamaz! Maksimum iki istemci kullanılabilir.");
            }

            // 2️⃣ PostgreSQL’de istemci kayıtlı mı kontrol et
            string clientGuidText = availableClient.Element("ClientId").Value;
            if (!IsClientRegisteredInDatabase(clientGuidText))
            {
                throw new Exception($"❌ İstemci veritabanında kayıtlı değil! GUID: {clientGuidText}");
            }

            // 3️⃣ GUID'i al ve kullanılabilirliği güncelle
            Guid clientGuid = Guid.Parse(clientGuidText);
            availableClient.Element("IsUsed").Value = "true";
            doc.Save(guidFilePath);

            Console.WriteLine($"✅ İstemci atandı: {availableClient.Element("Name").Value}, GUID: {clientGuid}");
            return (availableClient.Element("Name").Value, clientGuid);
        }
        private static bool IsClientRegisteredInDatabase(string clientGuid)
        {
            using (var connection = new NpgsqlConnection(DatabaseHelper.connectionString))
            {
                connection.Open();
                string query = "SELECT COUNT(*) FROM \"TESASch\".\"clientyetkilendirme\" WHERE clientguid = @ClientGuid";

                using (var cmd = new NpgsqlCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@ClientGuid", clientGuid);
                    int count = Convert.ToInt32(cmd.ExecuteScalar());

                    return count > 0;
                }
            }
        }


        /// **✅ Client listesinde sıradaki boş istemciyi döndür**
        public static (string clientName, Guid clientGuid) GetNextClient()
        {
            if (availableClients.Count == 0)
            {
                LoadClientsFromXml();
                if (availableClients.Count == 0)
                {
                    throw new Exception("Kullanılabilir istemci yapılandırması bulunamadı!");
                }
            }

            var nextClient = availableClients.FirstOrDefault(c => !c.isUsed);
            if (nextClient == default)
            {
                throw new Exception("⚠️ Kullanılabilir istemci bulunamadı!");
            }

            Console.WriteLine($"✅ İstemci atandı: {nextClient.name}, GUID: {nextClient.guid}");
            return (nextClient.name, nextClient.guid);
        }

        /// **✅ İstemcinin kullanım durumunu güncelle**
        public static void UpdateClientUsage(Guid clientGuid, bool isUsed)
        {
            XDocument doc = XDocument.Load(guidFilePath);
            XElement clientsElement = doc.Element("GuidConfig").Element("Clients");

            var client = clientsElement.Elements("Client")
                .FirstOrDefault(c => c.Element("ClientId").Value == clientGuid.ToString());

            if (client != null)
            {
                client.Element("IsUsed").Value = isUsed ? "true" : "false";
                doc.Save(guidFilePath);
                Console.WriteLine($"✅ İstemci durumu güncellendi: {clientGuid} -> IsUsed = {isUsed}");
            }
        }

        /// **✅ İstemcinin atanmış GUID'ini döndürür, yoksa yeni bir istemci atar**
        public static Guid GetClientGuidFromConfig()
        {
            XDocument doc = XDocument.Load(guidFilePath);
            XElement clientsElement = doc.Element("GuidConfig").Element("Clients");

            var availableClient = clientsElement.Elements("Client")
                .FirstOrDefault(c => c.Element("IsUsed").Value == "false");

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
                doc.Save(guidFilePath);
                return newGuid;
            }

            // **GUID'i al ve kullanılabilirliği güncelle**
            Guid clientGuid = Guid.Parse(availableClient.Element("ClientId").Value);
            UpdateClientUsage(clientGuid, true);

            return clientGuid;
        }

        /// **✅ Bağlı istemci GUID'ini döndür**
        public static string GetClientNameByGuid(Guid clientGuid)
        {
            var client = availableClients.FirstOrDefault(c => c.guid == clientGuid);
            return client.name ?? "Bilinmeyen İstemci";
        }

        /// **✅ Tüm tanımlı istemcileri listeler**
        public static List<(string name, Guid guid)> ListAllClients()
        {
            if (availableClients.Count == 0)
            {
                LoadClientsFromXml();
            }
            return availableClients.Select(c => (c.name, c.guid)).ToList();
        }
    }
}
