using Opc.Ua;
using Opc.Ua.Server;
using OpcUaServer;
using System;
using System.Collections.Generic;
using System.Linq;

public class MyNodeManager : CustomNodeManager2
{
    private const string Namespace = "urn:opcua:chat";
    private readonly UserRoleManager _userRoleManager = new UserRoleManager();
    private readonly Dictionary<Guid, FolderState> clientNodes = new Dictionary<Guid, FolderState>();
    private FolderState _rootFolder; // sınıfın en üstüne ekle

    private List<(string tagName, int initialValue)> GetTagTemplate()
    {
        return new List<(string, int)>
    {
        ("ayd_auto_mode", 0),
        ("ayd_setman1", 1),
        ("ayd_status1", 1),
        ("ayd_setauto2", 0),
        ("ayd_status2", 1),
        ("ayd_error_flag", 0)
    };
    }

    // **Aktif oturumları takip eden sözlük (Client GUID -> Session)**
    private readonly Dictionary<Guid, Session> activeSessionMap;
    //private readonly Dictionary<Guid, List<string>> clientAllowedTags = new Dictionary<Guid, List<string>>
    //{
    //    { new Guid("550e8400-e29b-41d4-a716-446655440000"), new List<string> { "ayd_status1", "ayd_auto_mode" } }, // Client_1
    //    { new Guid("550e8400-e29b-41d4-a716-446655440001"), new List<string> { "*" } }, // Client_2
    //    { new Guid("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"), new List<string> { "*" } } // Admin her şeyi görsün
    //};
    public MyNodeManager(IServerInternal server, ApplicationConfiguration config, Dictionary<Guid, Session> sessionMap)
        : base(server, config, Namespace)
    {
        activeSessionMap = sessionMap;
    }

