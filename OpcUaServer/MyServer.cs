using Microsoft.AspNetCore.Identity;
using Opc.Ua;
using Opc.Ua.Server;
using OpcUaServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

public class MyServer : StandardServer
{
    private readonly UserAccountManager _userManager;
    private readonly Dictionary<Guid, Session> activeSessions = new Dictionary<Guid, Session>();
    private readonly Dictionary<string, Session> userSessionMap = new(); // username → session
    private MyNodeManager _nodeManager;

    private MySessionManager _sessionManager;

    public MyServer()
    {
        _userManager = new UserAccountManager();
    }
    // **Statik GUID Listesi (Sabit 2 Client)**
    private static readonly Dictionary<Guid, string> predefinedClients = new Dictionary<Guid, string>
    {
        { new Guid("550e8400-e29b-41d4-a716-446655440000"), "Client_1" },
        { new Guid("550e8400-e29b-41d4-a716-446655440001"), "Client_2" },
        { new Guid("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"), "Admin" }
    };

    protected override MasterNodeManager CreateMasterNodeManager(IServerInternal server, ApplicationConfiguration configuration)
    {
        List<INodeManager> nodeManagers = new List<INodeManager>();
        _nodeManager = new MyNodeManager(server, configuration, activeSessions);

        // **MyNodeManager'ı oluştur ve session map'i ile başlat**
        //var nodeManager = new MyNodeManager(server, configuration, activeSessions);
        nodeManagers.Add(_nodeManager); 

        // **Ana NodeManager'ı oluştur**
        return new MasterNodeManager(server, configuration, null, nodeManagers.ToArray());
    }

    protected override void OnServerStarted(IServerInternal server)
    {
        base.OnServerStarted(server);

        // Oturum sürelerini yapılandır
        try
        {
            Configuration.ServerConfiguration.MaxSessionTimeout = 3600000; // 1 saat
            Configuration.ServerConfiguration.MaxRequestAge = 600000; // 10 dakika

            Console.WriteLine("Sunucu oturum ayarları güncellendi");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Oturum ayarları güncellenirken hata: {ex.Message}");
        }

        // **Oturum Yönetimi**
        server.SessionManager.SessionCreated += OnSessionCreated;
        server.SessionManager.SessionClosing += OnSessionDeleted;
        // SessionManager'e erişim sağlayın ve kimlik doğrulama olayını kaydedelim
        server.SessionManager.ImpersonateUser += OnImpersonateUser;
    }

    private void OnImpersonateUser(Session session, ImpersonateEventArgs args)
    {
        if (args.NewIdentity is UserNameIdentityToken userNameIdentity)
        {
            string username = userNameIdentity.UserName;
            string password = userNameIdentity.DecryptedPassword;

            if (!_userManager.ValidateUser(username, password))
            {
                Console.WriteLine($"Kullanıcı doğrulama başarısız: {username}");
                throw new ServiceResultException(StatusCodes.BadUserAccessDenied, "Geçersiz kullanıcı");
            }

            Console.WriteLine($"Kullanıcı doğrulama başarılı: {username}");

            // 🧠 Tam burada tag oluştur!
            _nodeManager?.RegisterUserTagNodes(username);
        }
        else
        {
            Console.WriteLine("Anonim bağlantı kabul edildi");
            _nodeManager?.RegisterUserTagNodes("Anonymous");
        }
    }

    protected override void OnServerStopping()
    {
        if (ServerInternal != null && ServerInternal.SessionManager != null)
        {
            ServerInternal.SessionManager.ImpersonateUser -= OnImpersonateUser;
        }

        base.OnServerStopping();
    }

    protected void OnSessionCreated(Session session, SessionEventReason reason)
    {
        var username = session?.Identity?.DisplayName;
        if (!string.IsNullOrEmpty(username))
        {
            _nodeManager?.RegisterUserTagNodes(username); // ← bu satırı kaldır
            Console.WriteLine($"Yeni kullanıcı oturumu: {username}");
        }
    }
        


    private void OnSessionDeleted(Session session, SessionEventReason reason)
    {
        try
        {
            var clientEntry = activeSessions.FirstOrDefault(x => x.Value == session);
            if (clientEntry.Key != Guid.Empty)
            {
                Console.WriteLine($"Client Bağlantısı Kapatılıyor | Client: {predefinedClients[clientEntry.Key]} | GUID: {clientEntry.Key} | Sebep: {reason}");

                // Oturumu kaldır
                activeSessions.Remove(clientEntry.Key);

                // Client düğümünü kaldır (kısa bir gecikme ile)
                ThreadPool.QueueUserWorkItem((_) =>
                {
                    Thread.Sleep(500); // 500 ms gecikme
                    try
                    {
                        var nodeManager = CurrentInstance?.NodeManager?.NodeManagers?[0] as MyNodeManager;
                        nodeManager?.RemoveClientNode(clientEntry.Key);
                        Console.WriteLine($"Client Bağlantısı Kapatıldı | Client: {predefinedClients[clientEntry.Key]} | GUID: {clientEntry.Key} | Aktif Client Sayısı: {activeSessions.Count}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Düğüm kaldırırken hata: {ex.Message}");
                    }
                });
            }
            else
            {
                Console.WriteLine($"Bilinmeyen bir oturum kapatıldı: {session.Id}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Oturum kapatma hatası: {ex.Message}");
        }
    }
}