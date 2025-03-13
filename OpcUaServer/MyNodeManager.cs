using Npgsql;
using Opc.Ua;
using Opc.Ua.Server;
using OPCCommonLibrary;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

class MyNodeManager : CustomNodeManager2
{
    private const string Namespace = "urn:opcua:chat";
    private BaseDataVariableState messageFromServer;
    private BaseDataVariableState messageFromClient;
    private string lastClientMessage = ""; // Son gelen mesajı saklamak için
    private string lastServerMessage = ""; // Son gelen mesajı saklamak için
    private DateTime lastMessageTime = DateTime.UtcNow; // Son mesaj zamanı
    private List<BaseDataVariableState> opcUaVariables = new List<BaseDataVariableState>();
    private System.Timers.Timer dbPollingTimer;

    private Dictionary<NodeId, FolderState> clientNodes = new Dictionary<NodeId, FolderState>();

    public MyNodeManager(IServerInternal server, ApplicationConfiguration config)
        : base(server, config, Namespace)
    {
    }

    // Yeni eklenen metot - Client node'u oluşturma
    public async Task RegisterClientNode(NodeId sessionId, Guid clientGuid)
    {
        lock (Lock)
        {
            // Eğer bu session ID için zaten bir node varsa, işlem yapma
            if (clientNodes.ContainsKey(sessionId))
            {
                Console.WriteLine($"Belirtilen ID için zaten bir Client Node var: {sessionId}");
                return;
            }

            try
            {
                // Client için özel bir klasör oluştur
                string clientIdString = sessionId.ToString().Replace(":", "_").Replace(";", "_");
                string clientNodeName = $"Client_{clientIdString}";

                FolderState clientFolder = new FolderState(null)
                {
                    NodeId = new NodeId(clientNodeName, NamespaceIndex),
                    BrowseName = new QualifiedName(clientNodeName, NamespaceIndex),
                    DisplayName = new LocalizedText(clientNodeName),
                    TypeDefinitionId = ObjectTypeIds.FolderType
                };

                // Client için özel değişkenler oluştur
                BaseDataVariableState clientStatus = new BaseDataVariableState(clientFolder)
                {
                    NodeId = new NodeId($"{clientNodeName}_Status", NamespaceIndex),
                    BrowseName = new QualifiedName("Status", NamespaceIndex),
                    DisplayName = new LocalizedText("Connection Status"),
                    DataType = DataTypeIds.String,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = "Connected"
                };

                BaseDataVariableState clientConnectTime = new BaseDataVariableState(clientFolder)
                {
                    NodeId = new NodeId($"{clientNodeName}_ConnectTime", NamespaceIndex),
                    BrowseName = new QualifiedName("ConnectTime", NamespaceIndex),
                    DisplayName = new LocalizedText("Connection Time"),
                    DataType = DataTypeIds.DateTime,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = DateTime.Now
                };
                BaseDataVariableState clientVariable = new BaseDataVariableState(clientFolder)
                {
                    NodeId = new NodeId($"{clientNodeName}_Value", NamespaceIndex),
                    BrowseName = new QualifiedName("ClientValue", NamespaceIndex),
                    DisplayName = new LocalizedText("Client Value"),
                    DataType = DataTypeIds.Int32,
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = 0  // Başlangıç değeri
                };

                // **FolderState İçine Ekleyelim**
                clientFolder.AddChild(clientVariable);

                // Değişkenleri klasöre ekle
                clientFolder.AddChild(clientStatus);
                clientFolder.AddChild(clientConnectTime);
                clientNodes[clientGuid] = clientFolder;
                Console.WriteLine($"✅ Client Node başarıyla oluşturuldu: {clientGuid}");

                // Client node'unu objects klasörüne bağla
                IList<IReference> references = new List<IReference>();
                references.Add(new NodeStateReference(ReferenceTypeIds.Organizes, false, clientFolder.NodeId));

                // Nodes klasörü var ise onun altına ekle, yoksa Objects klasörüne ekle
                NodeState parent = null;

                // Mevcut FolderState'leri kontrol et
                foreach (var node in PredefinedNodes.Values)
                {
                    if (node is FolderState folder && folder.BrowseName.Name == "MyFolder")
                    {
                        parent = folder;
                        break;
                    }
                }

                if (parent != null)
                {
                    // MyFolder altına ekle
                    ((FolderState)parent).AddReference(ReferenceTypeIds.Organizes, false, clientFolder.NodeId);
                    clientFolder.AddReference(ReferenceTypeIds.Organizes, true, parent.NodeId);
                }
                else
                {
                    // Objects klasörüne ekle
                    AddReferenceToObjectsFolder(clientFolder);
                }

                // Node'u ekle ve dictionary'de tut
                AddPredefinedNode(SystemContext, clientFolder);
                clientNodes.Add(sessionId, clientFolder);

                Console.WriteLine($"Client Node başarıyla oluşturuldu: {clientNodeName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Client Node oluşturma hatası: {ex.Message}");
            }
        }
    }
    // Yeni eklenen metot - Client node'unu silme
    public void RemoveClientNode(NodeId sessionId)
    {
        lock (Lock)
        {
            if (!clientNodes.TryGetValue(sessionId, out FolderState clientFolder))
            {
                Console.WriteLine($"Belirtilen ID için Client Node bulunamadı: {sessionId}");
                return;
            }

            try
            {
                // Client Node'unu sil
                DeleteNode(SystemContext, clientFolder.NodeId);

                // Referansı dictionary'den kaldır
                clientNodes.Remove(sessionId);

                Console.WriteLine($"Client Node başarıyla kaldırıldı: {clientFolder.DisplayName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Client Node kaldırma hatası: {ex.Message}");
            }
        }
    }

