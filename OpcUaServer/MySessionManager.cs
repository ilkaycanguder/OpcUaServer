using Opc.Ua;
using Opc.Ua.Server;
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

        public void OnSessionCreated(Session session, IServerInternal server)
        {
            _myServer.AddSession(session);
        }

        public void OnSessionDeleted(Session session, object reason)
        {
            _myServer.RemoveSession(session);
        }
    }
}
