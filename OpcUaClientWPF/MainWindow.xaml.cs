using Npgsql;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;
using OPCCommonLibrary;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace OpcUaClientWPF;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private Session session;

    private Subscription subscription;
    private const string ServerUrl = "opc.tcp://localhost:4840/UA/OpcUaServer";
    private string namespaceUri = "urn:opcua:chat";
    private int namespaceIndex = -1;
    private ObservableCollection<ChatMessage> messageList = new ObservableCollection<ChatMessage>();
    public ObservableCollection<OpcTag> OpcTags { get; set; } = new ObservableCollection<OpcTag>();
    private Dictionary<string, MonitoredItem> monitoredItems = new Dictionary<string, MonitoredItem>();
    public MainWindow()
    {
        InitializeComponent();
        this.DataContext = this;

        Loaded += async (s, e) =>
        {
            try
            {
                // 🔹 Guid.Config.xml'den ilk kullanılmayan Client GUID'ini al
                Guid clientGuid = GuidHelper.GetClientGuidFromConfig();

                Console.WriteLine($"🟢 Client Bağlanıyor: {clientGuid}");
                await InitializeOpcUaClient(clientGuid); // ✅ Sadece clientGuid gönderildi
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Hata: {ex.Message}", "Bağlantı Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };
    }

    private async Task GetOpcUaNodes(Guid clientGuid)
    {
        if (session == null || !session.Connected)
        {
            UpdateStatus("⚠️ OPC UA Sunucusuna bağlı değil!", Brushes.Red);
            return;
        }
        try
        {
            OpcTags.Clear();

            // 1️⃣ Client'in yetkili olduğu tag ID'lerini al
            var authorizedTags = await DatabaseHelper.GetAuthorizedTagsAsync(clientGuid);

            if (authorizedTags.Count == 0)
            {
                UpdateStatus("⚠️ Bu istemci için yetkilendirilmiş tag bulunamadı!", Brushes.Orange);
                return;
            }

            // 🔹 **Sadece tag ID'lerini çek**
            var tagIds = authorizedTags.Select(t => t.Id).ToArray();

            // 🔥 **Eğer hiç tag ID yoksa işlemi durdur**
            if (tagIds.Length == 0)
            {
                UpdateStatus("⚠️ Yetkilendirilmiş tag bulunamadı!", Brushes.Orange);
                return;
            }

            // 2️⃣ Yetkili tag ID'lerine göre `comp_tag_dtl` tablosundan tag isimlerini al
            using (var conn = new NpgsqlConnection(DatabaseHelper.connectionString))
            {
                conn.Open();

                string query = "SELECT \"id\", \"TagName\", \"TagValue\" FROM \"TESASch\".\"comp_tag_dtl\" WHERE \"id\" = ANY(@TagIds)";

                using (var cmd = new NpgsqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@TagIds", tagIds);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int tagId = reader.GetInt32(0);
                            string tagName = reader.GetString(1);
                            int tagValue = reader.GetInt32(2); // 🔥 Tag değerini de ekledik

                            OpcTags.Add(new OpcTag
                            {
                                Id = tagId,
                                TagName = tagName,
                                TagValue = tagValue,
                                LastUpdate = DateTime.Now
                            });
                        }
                    }
                }
            }

            // 🔹 **İstemci adını al**
            string clientName = GuidHelper.GetClientNameByGuid(clientGuid);

            // 🔹 **Sonuçları günlüğe kaydet**
            Console.WriteLine($"📊 {clientName} ({clientGuid}) için {OpcTags.Count} yetkili tag yüklendi");

            // 🔹 **Kullanıcı arayüzünü güncelle**
            UpdateStatus($"✅ İstemci: {clientName}, GUID: {clientGuid}, {OpcTags.Count} yetkili OPC UA tag'ı yüklendi.", Brushes.Green);
        }
        catch (Exception ex)
        {
            UpdateStatus($"⚠️ OPC UA Node Okuma Hatası: {ex.Message}", Brushes.Red);
            Console.WriteLine($"❌ Tag Yükleme Hatası: {ex.Message}");
        }
    }


    private async Task BrowseNodesRecursively(NodeId nodeId, string path, int depth = 0, int maxDepth = 3)
    {
        if (depth > maxDepth) return; // Sonsuz döngüyü önlemek için

        try
        {
            // Browse servisi ile düğümleri al
            BrowseDescriptionCollection browseDescription = new BrowseDescriptionCollection
            {
               new BrowseDescription
               {
                    NodeId = nodeId,
                    BrowseDirection = BrowseDirection.Forward,
                    ReferenceTypeId = ReferenceTypeIds.HierarchicalReferences,
                    IncludeSubtypes = true,
                    NodeClassMask = (uint)(NodeClass.Object | NodeClass.Variable),
                    ResultMask = (uint)BrowseResultMask.All
               }
            };
            BrowseResultCollection results;
            DiagnosticInfoCollection diagnostics;

            session.Browse(null, null, 100, browseDescription, out results, out diagnostics);

            if (results.Count > 0 && results[0].References != null)
            {
                foreach (var reference in results[0].References)
                {
                    string newPath = string.IsNullOrEmpty(path) ? reference.DisplayName.Text : $"{path}/{reference.DisplayName.Text}";
                    NodeId targetNodeId = ExpandedNodeId.ToNodeId(reference.NodeId, session.NamespaceUris);

                    // Düğüm türünü kontrol et
                    bool isVariable = ((reference.NodeClass & NodeClass.Variable) != 0);

                    if (isVariable)
                    {
                        // Variable türünde ise değerini ve veri tipini oku
                        DataValue value = null;
                        try
                        {
                            value = session.ReadValue(targetNodeId);
                        }
                        catch
                        {
                            // Değer okunamadıysa sessizce devam et
                        }

                        OpcTag tag = new OpcTag
                        {
                            Id = OpcTags.Count + 1, // ID'yi otomatik artır
                            TagName = reference.DisplayName.Text,
                            TagValue = value?.Value != null ? Convert.ToInt32(value.Value) : 0, // 🔥 `TagValue` integer!
                            LastUpdate = DateTime.Now
                        };

                        OpcTags.Add(tag);

                        // Veri değişikliğini izlemeye başla
                        AddMonitoredItem(tag);
                    }

                    // Alt düğümleri tara (recursive)
                    if ((reference.NodeClass & NodeClass.Object) != 0)
                    {
                        await BrowseNodesRecursively(targetNodeId, newPath, depth + 1, maxDepth);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Düğüm tarama hatası ({nodeId}): {ex.Message}");
        }
    }
    private void AddMonitoredItem(OpcTag tag)
    {
        try
        {
            if (subscription == null) return;

            NodeId nodeId = new NodeId(tag.TagName, (ushort)namespaceIndex);

            if (monitoredItems.ContainsKey(tag.TagName)) return;

            MonitoredItem monitoredItem = new MonitoredItem(subscription.DefaultItem)
            {
                DisplayName = tag.TagName,
                StartNodeId = nodeId,
                AttributeId = Attributes.Value,
                SamplingInterval = 500, // 🔥 500ms'de bir güncelleme kontrolü
                QueueSize = 10,
                DiscardOldest = true
            };

            monitoredItem.Notification += (item, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (e.NotificationValue is MonitoredItemNotification notification && notification.Value?.Value != null)
                    {
                        var existingTag = OpcTags.FirstOrDefault(t => t.TagName == tag.TagName);
                        if (existingTag != null)
                        {
                            existingTag.TagValue = Convert.ToInt32(notification.Value.Value);
                            existingTag.LastUpdate = DateTime.Now;
                            existingTag.State = "Updated"; // 🔥 VisualState değişimi

                            // **Görsel UI güncellemesi için state değiştir**
                            VisualStateManager.GoToState(this, "UpdatedState", true);

                            DatabaseHelper.UpdateTagValue(existingTag.TagName, existingTag.TagValue);
                        }
                    }
                    tagsListView.Items.Refresh();
                });
            };

            subscription.AddItem(monitoredItem);
            monitoredItems[tag.TagName] = monitoredItem;
            subscription.ApplyChanges();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"MonitoredItem ekleme hatası: {ex.Message}");
        }
    }
    public async Task ConnectToOpcUaServer()
    {
        try
        {
            // 🔥 Yeni Client oluştur veya mevcut Client GUID'ini yükle
            var (clientName, clientGuid) = GuidHelper.GetOrCreateClient();

            Console.WriteLine($"🟢 Client Bağlanıyor: {clientGuid} ({clientName})");

            // OPC UA Bağlantısı
            await InitializeOpcUaClient(clientGuid);

            // Bağlandıktan sonra GUI'ye yazdır
            Dispatcher.Invoke(() =>
            {
                statusTextBlock.Text = $"Bağlı Client ID: {clientGuid}";
                statusTextBlock.Foreground = Brushes.Green;
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Client Bağlantı Hatası: {ex.Message}");
        }
    }

    private async Task InitializeOpcUaClient(Guid clientGuid)
    {
        try
        {
            // OPC UA Yapılandırma Dosyası - her istemciye özel olabilir
            string configFilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OpcUaClient.Config.xml");

            await EnsureConfigurationFileExists(configFilePath);

            ApplicationInstance application = new ApplicationInstance
            {
                ApplicationType = ApplicationType.Client
            };

            ApplicationConfiguration config = await application.LoadApplicationConfiguration(configFilePath, silent: false);
            if (config == null)
            {
                throw new Exception("Yapılandırma dosyası yüklenemedi!");
            }

            if (config.ApplicationType != ApplicationType.Client)
            {
                UpdateStatus("ApplicationType hatalı, Client olarak güncellendi.", Brushes.Orange);
                config.ApplicationType = ApplicationType.Client;
            }

            bool certOK = await application.CheckApplicationInstanceCertificate(false, 2048);
            if (!certOK)
            {
                certOK = await application.CheckApplicationInstanceCertificate(true, 2048);
                if (!certOK)
                {
                    throw new Exception("İstemci sertifikası oluşturulamadı veya geçersiz!");
                }
            }

            application.ApplicationConfiguration = config;

            UpdateStatus($"PostgreSQL veritabanına bağlı OPC UA sunucusuna bağlanılıyor: {ServerUrl}", Brushes.Blue);

            var endpoint = CoreClientUtils.SelectEndpoint(ServerUrl, false, 15000);
            var configEndpoint = new ConfiguredEndpoint(null, endpoint, EndpointConfiguration.Create(config));

            session = await Session.Create(
                config,
                configEndpoint,
                false,
                clientGuid.ToString(),
                60000,
                new UserIdentity(new AnonymousIdentityToken()),
                null);


            subscription = new Subscription(session.DefaultSubscription)
            {
                PublishingInterval = 1000,
                KeepAliveCount = 10,
                LifetimeCount = 10000,
                PublishingEnabled = true
            };

            session.AddSubscription(subscription);
            subscription.Create();

            session.FetchNamespaceTables();
            namespaceIndex = session.NamespaceUris.GetIndex(namespaceUri);

            if (namespaceIndex == -1)
            {
                throw new Exception("Namespace index alınamadı!");
            }

            UpdateStatus($"🟢 OPC UA sunucusuna bağlandı! GUID: {clientGuid}, Namespace Index: {namespaceIndex}", Brushes.Green);

            // **Yetkilendirilmiş OPC UA Node'larını PostgreSQL'den al**
            var authorizedTags = await DatabaseHelper.GetAuthorizedTagsAsync(clientGuid);
            OpcTags.Clear();
            foreach (var tag in authorizedTags)
            {
                OpcTags.Add(tag);
            }

            UpdateStatus($"✅ {OpcTags.Count} yetkilendirilmiş OPC UA tag'ı alındı.", Brushes.Green);

            foreach (var tag in OpcTags)
            {
                AddMonitoredItem(tag);
            }

            var messageTag = OpcTags.FirstOrDefault(t => t.TagName.Contains("MessageFromServer"));
            if (messageTag == null)
            {
                MonitoredItem monitoredItem = new MonitoredItem(subscription.DefaultItem)
                {
                    DisplayName = "ServerMessageMonitor",
                    StartNodeId = new NodeId("MessageFromServer", (ushort)namespaceIndex),
                    AttributeId = Attributes.Value,
                    SamplingInterval = 1000,
                    QueueSize = 10,
                    DiscardOldest = true
                };

                monitoredItem.Notification += OnServerMessageReceived;
                subscription.AddItem(monitoredItem);
                UpdateStatus("✅ Varsayılan sunucu mesajları dinleniyor...", Brushes.Green);
            }

            subscription.ApplyChanges();
            await GetOpcUaNodes(clientGuid);
        }
        catch (Exception ex)
        {
            UpdateStatus($"❌ Hata: {ex.Message}", Brushes.Red);
            MessageBox.Show($"Bağlantı hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }


    // Sunucudan gelen mesajları işle
    private void OnServerMessageReceived(MonitoredItem monitoredItem, MonitoredItemNotificationEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            if (e.NotificationValue is not MonitoredItemNotification notification)
            {
                Console.WriteLine("Uyarı: MonitoredItemNotification boş veya yanlış formatta.");
                return;
            }

            if (notification.Value?.Value != null)
            {
                string message = notification.Value.Value.ToString();
                Console.WriteLine($"Sunucudan Gelen Mesaj: {message}");

                // PostgreSQL'den gelen mesajı göster
                AddMessage(message, "Sunucu (PostgreSQL)", false);
            }
        }, System.Windows.Threading.DispatcherPriority.Background);
    }


    private async void UpdateMessageOnServer(ChatMessage message)
    {
        if (session == null || !session.Connected)
        {
            UpdateStatus("⚠️ OPC UA Sunucusuna bağlı değil!", Brushes.Red);
            return;
        }
        session.FetchNamespaceTables(); // Namespace listesini çek

        namespaceIndex = session.NamespaceUris.GetIndex(namespaceUri);
        Console.WriteLine($"🟢 OPC UA Namespace Index Alındı: {namespaceIndex}");

        Console.WriteLine($"🟢 OPC UA Client Namespace Listesi: {string.Join(", ", session.NamespaceUris.ToArray())}");

        try
        {
            // PostgreSQL tag'ını bul
            var messageTag = OpcTags.FirstOrDefault(t => t.TagName.Contains("MessageFromClient"));

            NodeId nodeId;

            if (messageTag != null)
            {
                // ✅ NodeId doğrudan oluşturulmalı
                nodeId = new NodeId(messageTag.TagName, (ushort)namespaceIndex);
            }
            else
            {
                // Varsayılan node'u kullan
                nodeId = new NodeId("MessageFromClient", (ushort)namespaceIndex);
            }

            WriteValue valueToWrite = new WriteValue
            {
                NodeId = nodeId,
                AttributeId = Attributes.Value,
                Value = new DataValue(new Variant(message.Content))
            };

            WriteValueCollection valuesToWrite = new WriteValueCollection { valueToWrite };
            StatusCodeCollection results;
            DiagnosticInfoCollection diagnosticInfos;

            session.Write(null, valuesToWrite, out results, out diagnosticInfos);

            if (results[0] == StatusCodes.Good)
            {
                UpdateStatus("✅ Mesaj başarıyla PostgreSQL'de güncellendi!", Brushes.Green);
                Console.WriteLine($"**Güncellenen İstemci Mesajı:** {message.Content}");
            }
            else
            {
                UpdateStatus($"❌ Mesaj güncelleme hatası: {results[0]}", Brushes.Red);
            }
        }
        catch (Exception ex)
        {
            UpdateStatus($"❌ Mesaj güncelleme hatası: {ex.Message}", Brushes.Red);
        }
    }

    // Mesajı listeye ekle
    private void AddMessage(string content, string sender, bool isOutgoing)
    {
        var message = new ChatMessage
        {
            Content = content,
            Sender = sender,
            Timestamp = DateTime.Now,
            IsOutgoing = isOutgoing
        };
        messageList.Add(message);
    }

    // Durum güncellemesi
    private void UpdateStatus(string message, Brush color)
    {
        Dispatcher.Invoke(() =>
        {
            statusTextBlock.Text = message;
            statusTextBlock.Foreground = color;
        });
    }

    // Config.xml dosyasının varlığını kontrol edip yoksa oluşturan metot
    private async Task EnsureConfigurationFileExists(string filePath)
    {
        if (!File.Exists(filePath))
        {
            UpdateStatus("İstemci yapılandırma dosyası oluşturuluyor...", Brushes.Blue);
            try
            {
                string xmlContent = GetClientConfigXml();
                await File.WriteAllTextAsync(filePath, xmlContent);
                UpdateStatus("Yapılandırma dosyası oluşturuldu.", Brushes.Green);
            }
            catch (Exception ex)
            {
                UpdateStatus($"Yapılandırma dosyası oluşturulamadı: {ex.Message}", Brushes.Red);
            }
        }
    }

    // OPC UA Client XML Yapılandırması
    private string GetClientConfigXml()
    {
        return @"<?xml version=""1.0"" encoding=""utf-8""?>
        <ApplicationConfiguration xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" 
                               xmlns:ua=""http://opcfoundation.org/UA/2008/02/Types.xsd"" 
                               xmlns=""http://opcfoundation.org/UA/SDK/Configuration.xsd"">
            <ApplicationName>OpcUaWpfClient</ApplicationName>
            <ApplicationUri>urn:localhost:OpcUaWpfClient</ApplicationUri>
            <ProductUri>urn:localhost:OpcUaWpfClient</ProductUri>
            <ApplicationType>Client</ApplicationType>
            <SecurityConfiguration>
                <ApplicationCertificate>
                    <StoreType>Directory</StoreType>
                    <StorePath>%LocalApplicationData%/OPC Foundation/CertificateStores/MachineDefault</StorePath>
                    <SubjectName>CN=OpcUaWpfClient</SubjectName>
                </ApplicationCertificate>
                <TrustedPeerCertificates>
                    <StoreType>Directory</StoreType>
                    <StorePath>%LocalApplicationData%/OPC Foundation/CertificateStores/UA Applications</StorePath>
                </TrustedPeerCertificates>
                <TrustedIssuerCertificates>
                    <StoreType>Directory</StoreType>
                    <StorePath>%LocalApplicationData%/OPC Foundation/CertificateStores/UA Certificate Authorities</StorePath>
                </TrustedIssuerCertificates>
                <RejectedCertificateStore>
                    <StoreType>Directory</StoreType>
                    <StorePath>%LocalApplicationData%/OPC Foundation/CertificateStores/RejectedCertificates</StorePath>
                </RejectedCertificateStore>
                <AutoAcceptUntrustedCertificates>true</AutoAcceptUntrustedCertificates>
                <RejectSHA1SignedCertificates>false</RejectSHA1SignedCertificates>
                <MinimumCertificateKeySize>1024</MinimumCertificateKeySize>
            </SecurityConfiguration>
            <TransportQuotas>
                <OperationTimeout>120000</OperationTimeout>
                <MaxStringLength>1048576</MaxStringLength>
                <MaxByteStringLength>1048576</MaxByteStringLength>
                <MaxArrayLength>65535</MaxArrayLength>
                <MaxMessageSize>4194304</MaxMessageSize>
                <MaxBufferSize>65535</MaxBufferSize>
                <ChannelLifetime>600000</ChannelLifetime>
                <SecurityTokenLifetime>3600000</SecurityTokenLifetime>
            </TransportQuotas>
            <ClientConfiguration>
                <DefaultSessionTimeout>60000</DefaultSessionTimeout>
                <MinSubscriptionLifetime>10000</MinSubscriptionLifetime>
            </ClientConfiguration>
        </ApplicationConfiguration>";
    }

    // Uygulama kapatıldığında kaynakları temizle
    protected override void OnClosed(EventArgs e)
    {
        try
        {
            if (subscription != null)
            {
                subscription.Delete(true);
            }

            if (session != null)
            {
                session.Close();
                session.Dispose();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Kaynakları temizlerken hata: {ex.Message}");
        }

        base.OnClosed(e);
    }


    private void EditMessage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button editButton && editButton.CommandParameter is ChatMessage selectedMessage)
        {
            // Kullanıcı yeni mesajı girmeli
            string editedMessage = Microsoft.VisualBasic.Interaction.InputBox(
                "Mesajı düzenle:",
                "Mesaj Düzenleme",
                selectedMessage.Content);

            if (!string.IsNullOrEmpty(editedMessage) && editedMessage != selectedMessage.Content)
            {
                // Yeni mesajı güncelle
                selectedMessage.Content = editedMessage;
                selectedMessage.Timestamp = DateTime.Now;

                // **Mesaj sunucudan mı geldi? İstemciden mi?**
                if (selectedMessage.Sender == "Ben") // **İstemci mesajı**
                {
                    UpdateMessageOnServer(selectedMessage);
                }
                else // **Sunucudan gelen mesaj**
                {
                    UpdateServerMessage(selectedMessage);
                }
                //chatListBox.Items.Refresh();
            }
        }
    }
    private async void UpdateServerMessage(ChatMessage message)
    {
        if (session == null || !session.Connected)
        {
            UpdateStatus("⚠️ OPC UA Sunucusuna bağlı değil!", Brushes.Red);
            return;
        }

        try
        {
            // PostgreSQL tag'ını bul
            var messageTag = OpcTags.FirstOrDefault(t =>
                        t.TagName.Contains("MessageFromServer"));
            NodeId nodeId;

            if (messageTag != null)
            {
                // PostgreSQL'den gelen tag'ı kullan
                nodeId = new NodeId(messageTag.TagName, (ushort)namespaceIndex);
                UpdateStatus($"PostgreSQL server message tag'ı kullanılıyor: {messageTag.TagName}", Brushes.Green);
            }
            else
            {
                // Varsayılan node'u kullan
                nodeId = new NodeId("MessageFromServer", (ushort)namespaceIndex);
                UpdateStatus("Varsayılan server message tag'ı kullanılıyor.", Brushes.Orange);
            }

            WriteValue valueToWrite = new WriteValue
            {
                NodeId = nodeId,
                AttributeId = Attributes.Value,
                Value = new DataValue(new Variant(message.Content))
            };

            WriteValueCollection valuesToWrite = new WriteValueCollection { valueToWrite };
            StatusCodeCollection results;
            DiagnosticInfoCollection diagnosticInfos;

            session.Write(null, valuesToWrite, out results, out diagnosticInfos);

            if (results[0] == StatusCodes.Good)
            {
                UpdateStatus("✅ Sunucu mesajı PostgreSQL'de başarıyla güncellendi!", Brushes.Green);
                Console.WriteLine($"**Güncellenen Sunucu Mesajı:** {message.Content}");
            }
            else
            {
                UpdateStatus($"❌ Sunucu mesajını güncelleme hatası: {results[0]}", Brushes.Red);
            }
        }
        catch (Exception ex)
        {
            UpdateStatus($"❌ Sunucu mesajını güncelleme hatası: {ex.Message}", Brushes.Red);
        }
    }

    private async Task WriteValueToOpcUa(string nodeId, int newValue)
    {
        if (session == null || !session.Connected)
        {
            UpdateStatus("⚠️ OPC UA Sunucusuna bağlı değil!", Brushes.Red);
            return;
        }

        try
        {
            NodeId opcNodeId = new NodeId(nodeId, (ushort)namespaceIndex);

            WriteValue valueToWrite = new WriteValue
            {
                NodeId = opcNodeId,
                AttributeId = Attributes.Value,
                Value = new DataValue(new Variant(newValue))
            };

            WriteValueCollection valuesToWrite = new WriteValueCollection { valueToWrite };
            StatusCodeCollection results;
            DiagnosticInfoCollection diagnosticInfos;

            session.Write(null, valuesToWrite, out results, out diagnosticInfos);

            if (results[0] == StatusCodes.Good)
            {
                UpdateStatus($"✅ {nodeId} başarıyla güncellendi: {newValue}", Brushes.Green);

                // **PostgreSQL Güncelle**
                DatabaseHelper.UpdateTagValue(nodeId, newValue);
            }
            else
            {
                UpdateStatus($"❌ Güncelleme hatası: {results[0]}", Brushes.Red);
            }
        }
        catch (Exception ex)
        {
            UpdateStatus($"❌ Hata: {ex.Message}", Brushes.Red);
        }
    }


    // Sohbet mesajı sınıfı
    public class ChatMessage
    {
        public string Content { get; set; }
        public string Sender { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsOutgoing { get; set; }
    }

    private void EditTag_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button editButton && editButton.CommandParameter is OpcTag selectedTag)
        {
            string editedValue = Microsoft.VisualBasic.Interaction.InputBox(
                $"Yeni değer girin ({selectedTag.TagName}):",
                "Tag Güncelle",
                selectedTag.TagValue.ToString());

            if (int.TryParse(editedValue, out int newValue) && newValue != selectedTag.TagValue)
            {
                selectedTag.TagValue = newValue;  // 🔥 Değer değişti, otomatik güncellenecek

                // ✅ OPC UA Server'a Güncelleme Gönder
                WriteValueToOpcUa(selectedTag.TagName, newValue);
            }
            else
            {
                MessageBox.Show("Geçerli bir sayı giriniz!", "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}