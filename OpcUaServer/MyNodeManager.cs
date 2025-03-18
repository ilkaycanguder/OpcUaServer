using Opc.Ua;
using Opc.Ua.Server;
using System;
using System.Collections.Generic;
using System.Linq;

public class MyNodeManager : CustomNodeManager2
{
    private const string Namespace = "urn:opcua:chat";
    private readonly Dictionary<Guid, FolderState> clientNodes = new Dictionary<Guid, FolderState>();

    // **Aktif oturumları takip eden sözlük (Client GUID -> Session)**
    private readonly Dictionary<Guid, Session> activeSessionMap;

    public MyNodeManager(IServerInternal server, ApplicationConfiguration config, Dictionary<Guid, Session> sessionMap)
        : base(server, config, Namespace)
    {
        activeSessionMap = sessionMap;
    }

    public void RegisterClientNode(Guid clientGuid)
    {
        lock (Lock)
        {
            if (clientNodes.ContainsKey(clientGuid))
            {
                Console.WriteLine($"⚠️ Client {clientGuid} zaten eklenmiş.");
                return;
            }

            FolderState clientFolder = new FolderState(null)
            {
                NodeId = new NodeId($"Client_{clientGuid}", NamespaceIndex),
                BrowseName = new QualifiedName($"Client_{clientGuid}", NamespaceIndex),
                DisplayName = new LocalizedText($"Client_{clientGuid}"),
                TypeDefinitionId = ObjectTypeIds.FolderType
            };

            clientNodes[clientGuid] = clientFolder;
            AddPredefinedNode(SystemContext, clientFolder);
            Console.WriteLine($"✅ Client Folder başarıyla oluşturuldu: {clientGuid}");
        }
    }

    public void RemoveClientNode(Guid clientGuid)
    {
        lock (Lock)
        {
            if (!clientNodes.ContainsKey(clientGuid))
            {
                Console.WriteLine($"⚠️ Client Folder bulunamadı: {clientGuid}");
                return;
            }

            DeleteNode(SystemContext, clientNodes[clientGuid].NodeId);
            clientNodes.Remove(clientGuid);
            Console.WriteLine($"🔴 Client Folder kaldırıldı: {clientGuid}");
        }
    }

    public override void CreateAddressSpace(IDictionary<NodeId, IList<IReference>> externalReferences)
    {
        lock (Lock)
        {

            // **Ana klasör oluştur**
            FolderState rootFolder = CreateFolder(null, "EDBT1", "EDBT1");

            // **Objects klasörüne referans ekle**
            IList<IReference> references;
            if (!externalReferences.TryGetValue(ObjectIds.ObjectsFolder, out references))
            {
                references = new List<IReference>();
                externalReferences[ObjectIds.ObjectsFolder] = references;
            }
            references.Add(new NodeStateReference(ReferenceTypeIds.Organizes, false, rootFolder.NodeId));

            Console.WriteLine($"Ana klasör oluşturuldu: {rootFolder.BrowseName}");

            // **Client bazlı tag listesi**
            Dictionary<string, List<(string tagName, int initialValue)>> clientTags = new Dictionary<string, List<(string, int)>>()
            {
                {
                    "Client_1", new List<(string, int)>
                    {
                        ("ayd_auto_mode", 0),
                        ("ayd_setman1", 1),
                        ("ayd_status1", 1)
                    }
                },
                {
                    "Client_2", new List<(string, int)>
                    {
                        ("ayd_setauto2", 0),
                        ("ayd_status2", 1),
                        ("ayd_error_flag", 0)
                    }
                }
            };

            foreach (var client in clientTags)
            {
                string clientName = client.Key;
                List<(string tagName, int initialValue)> tags = client.Value;

                // **Her istemci için ayrı bir klasör oluştur**
                FolderState clientFolder = CreateFolder(rootFolder, clientName, clientName);

                foreach (var (tagName, initialValue) in tags)
                {
                    var variable = new BaseDataVariableState(clientFolder)
                    {
                        NodeId = new NodeId($"{clientName}.{tagName}", NamespaceIndex),
                        BrowseName = new QualifiedName(tagName, NamespaceIndex),
                        DisplayName = new LocalizedText(tagName),
                        DataType = DataTypeIds.Int32,
                        ValueRank = ValueRanks.Scalar,
                        AccessLevel = AccessLevels.CurrentReadOrWrite,
                        UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                        Value = initialValue,
                        Historizing = false
                    };

                    // **Yetkilendirme: Client_1 sadece kendi taglarını görsün**
                    if (clientName == "Client_1")
                    {
                        variable.AccessLevel = AccessLevels.CurrentRead;
                        variable.UserAccessLevel = AccessLevels.CurrentRead;
                    }
                    else if (clientName == "Client_2")
                    {
                        variable.AccessLevel = AccessLevels.CurrentReadOrWrite;
                        variable.UserAccessLevel = AccessLevels.CurrentReadOrWrite;
                    }

                    // **Yazma işlemi event'ini ekle**
                    variable.OnSimpleWriteValue = HandleTagValueUpdate;

                    clientFolder.AddChild(variable);
                    AddPredefinedNode(SystemContext, variable);

                    Console.WriteLine($"OPC UA Değişkeni oluşturuldu: {clientName} | {tagName} = {initialValue}");
                }
            }

            Console.WriteLine("OPC UA Adres Alanı başarıyla oluşturuldu!");
        }
    }

