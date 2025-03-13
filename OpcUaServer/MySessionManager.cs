using Npgsql;
using Opc.Ua;
using Opc.Ua.Server;
using OPCCommonLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpcUaServer
{
    public class MySessionManager : SessionManager
    {
        private readonly MyServer _myServer;

        public MySessionManager(MyServer myServer, IServerInternal server, ApplicationConfiguration configuration)
            : base(server, configuration)
        {
            _myServer = myServer;
        }

        public async Task OnSessionCreatedAsync(Session session, IServerInternal server)
        {
            try
            {
                // **Client'a özel GUID yükle veya sıradaki boş Client'ı al**
                (string resolvedClientName, Guid clientGuid) = GuidHelper.GetOrCreateClient();

                // **Server'a Client'ı ekle (ClientId ile)**
                await _myServer.AddSessionAsync(session, clientGuid);

                Console.WriteLine($"✅ Yeni Client Bağlandı: {resolvedClientName} | GUID: {clientGuid} | Session ID: {session.Id}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Hata: Session oluşturulurken GUID okunamadı! {ex.Message}");
            }
        }

        public async Task OnSessionDeletedAsync(Session session, object reason)
        {
            try
            {
                var clientId = _myServer.GetClientId(session);
                _myServer.RemoveSession(session);

                using (var connection = new NpgsqlConnection(DatabaseHelper.connectionString))
                {
                    await connection.OpenAsync(); // **ASYNC aç**
                    var query = "DELETE FROM \"TESASch\".\"clientyetkilendirme\" WHERE clientguid = @ClientGuid";

                    using (var cmd = new NpgsqlCommand(query, connection))
                    {
                        cmd.Parameters.AddWithValue("@ClientGuid", clientId);
                        await cmd.ExecuteNonQueryAsync(); // **ASYNC çalıştır**
                    }
                }

                Console.WriteLine($"🔴 Client Bağlantısı Kapatıldı | Client ID: {clientId} | Sebep: {reason}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Hata: Session silinirken GUID bulunamadı! {ex.Message}");
            }
        }
    }
}
