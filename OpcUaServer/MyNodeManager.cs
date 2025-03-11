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


    public MyNodeManager(IServerInternal server, ApplicationConfiguration config)
        : base(server, config, Namespace)
    {
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
        Console.WriteLine($"🔵 OPC UA Server Namespace: {string.Join(", ", this.NamespaceUris.ToArray())}");


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
                UpdateTagInDatabase(variable.BrowseName.Name, newValue);
                UpdateTagInConsole(variable.BrowseName.Name, newValue);
                UpdateTagInOpcUaNode(variable, newValue);

                return ServiceResult.Good;
            }
        }

        return StatusCodes.BadTypeMismatch;
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

    // ✅ **Method to update server message**
    //public void UpdateServerMessage(string newMessage)
    //{
    //    lock (Lock)
    //    {
    //        messageFromServer.Value = newMessage;
    //        messageFromServer.Timestamp = DateTime.UtcNow;
    //        messageFromServer.ClearChangeMasks(SystemContext, true);
    //        Console.WriteLine($"Server message updated: {newMessage}");
    //    }
    //}
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
}