    private ServiceResult HandleTagValueUpdate(ISystemContext context, NodeState node, ref object value)
    {
        if (node is BaseDataVariableState variable)
        {
            string nodeName = variable.BrowseName.Name;
            string nodeId = $"NS{variable.NodeId.NamespaceIndex}|String|{variable.BrowseName.NamespaceIndex}_{variable.BrowseName.Name}";

            // **Geçerli OPC UA session'ın SessionId'sini al**
            NodeId sessionId = (context as ServerSystemContext)?.SessionId;

            if (sessionId == null)
            {
                Console.WriteLine($"Hata: Geçerli session bulunamadı! {nodeName}");
                return StatusCodes.BadSessionIdInvalid;
            }

            // **SessionId ile Client GUID'ini bul**
            Guid? clientGuid = activeSessionMap.FirstOrDefault(x => x.Value.SessionDiagnostics.SessionId == sessionId).Key;

            if (clientGuid == null || clientGuid == Guid.Empty)
            {
                Console.WriteLine($"Hata: Geçerli Client GUID bulunamadı! {nodeName}");
                return StatusCodes.BadSessionIdInvalid;
            }

            // **Hangi Client olduğunu belirle**
            string clientName = (clientGuid == Guid.Parse("550e8400-e29b-41d4-a716-446655440000")) ? "Client_1" :
                                (clientGuid == Guid.Parse("550e8400-e29b-41d4-a716-446655440001")) ? "Client_2" : "Unknown";

            // **Yetkilendirme kontrolü: Client_1 için yalnızca Read izinli**
            if (clientName == "Client_1")
            {
                // Özel hata mesajı ekle
                string errorNodeId = $"NS{variable.NodeId.NamespaceIndex}|String|{variable.Parent.BrowseName}.{variable.BrowseName.Name}";
                Console.WriteLine($"❌ Yetkisiz Yazma Girişimi! Write to node '{errorNodeId}' failed [ret = BadNotWritable] | Client: {clientName}");
                return StatusCodes.BadNotWritable;
            }

            // **Yetkisi varsa, değeri güncelle**
            Console.WriteLine($"✅ OPC UA Değişkeni güncellendi: {clientName} | {nodeName} = {value}");
            variable.Value = value;
            variable.Timestamp = DateTime.UtcNow;
            variable.ClearChangeMasks(SystemContext, true);

            return ServiceResult.Good;
        }

        return StatusCodes.BadTypeMismatch;
    }



    private FolderState CreateFolder(NodeState parent, string name, string displayName)
    {
        FolderState folder = new FolderState(parent)
        {
            NodeId = new NodeId(name, NamespaceIndex),
            BrowseName = new QualifiedName(name, NamespaceIndex),
            DisplayName = new LocalizedText(displayName),
            TypeDefinitionId = ObjectTypeIds.FolderType,
            EventNotifier = EventNotifiers.None
        };

        if (parent != null)
        {
            parent.AddChild(folder);
        }

        AddPredefinedNode(SystemContext, folder);
        return folder;
    }
}
