using Opc.Ua;
using Opc.Ua.Server;
using OpcUaServer;
using System;
using System.Collections.Generic;

public class MyServer : StandardServer
{
    private readonly Dictionary<Guid, Session> _activeSessions = new Dictionary<Guid, Session>();
    private MySessionManager _sessionManager;

    protected override MasterNodeManager CreateMasterNodeManager(IServerInternal server, ApplicationConfiguration configuration)
    {
        return new MasterNodeManager(server, configuration, null, new MyNodeManager(server, configuration));
    }

    protected override void OnServerStarted(IServerInternal server)
    {
        base.OnServerStarted(server);
        _sessionManager = new MySessionManager(this, server, Configuration);
        server.SessionManager.SessionCreated += (session, reason) => _sessionManager.OnSessionCreatedAsync(session, server);
        server.SessionManager.SessionClosing += (session, reason) => _sessionManager.OnSessionDeletedAsync(session, reason);
    }
    public Guid GetClientId(Session session)
    {
        return _activeSessions.FirstOrDefault(x => x.Value == session).Key;
    }

    public async Task AddSessionAsync(Session session, Guid clientId)
    {
        _activeSessions[clientId] = session;
        Console.WriteLine($"🟢 Yeni Client Bağlandı: {clientId}, Aktif Client Sayısı: {_activeSessions.Count}");

        try
        {
            var nodeManager = CurrentInstance?.NodeManager?.NodeManagers?[0] as MyNodeManager;
            if (nodeManager != null)
            {
                await nodeManager.RegisterClientNode(session.SessionDiagnostics.SessionId, clientId); // clientGuid yerine clientId
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Node oluşturma hatası: {ex.Message}");
        }
    }



    public void RemoveSession(Session session)
    {
        var clientId = _activeSessions.FirstOrDefault(x => x.Value == session).Key;
        _activeSessions.Remove(clientId);

        Console.WriteLine($"🔴 Client Bağlantısı Kapatıldı | Client ID: {clientId} | Aktif Client Sayısı: {_activeSessions.Count}");

        try
        {
            var nodeManager = CurrentInstance?.NodeManager?.NodeManagers?[0] as MyNodeManager;
            nodeManager?.RemoveClientNode(session.SessionDiagnostics.SessionId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Node kaldırma hatası: {ex.Message}");
        }
    }
}