    // Yardımcı metot - Node'u Objects klasörüne ekler
    private void AddReferenceToObjectsFolder(NodeState node)
    {
        if (!PredefinedNodes.ContainsKey(ObjectIds.ObjectsFolder))
        {
            Console.WriteLine("❌ ObjectsFolder bulunamadı! OPC UA Server'ın doğru çalıştığını kontrol edin.");
            return;
        }

        var objectsFolder = PredefinedNodes[ObjectIds.ObjectsFolder] as FolderState;
        if (objectsFolder != null)
        {
            objectsFolder.AddReference(ReferenceTypeIds.Organizes, false, node.NodeId);
            node.AddReference(ReferenceTypeIds.Organizes, true, objectsFolder.NodeId);
            Console.WriteLine($"✅ {node.BrowseName} ObjectsFolder'a eklendi.");
        }
        else
        {
            Console.WriteLine("❌ ObjectsFolder'a referans eklenemedi!");
        }
    }


    private void DeleteNode(ISystemContext context, NodeId nodeId)
    {
        if (PredefinedNodes.TryGetValue(nodeId, out var node))
        {
            PredefinedNodes.Remove(nodeId);

            if (node is FolderState folder)
            {
                IList<BaseInstanceState> childNodes = new List<BaseInstanceState>();
                folder.GetChildren(context, childNodes);
                foreach (var child in childNodes)
                {
                    PredefinedNodes.Remove(child.NodeId);
                }
            }
        }
    }


