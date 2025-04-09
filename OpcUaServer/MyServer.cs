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

        // **MyNodeManager'ı oluştur ve session map'i ile başlat**
        var nodeManager = new MyNodeManager(server, configuration, activeSessions);
        nodeManagers.Add(nodeManager);

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
                // ImpersonateEventArgs.Accepted değil, reject durumunda exception fırlatılır
                throw new ServiceResultException(StatusCodes.BadUserAccessDenied, "Geçersiz kullanıcı");
            }

            Console.WriteLine($"Kullanıcı doğrulama başarılı: {username}");
            // Accepted özelliği yoktur, başarılı doğrulama exception fırlatılmaması ile belirlenir
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

    private void OnSessionCreated(Session session, SessionEventReason reason)
    {
        try
        {
            // 🔐 Admin sertifikası kontrolü
            if (session?.Identity?.TokenType == UserTokenType.Certificate)
            {
                var certSubject = session.Identity?.DisplayName ?? "";

                if (certSubject.Contains("CN=AdminClient"))
                {
                    Guid adminGuid = new Guid("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

                    // Admin zaten bağlıysa atla
                    if (!activeSessions.ContainsKey(adminGuid))
                    {
                        activeSessions[adminGuid] = session;
                        session.SessionDiagnostics.SessionName = adminGuid.ToString(); // SessionName içine GUID yazılıyor

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"Admin bağlantısı alındı | GUID: {adminGuid}");
                        Console.ResetColor();

                        // Admin düğümünü UA Server'da göster
                        ThreadPool.QueueUserWorkItem((_) =>
                        {
                            Thread.Sleep(500);
                            try
                            {
                                var nodeManager = CurrentInstance?.NodeManager?.NodeManagers?[0] as MyNodeManager;
                                nodeManager?.RegisterClientNode(adminGuid);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Admin düğüm eklenemedi: {ex.Message}");
                            }
                        });

                        return; // Admin bağlantısı tanımlandığı için normal client ataması yapılmaz
                    }
                    else
                    {
                        Console.WriteLine("Admin zaten bağlı!");
                        return;
                    }
                }
            }

            // 🔁 Normal client eşlemesi
            var availableClient = predefinedClients.FirstOrDefault(c => !activeSessions.ContainsKey(c.Key));
            if (availableClient.Key == Guid.Empty)
            {
                if (activeSessions.Count > 0)
                {
                    var oldestClient = activeSessions.Keys.First();
                    Console.WriteLine("Tüm GUID'ler kullanımda, eski oturum değiştiriliyor: {0}", predefinedClients[oldestClient]);
                    activeSessions.Remove(oldestClient);
                    availableClient = new KeyValuePair<Guid, string>(oldestClient, predefinedClients[oldestClient]);
                }
                else
                {
                    Console.WriteLine("Maksimum istemci sayısına ulaşıldı! Yeni istemci atanamadı.");
                    return;
                }
            }

            activeSessions[availableClient.Key] = session;
            session.SessionDiagnostics.SessionName = availableClient.Key.ToString();

            Console.WriteLine($"Yeni Client Bağlandı: {availableClient.Value} | GUID: {availableClient.Key} | Aktif Client Sayısı: {activeSessions.Count}");
            Console.WriteLine($"Oturum Bilgileri: SessionId={session.Id}");

            ThreadPool.QueueUserWorkItem((_) =>
            {
                Thread.Sleep(500);
                try
                {
                    var nodeManager = CurrentInstance?.NodeManager?.NodeManagers?[0] as MyNodeManager;
                    nodeManager?.RegisterClientNode(availableClient.Key);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Düğüm kaydederken hata: {ex.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Oturum oluşturma hatası: {ex.Message}");
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