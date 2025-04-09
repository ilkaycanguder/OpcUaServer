OPC UA Çözümü
Bu proje, OPC UA protokolünü kullanarak endüstriyel otomasyon sistemleri için bir sunucu-istemci çözümü sunar. OPC UA sunucusu, istemciler için gerçek zamanlı veri erişimi ve yönetimi sağlar.

Proje Yapısı
Çözüm aşağıdaki projelerden oluşmaktadır:

OpcUaServer
OPC UA protokolünü kullanarak endüstriyel otomasyon sistemleri için bir sunucu uygulaması.

Program.cs: Sunucu yapılandırması ve başlatma işlemlerini içerir
MyServer.cs: OPC UA sunucu sınıfı
MyNodeManager.cs: OPC UA adres uzayını ve düğümleri yöneten sınıf
OpcUaServer.Config.xml: Sunucu yapılandırma dosyası

OpcUaClient
Komut satırı tabanlı OPC UA istemci uygulaması. (Geliştirme aşamasında)

Program.cs: İstemci uygulamasının ana kodu
OpcUaClient.Config.xml: İstemci yapılandırma dosyası

OpcUaClientWPF
Grafiksel kullanıcı arayüzüne sahip OPC UA istemci uygulaması. (Geliştirme aşamasında)

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
WPF tabanlı modern kullanıcı arayüzü (Geliştirme aşamasında)
Otomatik oturum yönetimi ve client takibi
Detaylı loglama ve hata izleme
Rol tabanlı erişim kontrolü

Gereksinimler

.NET Framework veya .NET Core
OPC UA kütüphaneleri (OPC Foundation UA .NET Standard)
OpenSSL (sertifika oluşturmak için)
UaExpert (test için)

Kurulum

1. Çözümü Visual Studio'da açın ve derleyin.
2. Sertifika yapılandırması:
   - OpenSSL kullanarak sertifika oluşturun:
     ```
     openssl req -x509 -newkey rsa:2048 -keyout server_key.pem -out server_cert.pem -days 365 -nodes
     ```
   - Sertifikaları DER formatına dönüştürün:
     ```
     openssl x509 -outform der -in server_cert.pem -out server_cert.der
     ```
   - Sertifikaları uygulama dizinine kopyalayın
3. Sunucu yapılandırma dosyasını ihtiyaçlarınıza göre düzenleyin.

Kullanım

1. Önce OpcUaServer uygulamasını başlatın.
2. UaExpert ile test edin:
   - Yeni bir sunucu bağlantısı ekleyin
   - URL: `opc.tcp://localhost:4840/UA/OpcUaServer`
   - Security Mode: None
   - Security Policy: None
   - User Identity: Anonymous
3. Tag'leri görüntüleyin ve test edin.

Not: OpcUaClientWPF ve OpcUaClient uygulamaları şu anda geliştirme aşamasındadır. Testler için UaExpert kullanmanız önerilir.

Rol Tabanlı Erişim Kontrolü

1. Kullanıcı Rolleri:

   - Admin: Tüm yetkilere sahip
   - Guest: Sadece okuma yetkisine sahip
   - Operator: Okuma ve yazma yetkisine sahip

2. Yetki Seviyeleri:

   - Admin:
     - Tüm tagları okuyabilir ve yazabilir
     - Yeni tag ekleyebilir
     - Tag silebilir
     - Kullanıcı yönetimi yapabilir
   - Operator:
     - Tüm tagları okuyabilir
     - Belirli taglara yazabilir
     - Tag değerlerini değiştirebilir
   - Guest:
     - Tagları sadece okuyabilir
     - Değer değiştiremez

3. Kimlik Doğrulama:
   - Kullanıcı adı/şifre ile giriş
   - Rol bazlı yetkilendirme
   - Oturum yönetimi

Güvenlik

Uygulama, OPC UA protokolünün güvenlik özelliklerini kullanır:

1. Sertifika Yapılandırması:

   - OpenSSL ile oluşturulan özel sertifikalar
   - DER formatında sertifika desteği
   - Otomatik sertifika doğrulama

2. Güvenlik Modları:

   - None: Güvenlik olmadan bağlantı
   - Sign: Mesaj imzalama
   - SignAndEncrypt: Mesaj imzalama ve şifreleme

3. Kimlik Doğrulama:

   - Anonymous (Anonim) erişim
   - Kullanıcı adı/şifre doğrulaması
   - Sertifika tabanlı doğrulama
   - Rol tabanlı yetkilendirme

4. Oturum Yönetimi:
   - Maksimum oturum süresi: 1 saat
   - Maksimum istek yaşı: 10 dakika
   - Otomatik oturum temizleme
   - Rol bazlı oturum kontrolü

UaExpert ile Test Etme

1. UaExpert'te yeni bir sunucu bağlantısı ekleyin:

   - URL: `opc.tcp://localhost:4840/UA/OpcUaServer`
   - Security Mode: None
   - Security Policy: None
   - User Identity: Anonymous

2. Sertifika yönetimi:

   - UaExpert'te "Settings" → "Manage Certificate Trust Lists..."
   - "Trusted Certificate Authorities" sekmesine sertifikanızı ekleyin
   - "Trusted Certificates" sekmesine sertifikanızı ekleyin

3. Bağlantı sorunları yaşarsanız:

   - Sertifikaları yeniden oluşturun
   - UaExpert'i yeniden başlatın
   - Güvenlik ayarlarını "None" olarak ayarlayın

4. Test Adımları:
   - Sunucuya bağlanın
   - Tag'leri görüntüleyin
   - Değerleri okuyun
   - Değerleri yazın (yetkiniz varsa)
   - Rol bazlı erişim kontrolünü test edin

Lisans
Bu proje MIT Lisansı altında lisanslanmıştır. Daha fazla bilgi için LICENSE dosyasını inceleyebilirsiniz.
MIT Lisansı, yazılımı özgürce kullanma, değiştirme ve dağıtma hakkı verir, ancak orijinal telif hakkı ve lisans bildirimlerinin korunması gerekir.
