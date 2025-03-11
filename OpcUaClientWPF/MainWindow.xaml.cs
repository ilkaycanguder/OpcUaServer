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

        // UI başlatıldıktan sonra OPC UA istemcisini başlat
        Loaded += async (s, e) => await InitializeOpcUaClient();
    }
    public ObservableCollection<ChatMessage> MessageList => messageList;
    private async Task GetOpcUaNodes()
    {
        if (session == null || !session.Connected)
        {
            UpdateStatus("⚠️ OPC UA Sunucusuna bağlı değil!", Brushes.Red);
            return;
        }

        try
        {
            OpcTags.Clear();
            UpdateStatus("PostgreSQL veritabanından OPC UA tag'ları alınıyor...", Brushes.Blue);
            // Önce özel namespace'i kontrol et
            // 🔥 Server’dan tag listesini al
            var serverTags = DatabaseHelper.GetTagsFromDatabase();

            foreach (var tag in serverTags)
            {
                OpcTags.Add(tag);
            }

            tagsListView.ItemsSource = OpcTags;
            UpdateStatus($"✅ {OpcTags.Count} adet OPC UA tag'ı başarıyla alındı.", Brushes.Green);
        }
        catch (Exception ex)
        {
            UpdateStatus($"⚠️ OPC UA Node Okuma Hatası: {ex.Message}", Brushes.Red);
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

            // ✅ NodeId'yi doğru şekilde oluştur
            NodeId nodeId = new NodeId(tag.TagName, (ushort)namespaceIndex);

            // Eğer bu tag zaten izleniyorsa tekrar ekleme
            if (monitoredItems.ContainsKey(tag.TagName)) return;

            // Yeni monitored item oluştur
            MonitoredItem monitoredItem = new MonitoredItem(subscription.DefaultItem)
            {
                DisplayName = tag.TagName,
                StartNodeId = nodeId,
                AttributeId = Attributes.Value,
                SamplingInterval = 1000,
                QueueSize = 10,
                DiscardOldest = true
            };

            // Değer değişikliğinde tetiklenecek event
            monitoredItem.Notification += (item, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (e.NotificationValue is MonitoredItemNotification notification && notification.Value?.Value != null)
                    {
                        // Tag değerini güncelle
                        var existingTag = OpcTags.FirstOrDefault(t => t.TagName == tag.TagName);
                        if (existingTag != null)
                        {
                            existingTag.TagValue = notification.Value.Value != null ? Convert.ToInt32(notification.Value.Value) : 0;
                            existingTag.LastUpdate = DateTime.Now;

                            // **Veritabanını Güncelle!**
                            DatabaseHelper.UpdateTagValue(existingTag.TagName, existingTag.TagValue);
                        }
                    }
                    tagsListView.Items.Refresh();
                });
            };

            // Subscription'a ekle
            subscription.AddItem(monitoredItem);
            monitoredItems[tag.TagName] = monitoredItem;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"MonitoredItem ekleme hatası: {ex.Message}");
        }
    }

    private async Task InitializeOpcUaClient()
    {
        try
        {
            UpdateStatus("OPC UA İstemcisi başlatılıyor...", Brushes.Blue);

            // Yapılandırma dosyasının yolu
            string configFilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OpcUaClient.Config.xml");

            // Config.xml dosyasını oluştur
            await EnsureConfigurationFileExists(configFilePath);

            // OPC UA uygulama örneği
            ApplicationInstance application = new ApplicationInstance
            {
                ApplicationName = "OpcUaWpfClient",
                ApplicationType = ApplicationType.Client
            };

            // XML yapılandırmasını yükle
            ApplicationConfiguration config = await application.LoadApplicationConfiguration(configFilePath, silent: false);
            if (config == null)
            {
                throw new Exception("Yapılandırma dosyası yüklenemedi!");
            }

            // ApplicationType'ı kontrol et
            if (config.ApplicationType != ApplicationType.Client)
            {
                UpdateStatus("ApplicationType hatalı, Client olarak güncellendi.", Brushes.Orange);
                config.ApplicationType = ApplicationType.Client;
            }

            // Sertifikayı kontrol et veya oluştur
            bool certOK = await application.CheckApplicationInstanceCertificate(false, 2048);
            if (!certOK)
            {
                UpdateStatus("Sertifika kontrol ediliyor...", Brushes.Orange);
                certOK = await application.CheckApplicationInstanceCertificate(true, 2048);
                if (!certOK)
                {
                    throw new Exception("İstemci sertifikası oluşturulamadı veya geçersiz!");
                }
            }

            application.ApplicationConfiguration = config;

            // OPC UA Sunucusuna Bağlan
            UpdateStatus($"PostgreSQL veritabanına bağlı OPC UA sunucusuna bağlanılıyor: {ServerUrl}", Brushes.Blue);

            // Endpoint seç
            var endpoint = CoreClientUtils.SelectEndpoint(ServerUrl, false, 15000);
            var configEndpoint = new ConfiguredEndpoint(null, endpoint, EndpointConfiguration.Create(config));

            // Oturum oluştur
            if (namespaceIndex == -1)
            {
                Console.WriteLine("⚠️ Namespace Index -1! Client bağlantısı yenileniyor...");

                //session.Close(); // Mevcut oturumu kapat
                session = await Session.Create(
                    config,
                    configEndpoint,
                    false,
                    "OpcUaWpfClient",
                    60000,
                    new UserIdentity(new AnonymousIdentityToken()),
                    null
                );

                session.FetchNamespaceTables(); // Namespace listesini yeniden çek
                namespaceIndex = session.NamespaceUris.GetIndex(namespaceUri);
                Console.WriteLine($"🟢 OPC UA Namespace Index Yenilendi: {namespaceIndex}");
            }


            UpdateStatus("OPC UA sunucusuna bağlandı! PostgreSQL veritabanı entegrasyonu kontrol ediliyor...", Brushes.Green);


            // Subscription oluştur
            subscription = new Subscription(session.DefaultSubscription)
            {
                PublishingInterval = 1000,
                KeepAliveCount = 10,
                LifetimeCount = 10000,
                PublishingEnabled = true
            };

            session.AddSubscription(subscription);
            subscription.Create();
            await GetOpcUaNodes();

            // Sunucu mesajlarını izle (ChatMessage tag'ını bul)
            var messageTag = OpcTags.FirstOrDefault(t =>
                t.TagName.Contains("MessageFromServer"));

            if (messageTag != null)
            {
                UpdateStatus($"Sunucu mesaj tag'ı bulundu: {messageTag.TagName}", Brushes.Green);
            }
            else
            {
                // Varsayılan mesaj izlemeyi ekle
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
                UpdateStatus("Varsayılan sunucu mesajları dinleniyor...", Brushes.Green);
            }

            // Değişiklikleri uygula
            subscription.ApplyChanges();
        }
        catch (Exception ex)
        {
            UpdateStatus($"Hata: {ex.Message}", Brushes.Red);
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

    // Mesaj gönder butonuna tıklama
    private async void SendButton_Click(object sender, RoutedEventArgs e)
    {
        if (session == null || !session.Connected)
        {
            UpdateStatus("Sunucuya bağlı değil!", Brushes.Red);
            return;
        }

        //string message = messageTextBox.Text.Trim();
        //if (string.IsNullOrEmpty(message))
        //{
        //    return;
        //}

        //try
        //{
        //    await SendMessageToServer(message);
        //    AddMessage(message, "Ben", true);
        //    messageTextBox.Clear();
        //}
        //catch (Exception ex)
        //{
        //    UpdateStatus($"Mesaj gönderme hatası: {ex.Message}", Brushes.Red);
        //}
    }

    // Sunucuya mesaj gönder
    //private async Task SendMessageToServer(string message)
    //{
    //    try
    //    {
    //        if (session == null || !session.Connected)
    //        {
    //            UpdateStatus("⚠️ OPC UA Sunucusuna bağlı değil!", Brushes.Red);
    //            return;
    //        }

    //        var messageTag = OpcTags.FirstOrDefault(t =>
    //         t.TagName.Contains("MessageFromClient"));

    //        NodeId nodeId = messageTag != null ? new NodeId(messageTag.TagName, (ushort)namespaceIndex)
    //                                              : new NodeId("MessageFromClient", (ushort)namespaceIndex);

    //        UpdateStatus($"PostgreSQL message tag'ı kullanılıyor: {messageTag?.TagName ?? "Varsayılan"}", Brushes.Green);

    //        // ✅ Değer yazma işlemi
    //        WriteValue valueToWrite = new WriteValue
    //        {
    //            NodeId = nodeId,
    //            AttributeId = Attributes.Value,
    //            Value = new DataValue(new Variant(message))
    //        };

    //        WriteValueCollection valuesToWrite = new WriteValueCollection { valueToWrite };
    //        StatusCodeCollection results;
    //        DiagnosticInfoCollection diagnosticInfos;

    //        session.Write(null, valuesToWrite, out results, out diagnosticInfos);

    //        if (results == null || results.Count == 0)
    //        {
    //            UpdateStatus("❌ Mesaj gönderme hatası: Sonuç boş!", Brushes.Red);
    //        }
    //        else if (results[0] != StatusCodes.Good)
    //        {
    //            UpdateStatus($"❌ Mesaj gönderme hatası: {results[0]}", Brushes.Red);
    //        }
    //        else
    //        {
    //            UpdateStatus("✅ Mesaj başarıyla PostgreSQL veritabanına gönderildi!", Brushes.Green);
    //        }
    //    }
    //    catch (Exception ex)
    //    {
    //        UpdateStatus($"❌ Mesaj gönderme hatası: {ex.Message}", Brushes.Red);
    //    }
    //}
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
        //chatListBox.ScrollIntoView(message);
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

    private void messageTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            SendButton_Click(sender, e);
        }
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
        Console.WriteLine($"🟡 OPC UA Namespace Listesi: {string.Join(", ", session.NamespaceUris.ToArray())}");

        try
        {
            NodeId opcNodeId = new NodeId(nodeId, (ushort)namespaceIndex);

            WriteValue valueToWrite = new WriteValue
            {
                NodeId = opcNodeId,
                AttributeId = Attributes.Value,
                Value = new DataValue(new Variant(newValue))  // Send as integer
            };

            WriteValueCollection valuesToWrite = new WriteValueCollection { valueToWrite };
            StatusCodeCollection results;
            DiagnosticInfoCollection diagnosticInfos;

            session.Write(null, valuesToWrite, out results, out diagnosticInfos);
            Console.WriteLine($"🟢 OPC UA NodeId: {opcNodeId.Identifier}, NamespaceIndex: {namespaceIndex}");

            Console.WriteLine($"🟡 OPC UA Sunucusuna yazma denemesi: {nodeId} = {newValue}");

            if (results.Count > 0)
            {
                Console.WriteLine($"🔴 OPC UA Yazma Hatası! NodeId: {nodeId}, Hata Kodu: {results[0]}");
            }
            else
            {
                Console.WriteLine("⚠️ OPC UA Yazma başarısız ama results boş!");
            }
            if (results[0] == StatusCodes.Good)
            {
                UpdateStatus($"✅ {nodeId} başarıyla güncellendi: {newValue}", Brushes.Green);
                // Also update in database
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
                selectedTag.TagValue = newValue;
                WriteValueToOpcUa(selectedTag.TagName, newValue);

                // Update UI to show the change
                UpdateStatus($"✅ {selectedTag.TagName} değeri {newValue} olarak güncelleniyor...", Brushes.Green);
                selectedTag.LastUpdate = DateTime.Now;
                tagsListView.Items.Refresh();
            }
            else
            {
                MessageBox.Show("Geçerli bir sayı giriniz!", "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    //private void addTagButton_Click(object sender, RoutedEventArgs e)
    //{
    //    string tagName = newTagNameTextBox.Text.Trim();
    //    string tagValueString = newTagValueTextBox.Text.Trim(); // Yeni değer

    //    if (string.IsNullOrEmpty(tagName) || string.IsNullOrEmpty(tagValueString))
    //    {
    //        MessageBox.Show("Lütfen geçerli bir Tag Name ve Tag Value giriniz.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
    //        return;
    //    }

    //    if (!int.TryParse(tagValueString, out int newTagValue))
    //    {
    //        MessageBox.Show("Lütfen Tag Value için geçerli bir sayı giriniz.", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
    //        return;
    //    }

    //    //DatabaseHelper.InsertNewTag(tagName, newTagValue);

    //    // ✅ UI Güncelleme
    //    OpcTags.Add(new OpcTag { TagName = tagName, TagValue = newTagValue, LastUpdate = DateTime.Now });

    //    // ✅ UI temizleme
    //    newTagNameTextBox.Clear();
    //    newTagValueTextBox.Clear();
    //}
}