    private async Task CheckDatabaseForChanges()
    {
        try
        {
            var currentTags = DatabaseHelper.GetTagsFromDatabase();
            lock (Lock)
            {
                foreach (var tag in currentTags)
                {
                    // Find the OPC UA variable for this tag
                    var variable = opcUaVariables.FirstOrDefault(v =>
                        v.BrowseName.Name == tag.TagName);

                    if (variable != null)
                    {
                        string tagValueString = tag.TagValue.ToString();

                        // Update only if value has changed
                        if (variable.Value?.ToString() != tagValueString)
                        {
                            Console.WriteLine($"DB Change Detected: {tag.TagName} = {tagValueString}");
                            variable.Value = tag.TagValue;
                            variable.Timestamp = DateTime.UtcNow;
                            variable.ClearChangeMasks(SystemContext, true);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Database polling error: {ex.Message}");
        }
    }

    public override void CreateAddressSpace(IDictionary<NodeId, IList<IReference>> externalReferences)
    {
        lock (Lock)
        {
            FolderState myFolder = new FolderState(null)
            {
                NodeId = new NodeId("MyFolder", NamespaceIndex),
                BrowseName = new QualifiedName("MyFolder", NamespaceIndex),
                DisplayName = new LocalizedText("MyFolder"),
                TypeDefinitionId = ObjectTypeIds.FolderType
            };

            var tags = DatabaseHelper.GetTagsFromDatabase();

            messageFromServer = new BaseDataVariableState(myFolder)
            {
                NodeId = new NodeId("MessageFromServer", (ushort)NamespaceIndex),
                BrowseName = new QualifiedName("MessageFromServer", NamespaceIndex),
                DataType = DataTypeIds.String,
                ValueRank = ValueRanks.Scalar,
                AccessLevel = AccessLevels.CurrentReadOrWrite,
                UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                Value = "Initial Message"
            };

            // ✅ **Variable to store messages from the client**
            messageFromClient = new BaseDataVariableState(myFolder)
            {
                NodeId = new NodeId("MessageFromClient", (ushort)NamespaceIndex),
                BrowseName = new QualifiedName("MessageFromClient", NamespaceIndex),
                DataType = DataTypeIds.String,
                ValueRank = ValueRanks.Scalar,
                AccessLevel = AccessLevels.CurrentReadOrWrite,
                UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                WriteMask = AttributeWriteMask.ValueForVariableType, // ✅ **Ensures the value is writable**
                UserWriteMask = AttributeWriteMask.ValueForVariableType,
                Value = "Waiting for client..."
            };
            foreach (var tag in tags)
            {
                var variable = new BaseDataVariableState(myFolder)
                {
                    NodeId = new NodeId(tag.TagName, (ushort)NamespaceIndex),
                    BrowseName = new QualifiedName(tag.TagName, NamespaceIndex),
                    DataType = DataTypeIds.Int32,  // Changed to Int32 since you're using integers
                    ValueRank = ValueRanks.Scalar,
                    AccessLevel = AccessLevels.CurrentReadOrWrite,
                    UserAccessLevel = AccessLevels.CurrentReadOrWrite,
                    Value = tag.TagValue  // This should already be an integer
                };

                variable.OnSimpleWriteValue = HandleTagValueUpdate;

                myFolder.AddChild(variable);
                opcUaVariables.Add(variable);

                myFolder.AddChild(variable);
                opcUaVariables.Add(variable);
            }

            messageFromClient.OnSimpleWriteValue = OnWriteClientMessage;
            messageFromClient.OnSimpleWriteValue = OnWriteServerMessage;

            myFolder.AddChild(messageFromServer);
            myFolder.AddChild(messageFromClient);

            externalReferences[ObjectIds.ObjectsFolder] = new List<IReference> {
                new NodeStateReference(ReferenceTypeIds.Organizes, false, myFolder.NodeId)
            };

            AddPredefinedNode(SystemContext, myFolder);
        }
    }
    private void UpdateTagInDatabase(string tagName, int newValue)
    {
        DatabaseHelper.UpdateTagValue(tagName, newValue);
    }

    // 🔹 Server Console Çıktısı
    private void UpdateTagInConsole(string tagName, int newValue)
    {
        Console.WriteLine($"🔄 [OPC UA Update] Tag: {tagName}, Yeni Değer: {newValue}");
    }
    private ServiceResult HandleTagValueUpdate(ISystemContext context, NodeState node, ref object value)
    {
        if (value == null)
            return StatusCodes.BadTypeMismatch;

        if (int.TryParse(value.ToString(), out int newValue))
        {
            var variable = node as BaseDataVariableState;
            if (variable != null)
            {
                // 🔥 PostgreSQL Güncelle
                DatabaseHelper.UpdateTagValue(variable.BrowseName.Name, newValue);

                // 🔥 Server Console Log
                Console.WriteLine($"[OPC UA Update] Tag: {variable.BrowseName.Name}, Yeni Değer: {newValue}");

                // 🔥 OPC UA Node Güncelleme
                variable.Value = newValue;
                variable.Timestamp = DateTime.UtcNow;
                variable.ClearChangeMasks(SystemContext, true);

                // 🔥 Yetkili istemcilere bildir
                NotifyAuthorizedClients(variable.NodeId, newValue);

                return ServiceResult.Good;
            }
        }

        return StatusCodes.BadTypeMismatch;
    }


    private void NotifyClients(BaseDataVariableState variable, int newValue)
    {
        lock (Lock)
        {
            Console.WriteLine($"✅ OPC UA Güncellendi: {variable.BrowseName.Name} = {newValue}");
            variable.Value = newValue;
            variable.Timestamp = DateTime.UtcNow;
            variable.ClearChangeMasks(SystemContext, true);
        }
    }

    // 🔹 OPC UA Node Güncelleme
    private void UpdateTagInOpcUaNode(BaseDataVariableState variable, int newValue)
    {
        variable.Value = newValue;
        variable.Timestamp = DateTime.UtcNow;
        variable.ClearChangeMasks(SystemContext, true);
    }
    public void StartDatabasePolling()
    {
        dbPollingTimer = new System.Timers.Timer(2000); // Check every 2 seconds
        dbPollingTimer.Elapsed += async (sender, e) => await CheckDatabaseForChanges();
        dbPollingTimer.AutoReset = true;
        dbPollingTimer.Enabled = true;
        Console.WriteLine("Database polling started - checking for changes every 2 seconds");
    }
    private ServiceResult OnWriteClientMessage(
        ISystemContext context,
        NodeState node,
        ref object value)
    {
        lock (Lock)
        {
            if (value == null)
            {
                Console.WriteLine("❌ Hata: NULL Değer Gönderildi!");
                return StatusCodes.BadUnexpectedError;
            }

            string newMessage = value.ToString().Trim();

            // Eğer mesaj boşsa veya önceki mesajla tamamen aynıysa işlem yapma
            if (string.IsNullOrEmpty(newMessage) || newMessage == lastClientMessage)
            {
                return ServiceResult.Good;
            }

            // Eğer mesaj önceki mesajla aynıysa ve 5 saniyeden kısa sürede tekrar geldiyse işlemi durdur
            if (newMessage == lastClientMessage && (DateTime.UtcNow - lastMessageTime).TotalSeconds < 5)
            {
                return ServiceResult.Good;
            }
            if (newMessage != lastClientMessage)
            {
                lastClientMessage = newMessage;
                Console.WriteLine($"**İstemciden Güncellenmiş Mesaj:** {newMessage}");
            }
            // Yeni mesajı kaydet ve sadece bir kez yazdır
            lastClientMessage = newMessage;
            lastMessageTime = DateTime.UtcNow;
            //DatabaseHelper.UpdateTagValue("MessageFromClient", newMessage);

            Console.WriteLine($"Client Message: {newMessage}");

            messageFromClient.Value = newMessage;
            messageFromClient.Timestamp = DateTime.UtcNow;
            messageFromClient.ClearChangeMasks(SystemContext, true);
        }
        return ServiceResult.Good;
    }
    private ServiceResult OnWriteServerMessage(
     ISystemContext context,
     NodeState node,
     ref object value)
    {
        lock (Lock)
        {
            if (value == null)
            {
                Console.WriteLine("❌ Hata: NULL Değer Gönderildi!");
                return StatusCodes.BadUnexpectedError;
            }

            string newMessage = value.ToString().Trim();

            if (string.IsNullOrEmpty(newMessage))
            {
                return ServiceResult.Good;
            }

            if (newMessage != messageFromServer.Value?.ToString())
            {
                messageFromServer.Value = newMessage;
                messageFromServer.Timestamp = DateTime.UtcNow;
                messageFromServer.ClearChangeMasks(SystemContext, true);

                Console.WriteLine($"✏️ **Sunucudan Güncellenmiş Mesaj:** {newMessage}");
            }
        }
        return ServiceResult.Good;
    }
    public void ReadClientMessage()
    {
        lock (Lock)
        {
            string currentMessage = messageFromClient.Value?.ToString();

            // Eğer mesaj boşsa veya önceki mesajla aynıysa tekrar yazdırma
            if (string.IsNullOrEmpty(currentMessage) || currentMessage == lastClientMessage)
            {
                return;
            }
        }
    }
    private bool IsClientAuthorized(Guid clientGuid, string tagName, string accessType)
    {
        using (var connection = new NpgsqlConnection(DatabaseHelper.connectionString))
        {
            connection.Open();

            // Önce tagName'e karşılık gelen tagid'yi al
            int? tagId = null;
            string tagIdQuery = "SELECT id FROM \"TESASch\".\"comp_tag_dtl\" WHERE \"TagName\" = @TagName";

            using (var cmd = new NpgsqlCommand(tagIdQuery, connection))
            {
                cmd.Parameters.AddWithValue("@TagName", tagName);
                var result = cmd.ExecuteScalar();
                if (result != null)
                {
                    tagId = (int)result;
                }
            }

            if (!tagId.HasValue)
            {
                Console.WriteLine($"⚠️ Veritabanında Tag Bulunamadı: {tagName}");
                return false;
            }

            // Yetkiyi kontrol et
            string query = $"SELECT {accessType} FROM \"TESASch\".\"clientyetkilendirme\" WHERE clientguid = @ClientGuid::text AND tagid @> ARRAY[@TagId]";

            using (var cmd = new NpgsqlCommand(query, connection))
            {
                cmd.Parameters.AddWithValue("@ClientGuid", clientGuid.ToString()); // 🔹 String'e çevir
                cmd.Parameters.AddWithValue("@TagId", tagId.Value);
                var result = cmd.ExecuteScalar();
                return result != null && (bool)result;
            }
        }
    }

    private void NotifyAuthorizedClients(NodeId nodeId, object newValue)
    {
        lock (Lock)
        {
            var authorizedClients = new List<Guid>();

            using (var connection = new NpgsqlConnection(DatabaseHelper.connectionString))
            {
                connection.Open();
                var query = "SELECT ClientGuid FROM \"TESASch\".clientyetkilendirme WHERE SubscribeAccess = TRUE";

                using (var cmd = new NpgsqlCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@NodeId", nodeId.ToString());

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            authorizedClients.Add(Guid.Parse(reader.GetString(0))); // 🔹 `TEXT`'i tekrar `Guid` yap
                        }
                    }
                }
            }

            Console.WriteLine($"🔹 Yetkilendirilmiş {authorizedClients.Count} istemci bulundu.");

            foreach (var clientGuid in authorizedClients)
            {
                if (clientNodes.TryGetValue(clientGuid, out var clientFolder))
                {
                    var clientVariable = clientFolder.FindChild(SystemContext, new QualifiedName("ClientValue", NamespaceIndex)) as BaseDataVariableState;

                    if (clientVariable != null)
                    {
                        clientVariable.Value = newValue;
                        clientVariable.Timestamp = DateTime.UtcNow;
                        clientVariable.ClearChangeMasks(SystemContext, true);
                        Console.WriteLine($"✅ Güncellendi: {clientGuid} -> {newValue}");
                    }
                    else
                    {
                        Console.WriteLine($"⚠️ Client değişkeni bulunamadı: {clientGuid}");
                    }
                }
                else
                {
                    Console.WriteLine($"⚠️ ClientFolder bulunamadı: {clientGuid}");
                }
            }
        }
    }
}
