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
    private readonly Dictionary<Guid, List<string>> clientAllowedTags = new Dictionary<Guid, List<string>>
    {
        { new Guid("550e8400-e29b-41d4-a716-446655440000"), new List<string> { "ayd_status1", "ayd_auto_mode" } }, // Client_1
        { new Guid("550e8400-e29b-41d4-a716-446655440001"), new List<string> { "*" } } // Client_2
    };
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
    protected override void OnMonitoredItemCreated(ServerSystemContext context, NodeHandle handle, MonitoredItem monitoredItem)
    {
        base.OnMonitoredItemCreated(context, handle, monitoredItem);

        if (handle.Node is BaseDataVariableState variable)
        {
            string tagName = variable.BrowseName?.Name ?? "unknown";
            NodeId currentSessionId = context.SessionId;

            Guid matchedClientGuid = Guid.Empty;
            foreach (var kvp in activeSessionMap)
            {
                if (kvp.Value?.Id == currentSessionId)
                {
                    matchedClientGuid = kvp.Key;
                    break;
                }
            }

            string clientName = matchedClientGuid == Guid.Parse("550e8400-e29b-41d4-a716-446655440000") ? "Client_1" :
                                matchedClientGuid == Guid.Parse("550e8400-e29b-41d4-a716-446655440001") ? "Client_2" : "Unknown";

            // Yetki kontrolü
            bool isAllowed = false;
            if (clientAllowedTags.TryGetValue(matchedClientGuid, out var allowedTags))
            {
                isAllowed = allowedTags.Contains("*") || allowedTags.Contains(tagName);
            }

            if (!isAllowed)
            {
                monitoredItem.SetMonitoringMode(MonitoringMode.Disabled);

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("⛔ [SUBSCRIBE REDDEDİLDİ]");
                Console.WriteLine($"🔒 Client: {clientName}");
                Console.WriteLine($"📛 Tag: {tagName}");
                Console.WriteLine($"🚫 Erişim izni yok!");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("🔔 [SUBSCRIBE OLAYI]");
                Console.WriteLine($"📄 İzlenen Tag: {tagName}");
                Console.WriteLine($"👤 Client: {clientName}");
                Console.ResetColor();
            }
        }
    }


    // Override with protected access modifier to match the base class
    protected override void OnMonitoredItemDeleted(ServerSystemContext context, NodeHandle handle, MonitoredItem monitoredItem)
    {
        base.OnMonitoredItemDeleted(context, handle, monitoredItem);

        if (handle.Node is BaseDataVariableState variable)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("📤 [UNSUBSCRIBE OLAYI]");
            Console.WriteLine($"🗑️ Kaldırılan Tag: {variable.BrowseName}");
            Console.WriteLine($"👤 SessionId: {context.SessionId}");
            Console.ResetColor();
        }
    }

    private ServiceResult HandleTagValueUpdate(ISystemContext context, NodeState node, ref object value)
    {
        if (node is BaseDataVariableState variable)
        {
            string nodeName = variable.BrowseName?.Name ?? "Unknown";
            NodeId currentSessionId = (context as ServerSystemContext)?.SessionId;

            Guid matchedClientGuid = Guid.Empty;
            foreach (var kvp in activeSessionMap)
            {
                if (kvp.Value?.Id == currentSessionId)
                {
                    matchedClientGuid = kvp.Key;
                    break;
                }
            }

            string clientName = matchedClientGuid == Guid.Parse("550e8400-e29b-41d4-a716-446655440000") ? "Client_1" :
                                matchedClientGuid == Guid.Parse("550e8400-e29b-41d4-a716-446655440001") ? "Client_2" : "Unknown";

            // Eğer yazılamazsa
            if ((variable.UserAccessLevel & AccessLevels.CurrentWrite) == 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("❌ [YAZMA REDDEDİLDİ]");
                Console.WriteLine($"🔒 Tag: {variable.NodeId}");
                Console.WriteLine($"👤 Client: {clientName}");
                Console.WriteLine($"📛 Yetki: Sadece Okuma (ReadOnly)");
                Console.WriteLine($"⛔ Değer değiştirme işlemi engellendi.\n");
                Console.ResetColor();
                return StatusCodes.BadNotWritable;
            }

            // Yazılabilir ise
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✅ [TAG GÜNCELLENDİ]");
            Console.WriteLine($"📍 Client: {clientName}");
            Console.WriteLine($"📄 Tag: {nodeName}");
            Console.ResetColor();

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