    public void RegisterClientNode(string username)
    {
        lock (Lock)
        {
            if (clientNodes.ContainsKey(GuidFromName(username)))
            {
                Console.WriteLine($"⚠️ Kullanıcı {username} zaten eklenmiş.");
                return;
            }

            var userGuid = GuidFromName(username); // string → sabit GUID üret

            FolderState clientFolder = new FolderState(null)
            {
                NodeId = new NodeId($"Client_{username}", NamespaceIndex),
                BrowseName = new QualifiedName($"Client_{username}", NamespaceIndex),
                DisplayName = new LocalizedText(username),
                TypeDefinitionId = ObjectTypeIds.FolderType
            };

            clientNodes[userGuid] = clientFolder;
            AddPredefinedNode(SystemContext, clientFolder);
            Console.WriteLine($"✅ Kullanıcı klasörü oluşturuldu: {username}");
        }
    }
    private Guid GuidFromName(string name)
    {
        using (var md5 = System.Security.Cryptography.MD5.Create())
        {
            byte[] hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(name));
            return new Guid(hash);
        }
    }

    public void RemoveClientNode(Guid clientGuid)
    {
        lock (Lock)
        {
            if (!clientNodes.ContainsKey(clientGuid))
            {
                Console.WriteLine($"Client Folder bulunamadı: {clientGuid}");
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
            // 1. Ana klasörü oluştur
            _rootFolder = CreateFolder(null, "EDBT1", "EDBT1");

            // 2. UA'nın ObjectsFolder'ına referans ekle
            if (!externalReferences.TryGetValue(ObjectIds.ObjectsFolder, out var references))
            {
                references = new List<IReference>();
                externalReferences[ObjectIds.ObjectsFolder] = references;
            }
            references.Add(new NodeStateReference(ReferenceTypeIds.Organizes, false, _rootFolder.NodeId));
            _rootFolder.AddReference(ReferenceTypeIds.Organizes, true, ObjectIds.ObjectsFolder);

            // 3. Ana klasörü adres alanına ekle
            AddPredefinedNode(SystemContext, _rootFolder);

            Console.WriteLine("✅ EDBT1 klasörü oluşturuldu ve ObjectsFolder'a bağlandı.");
        }
    }

    public void RegisterUserTagNodes(string username)
    {
        lock (Lock)
        {
            var role = _userRoleManager.GetUserRole(username);
            var allowedTags = _userRoleManager.GetAllowedTags(username);

            if (_rootFolder == null)
            {
                Console.WriteLine("Root klasör bulunamadı! CreateAddressSpace çağrılmamış olabilir.");
                return;
            }

            FolderState userFolder = CreateFolder(_rootFolder, username, username);

            foreach (var (tagName, initialValue) in GetTagTemplate())
            {
                if (!allowedTags.Contains("*") && !allowedTags.Contains(tagName))
                    continue;

                // 🔐 Yetki kontrolü - sadece Admin ve Operator yazabilsin
                bool isWriteAllowed = role != UserRole.Guest;

                var variable = new BaseDataVariableState(userFolder)
                {
                    NodeId = new NodeId($"{username}.{tagName}", NamespaceIndex),
                    BrowseName = new QualifiedName(tagName, NamespaceIndex),
                    DisplayName = new LocalizedText(tagName),
                    DataType = DataTypeIds.Int32,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = isWriteAllowed ? AccessLevels.CurrentReadOrWrite : AccessLevels.CurrentRead,
                    UserAccessLevel = isWriteAllowed ? AccessLevels.CurrentReadOrWrite : AccessLevels.CurrentRead,
                    Value = initialValue,
                    Historizing = false,
                    OnSimpleWriteValue = HandleTagValueUpdate
                };

                AddPredefinedNode(SystemContext, variable);
                userFolder.AddChild(variable);
                Console.WriteLine($"[{username}] Tag eklendi: {tagName} = {initialValue}");
            }
        }
    }


    protected override void OnMonitoredItemCreated(ServerSystemContext context, NodeHandle handle, MonitoredItem monitoredItem)
    {
        base.OnMonitoredItemCreated(context, handle, monitoredItem);

        if (handle.Node is BaseDataVariableState variable)
        {
            string tagName = variable.BrowseName?.Name ?? "unknown";
            string username = context?.UserIdentity?.DisplayName ?? "unknown";

            // Rol kontrolü
            bool isAllowed = _userRoleManager.HasPermission(username, tagName);

            if (!isAllowed)
            {
                monitoredItem.SetMonitoringMode(MonitoringMode.Disabled);

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[SUBSCRIBE REDDEDİLDİ]");
                Console.WriteLine($"Kullanıcı: {username}");
                Console.WriteLine($"Tag: {tagName}");
                Console.WriteLine($"Erişim izni yok!");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("[SUBSCRIBE OLAYI]");
                Console.WriteLine($"İzlenen Tag: {tagName}");
                Console.WriteLine($"Kullanıcı: {username}");
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
            Console.WriteLine("[UNSUBSCRIBE OLAYI]");
            Console.WriteLine($"Kaldırılan Tag: {variable.BrowseName}");
            Console.WriteLine($"SessionId: {context.SessionId}");
            Console.ResetColor();
        }
    }

    private ServiceResult HandleTagValueUpdate(ISystemContext context, NodeState node, ref object value)
    {
        if (node is BaseDataVariableState variable)
        {
            string nodeName = variable.BrowseName?.Name ?? "Unknown";
            string username = (context as ServerSystemContext)?.UserIdentity?.DisplayName ?? "unknown";

            // Rol kontrolü
            bool isAllowed = _userRoleManager.HasPermission(username, nodeName);

            if (!isAllowed)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[YAZMA REDDEDİLDİ - Yetki dışı erişim]");
                Console.WriteLine($"Kullanıcı: {username}");
                Console.WriteLine($"Tag: {nodeName}");
                Console.ResetColor();
                return StatusCodes.BadUserAccessDenied;
            }

            // Tag sadece okunabilir durumda ise
            if ((variable.UserAccessLevel & AccessLevels.CurrentWrite) == 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[YAZMA REDDEDİLDİ - ReadOnly Tag]");
                Console.WriteLine($"❌ Kullanıcı: {username}");
                Console.WriteLine($"📛 Tag: {nodeName}");
                Console.WriteLine($"🔒 UserAccessLevel: {variable.UserAccessLevel}");
                Console.ResetColor();
                return StatusCodes.BadNotWritable;
            }

            // ✅ Başarılı yazma
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("[TAG GÜNCELLENDİ]");
            Console.WriteLine($"Kullanıcı: {username}");
            Console.WriteLine($"Tag: {nodeName}");
            Console.WriteLine($"Yeni Değer: {value}");
            Console.ResetColor();

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
