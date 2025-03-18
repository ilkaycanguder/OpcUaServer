using Opc.Ua;
using Opc.Ua.Server;
using System;
using System.Collections.Generic;
using System.Linq;

public class MyServer : StandardServer
{
    private readonly Dictionary<Guid, Session> activeSessions = new Dictionary<Guid, Session>();

    // **Statik GUID Listesi (Sabit 2 Client)**
    private static readonly Dictionary<Guid, string> predefinedClients = new Dictionary<Guid, string>
    {
        { new Guid("550e8400-e29b-41d4-a716-446655440000"), "Client_1" },
        { new Guid("550e8400-e29b-41d4-a716-446655440001"), "Client_2" }
    };

    protected override MasterNodeManager CreateMasterNodeManager(IServerInternal server, ApplicationConfiguration configuration)
    {
        List<INodeManager> nodeManagers = new List<INodeManager>();

        // **MyNodeManager'ı oluştur ve session map'i ile başlat**
        var nodeManager = new MyNodeManager(server, configuration, activeSessions);
        nodeManagers.Add(nodeManager);

        // **Ana NodeManager'ı oluştur**
        return new MasterNodeManager(server, configuration, null, nodeManagers.ToArray());
    }

    protected override void OnServerStarted(IServerInternal server)
    {
        base.OnServerStarted(server);

        // **Oturum Yönetimi**
        server.SessionManager.SessionCreated += OnSessionCreated;
        server.SessionManager.SessionClosing += OnSessionDeleted;
    }

    private void OnSessionCreated(Session session, SessionEventReason reason)
    {
        var availableClient = predefinedClients.FirstOrDefault(c => !activeSessions.ContainsKey(c.Key));
        if (availableClient.Key == Guid.Empty)
        {
            Console.WriteLine("❌ Maksimum istemci sayısına ulaşıldı! Yeni istemci atanamadı.");
            return;
        }
        activeSessions[availableClient.Key] = session;
        Console.WriteLine($"🟢 Yeni Client Bağlandı: {availableClient.Value} | GUID: {availableClient.Key} | Aktif Client Sayısı: {activeSessions.Count}");

        // **Client düğümünü UA Server'da göster**
        var nodeManager = CurrentInstance?.NodeManager?.NodeManagers?[0] as MyNodeManager;
        nodeManager?.RegisterClientNode(availableClient.Key);
    }

    private void OnSessionDeleted(Session session, SessionEventReason reason)
    {
        var clientEntry = activeSessions.FirstOrDefault(x => x.Value == session);
        if (clientEntry.Key != Guid.Empty)
        {
            activeSessions.Remove(clientEntry.Key);
            Console.WriteLine($"🔴 Client Bağlantısı Kapatıldı | Client: {predefinedClients[clientEntry.Key]} | GUID: {clientEntry.Key} | Aktif Client Sayısı: {activeSessions.Count}");

            // **Client düğümünü UA Server'dan kaldır**
            var nodeManager = CurrentInstance?.NodeManager?.NodeManagers?[0] as MyNodeManager;
            nodeManager?.RemoveClientNode(clientEntry.Key);
        }
    }
}
