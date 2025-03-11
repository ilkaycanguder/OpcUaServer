using Opc.Ua;
using Opc.Ua.Server;
using System.Collections.Generic;

class MyServer : StandardServer
{
    protected override MasterNodeManager CreateMasterNodeManager(IServerInternal server, ApplicationConfiguration configuration)
    {
        return new MasterNodeManager(server, configuration, null, new MyNodeManager(server, configuration));
    }
}
