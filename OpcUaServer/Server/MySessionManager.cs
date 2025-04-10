using Opc.Ua;
using Opc.Ua.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OpcUaServer.Server
{
    public class MySessionManager : SessionManager
    {
        // **Statik olarak kullanılacak istemci listesi**
        private static readonly Dictionary<Guid, string> _staticClients = new Dictionary<Guid, string>
        {
            { Guid.Parse("550e8400-e29b-41d4-a716-446655440000"), "Client_1" },
            { Guid.Parse("550e8400-e29b-41d4-a716-446655440001"), "Client_2" }
        };

        // **Aktif istemcileri ve onların oturumlarını takip etmek için sözlük**
        private static readonly Dictionary<Guid, Session> _activeSessionMap = new Dictionary<Guid, Session>();

        // **Oturum kimlikleri ve GUID'ler arasında eşleme yapmak için**
        private static readonly Dictionary<NodeId, Guid> _sessionIdToGuidMap = new Dictionary<NodeId, Guid>();

        private readonly MyNodeManager _nodeManager;

        public MySessionManager(IServerInternal server, ApplicationConfiguration configuration, MyNodeManager nodeManager)
            : base(server, configuration)
        {
            _nodeManager = nodeManager;
        }

        public void OnSessionCreatedAsync(Session session, IServerInternal server)
        {
            try
            {
                // **Boşta olan bir istemci GUID bul**
                var availableClient = _staticClients.FirstOrDefault(c => !_activeSessionMap.ContainsKey(c.Key));
                if (availableClient.Key == Guid.Empty)
                {
                    Console.WriteLine("❌ Yeni istemci oluşturulamaz! Maksimum iki istemci kullanılabilir.");
                    return;
                }

                Guid clientGuid = availableClient.Key;
                string clientName = availableClient.Value;

                // **İstemciyi aktif olarak işaretle ve oturumu kaydet**
                _activeSessionMap[clientGuid] = session;
                _sessionIdToGuidMap[session.SessionDiagnostics.SessionId] = clientGuid;

                Console.WriteLine($"✅ Yeni Client Bağlandı: {clientName} | GUID: {clientGuid} | Session ID: {session.Id}");

                // **OPC UA Sunucusuna istemci düğümünü ekle**
                // Update the method call to match the correct signature
                _nodeManager?.RegisterClientNode(clientGuid.ToString());
                //_nodeManager?.RegisterClientNode(session.SessionDiagnostics.SessionId, clientGuid);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Hata: Session oluşturulurken GUID atanamadı! {ex.Message}");
            }
        }

        public void OnSessionDeletedAsync(Session session, object reason)
        {
            try
            {
                // **Oturumun GUID'sini bul**
                if (!_sessionIdToGuidMap.TryGetValue(session.SessionDiagnostics.SessionId, out Guid clientGuid))
                {
                    Console.WriteLine($"⚠️ Bağlantı kapatma hatası: Bu client zaten bağlı değil.");
                    return;
                }

                // **Oturumu ve eşlemeleri sil**
                _activeSessionMap.Remove(clientGuid);
                _sessionIdToGuidMap.Remove(session.SessionDiagnostics.SessionId);

                Console.WriteLine($"🔴 Client Bağlantısı Kapatıldı | Client: {_staticClients[clientGuid]} | GUID: {clientGuid} | Sebep: {reason}");

                // **OPC UA Sunucusundan istemci düğümünü kaldır**
                _nodeManager?.RemoveClientNode(clientGuid);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Hata: Session silinirken GUID bulunamadı! {ex.Message}");
            }
        }

        // **Aktif istemcilerin sayısını döndür**
        public int GetActiveClientCount()
        {
            return _activeSessionMap.Count;
        }

        // **Belirli bir GUID için oturumu döndür**
        public Session GetSessionByGuid(Guid clientGuid)
        {
            _activeSessionMap.TryGetValue(clientGuid, out Session session);
            return session;
        }
    }
}