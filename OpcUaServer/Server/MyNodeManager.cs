using Newtonsoft.Json;
using Opc.Ua;
using Opc.Ua.Server;
using OpcUaServer.Application.Managers;
using OpcUaServer.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class MyNodeManager : CustomNodeManager2
{
    private const string Namespace = "urn:opcua:chat";
    private readonly UserAccountManager _userAccountManager = new UserAccountManager();
    private readonly UserRoleManager _userRoleManager;
    private readonly Dictionary<Guid, Session> activeSessionMap;
    private readonly Dictionary<Guid, FolderState> clientNodes = new();
    private FolderState _rootFolder;

    public List<BaseDataVariableState> GetAllTagNodes()
    {
        lock (Lock)
        {
            return PredefinedNodes.OfType<BaseDataVariableState>().ToList();
        }
    }

    public MyNodeManager(IServerInternal server, ApplicationConfiguration config, Dictionary<Guid, Session> sessionMap)
        : base(server, config, Namespace)
    {
        activeSessionMap = sessionMap;
        _userRoleManager = new UserRoleManager(_userAccountManager);
    }

    public void RegisterClientNode(string username)
    {
        lock (Lock)
        {
            if (clientNodes.ContainsKey(GuidFromName(username))) return;

            var userGuid = GuidFromName(username);

            var clientFolder = new FolderState(null)
            {
                NodeId = new NodeId($"Client_{username}", NamespaceIndex),
                BrowseName = new QualifiedName($"Client_{username}", NamespaceIndex),
                DisplayName = new LocalizedText(username),
                TypeDefinitionId = ObjectTypeIds.FolderType
            };

            clientNodes[userGuid] = clientFolder;
            AddPredefinedNode(SystemContext, clientFolder);
        }
    }
    private Guid GuidFromName(string name)
    {
        using (var md5 = System.Security.Cryptography.MD5.Create())
        {
            byte[] hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(name));
            return new Guid(hash.Take(16).ToArray());
        }
    }

    public void RemoveClientNode(Guid clientGuid)
    {
        lock (Lock)
        {
            if (!clientNodes.ContainsKey(clientGuid)) return;

            DeleteNode(SystemContext, clientNodes[clientGuid].NodeId);
            clientNodes.Remove(clientGuid);
        }
    }

    public override void CreateAddressSpace(IDictionary<NodeId, IList<IReference>> externalReferences)
    {
        lock (Lock)
        {
            string jsonPath = Path.Combine(AppContext.BaseDirectory, "Configuration", "folders.json");
            if (!File.Exists(jsonPath)) return;

            var folderDefs = JsonConvert.DeserializeObject<List<FolderDefinition>>(File.ReadAllText(jsonPath));
            if (folderDefs == null) return;

            foreach (var root in folderDefs)
            {
                _rootFolder = CreateFolder(null, root.Name, root.DisplayName);

                if (!externalReferences.TryGetValue(ObjectIds.ObjectsFolder, out var references))
                {
                    references = new List<IReference>();
                    externalReferences[ObjectIds.ObjectsFolder] = references;
                }

                references.Add(new NodeStateReference(ReferenceTypeIds.Organizes, false, _rootFolder.NodeId));
                _rootFolder.AddReference(ReferenceTypeIds.Organizes, true, ObjectIds.ObjectsFolder);

                AddPredefinedNode(SystemContext, _rootFolder);

                foreach (var child in root.Children)
                {
                    var childFolder = CreateFolder(_rootFolder, child.Name, child.DisplayName);
                    foreach (var tag in child.Tags)
                    {
                        var variable = new BaseDataVariableState(childFolder)
                        {
                            NodeId = new NodeId($"{child.Name}.{tag}", NamespaceIndex),
                            BrowseName = new QualifiedName(tag, NamespaceIndex),
                            DisplayName = new LocalizedText(tag),
                            DataType = DataTypeIds.Int32,
                            ValueRank = ValueRanks.Scalar,
                            AccessLevel = AccessLevels.CurrentReadOrWrite,
                            UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                            Value = 0,
                            Historizing = false
                        };

                        childFolder.AddChild(variable);
                        AddPredefinedNode(SystemContext, variable);
                    }
                }
            }
        }
    }

    public void RegisterUserTagNodes(string username)
    {
        lock (Lock)
        {
            if (_rootFolder == null) return;

            var role = _userRoleManager.GetUserRole(username);
            var allowedTags = _userRoleManager.GetAllowedTags(role);
            bool isWriteAllowed = role != UserRole.Guest;

            var userFolder = CreateFolder(_rootFolder, username, username);

            string tagPath = Path.Combine(AppContext.BaseDirectory, "Configuration", "tags.json");
            if (!File.Exists(tagPath)) return;

            var allUserTags = JsonConvert.DeserializeObject<Dictionary<string, List<TagDefinition>>>(File.ReadAllText(tagPath));
            if (allUserTags == null || !allUserTags.TryGetValue(username, out var userTags)) return;

            foreach (var tag in userTags)
            {
                if (!allowedTags.Contains("*") && !allowedTags.Contains(tag.TagName)) continue;

                var variable = new BaseDataVariableState(userFolder)
                {
                    NodeId = new NodeId($"{username}.{tag.TagName}", NamespaceIndex),
                    BrowseName = new QualifiedName(tag.TagName, NamespaceIndex),
                    DisplayName = new LocalizedText(tag.TagName),
                    DataType = DataTypeIds.Int32,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = isWriteAllowed ? AccessLevels.CurrentReadOrWrite : AccessLevels.CurrentRead,
                    UserAccessLevel = isWriteAllowed ? AccessLevels.CurrentReadOrWrite : AccessLevels.CurrentRead,
                    Value = tag.InitialValue,
                    Historizing = false,
                    OnSimpleWriteValue = HandleTagValueUpdate
                };

                userFolder.AddChild(variable);
                AddPredefinedNode(SystemContext, variable);
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

            UserRole role = _userRoleManager.GetUserRole(username);
            bool isAllowed = _userRoleManager.HasPermission(role, tagName);

            if (!isAllowed)
            {
                monitoredItem.SetMonitoringMode(MonitoringMode.Disabled);

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[SUBSCRIBE REDDEDİLDİ]");
                Console.WriteLine($"Kullanıcı: {username}");
                Console.WriteLine($"Tag: {tagName}");
                Console.WriteLine("Erişim izni yok!");
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


    protected override void OnMonitoredItemDeleted(ServerSystemContext context, NodeHandle handle, MonitoredItem monitoredItem)
    {
        base.OnMonitoredItemDeleted(context, handle, monitoredItem);
    }

    private ServiceResult HandleTagValueUpdate(ISystemContext context, NodeState node, ref object value)
    {
        if (node is BaseDataVariableState variable)
        {
            string nodeName = variable.BrowseName?.Name ?? "Unknown";
            string username = (context as ServerSystemContext)?.UserIdentity?.DisplayName ?? "unknown";

            // Önce rolü al
            UserRole role = _userRoleManager.GetUserRole(username);

            // Yetki kontrolü
            bool isAllowed = _userRoleManager.HasPermission(role, nodeName);

            if (!isAllowed)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("[YAZMA REDDEDİLDİ - Yetki dışı erişim]");
                Console.WriteLine($"Kullanıcı: {username}");
                Console.WriteLine($"Tag: {nodeName}");
                Console.ResetColor();

                return StatusCodes.BadUserAccessDenied;
            }

            // Yalnızca yazma izni olan tag'lar
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
        var folder = new FolderState(parent)
        {
            NodeId = new NodeId(name, NamespaceIndex),
            BrowseName = new QualifiedName(name, NamespaceIndex),
            DisplayName = new LocalizedText(displayName),
            TypeDefinitionId = ObjectTypeIds.FolderType,
            EventNotifier = EventNotifiers.None
        };

        parent?.AddChild(folder);
        AddPredefinedNode(SystemContext, folder);
        return folder;
    }
}
