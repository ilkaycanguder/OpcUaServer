OPC UA Çözümü
Bu proje, OPC UA protokolünü kullanarak endüstriyel otomasyon sistemleri için bir sunucu-istemci çözümü sunar. OPC UA sunucusu, istemciler için gerçek zamanlı veri erişimi ve yönetimi sağlar.
Proje Yapısı
Çözüm aşağıdaki projelerden oluşmaktadır:
OpcUaServer
OPC UA protokolünü kullanan bir sunucu uygulaması. İstemcilerden gelen bağlantıları kabul eder ve verileri yönlendirir.

Program.cs: Sunucu yapılandırması ve başlatma işlemlerini içerir
MyServer.cs: OPC UA sunucu sınıfı
MyNodeManager.cs: OPC UA adres uzayını ve düğümleri yöneten sınıf
OpcUaServer.Config.xml: Sunucu yapılandırma dosyası

OpcUaClient
Komut satırı tabanlı OPC UA istemci uygulaması.

Program.cs: İstemci uygulamasının ana kodu
OpcUaClient.Config.xml: İstemci yapılandırma dosyası

OpcUaClientWPF
Grafiksel kullanıcı arayüzüne sahip OPC UA istemci uygulaması.

MainWindow.xaml/MainWindow.xaml.cs: Ana pencere ve uygulama mantığı
Converters.cs: XAML veri dönüşümleri için yardımcı sınıflar

OPCCommonLibrary
Sunucu ve istemci uygulamaları tarafından paylaşılan ortak kod kütüphanesi.

OpcTag.cs: OPC etiketlerini temsil eden veri modeli

Özellikler

OPC UA protokolü üzerinden güvenli iletişim
Gerçek zamanlı veri izleme ve kontrol
Sohbet benzeri mesajlaşma özelliği
Etiket (tag) değerlerinin izlenmesi ve değiştirilmesi
WPF tabanlı modern kullanıcı arayüzü
Client_1 yalnızca okuma (READ) erişimine sahiptir.
Client_2 okuma ve yazma (READ/WRITE) erişimine sahiptir.

Gereksinimler

.NET Framework veya .NET Core
OPC UA kütüphaneleri (OPC Foundation UA .NET Standard)

Kurulum

Çözümü Visual Studio'da açın ve derleyin.
Sunucu ve istemci yapılandırma dosyalarını ihtiyaçlarınıza göre düzenleyin.

Kullanım

Önce OpcUaServer uygulamasını başlatın.
Ardından OpcUaClientWPF veya OpcUaClient uygulamasını başlatın.
İstemci otomatik olarak sunucuya bağlanacak ve mevcut etiketleri (tag) listeleyecektir.
Client_1, yalnızca veri okuyabilir. Client_2, hem okuyabilir hem de yazabilir.
Mesajlaşma özelliğini kullanmak için mesaj kutusuna metin girin ve gönder düğmesine tıklayın.

Güvenlik
Uygulama, OPC UA protokolünün güvenlik özelliklerini kullanır:

Sertifika tabanlı kimlik doğrulama
Mesaj şifreleme
İmzalama

Şu anda Anonymous (Anonim) erişim etkin durumdadır. Kullanıcı doğrulama mekanizması eklemek için MyServer.cs dosyasında ilgili kodları değiştirebilirsiniz.
Lisans
Bu proje MIT Lisansı altında lisanslanmıştır. Daha fazla bilgi için LICENSE dosyasını inceleyebilirsiniz.
MIT Lisansı, yazılımı özgürce kullanma, değiştirme ve dağıtma hakkı verir, ancak orijinal telif hakkı ve lisans bildirimlerinin korunması gerekir.
