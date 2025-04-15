# OPC UA Sunucu

Bu proje, OPC UA protokolünü kullanarak endüstriyel otomasyon sistemleri için sunucu uygulaması sunar. OPC UA sunucusu, istemciler için gerçek zamanlı veri erişimi ve yönetimi sağlar.

## Proje Yapısı

OpcUaServer projesi aşağıdaki dosyalardan oluşmaktadır:

- **Program.cs**: Sunucu yapılandırması ve başlatma işlemlerini içerir
- **MyServer.cs**: OPC UA sunucu sınıfı
- **MyNodeManager.cs**: OPC UA adres uzayını ve düğümleri yöneten sınıf
- **OpcUaServer.Config.xml**: Sunucu yapılandırma dosyası

## Özellikler

- OPC UA protokolü üzerinden güvenli iletişim
- Gerçek zamanlı veri izleme ve kontrol
- Tag değerlerinin izlenmesi ve değiştirilmesi
- Otomatik oturum yönetimi ve client takibi
- Detaylı loglama ve hata izleme
- Rol tabanlı erişim kontrolü
- SSL sertifika desteği

## Gereksinimler

- .NET Framework veya .NET Core
- OPC UA kütüphaneleri (OPC Foundation UA .NET Standard)
- OpenSSL (sertifika oluşturmak için)
- UaExpert veya başka OPC UA istemcileri (test için)

## Kurulum

1. Projeyi Visual Studio'da açın ve derleyin.
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

## Kullanım

1. OpcUaServer uygulamasını başlatın.
2. Herhangi bir OPC UA istemcisi (UaExpert, UAModeler, Prosys vb.) ile test edin:
   - Yeni bir sunucu bağlantısı ekleyin
   - URL: `opc.tcp://localhost:4840/UA/OpcUaServer`
   - Security Mode: None
   - Security Policy: None
   - User Identity: Anonymous veya kullanıcı adı/şifre
3. Tag'leri görüntüleyin ve test edin.

## Rol Tabanlı Erişim Kontrolü

1. Kullanıcı Rolleri:

   - Admin: Tüm yetkilere sahip
   - Guest: Sadece okuma yetkisine sahip

2. Yetki Seviyeleri:

   - Admin:

     - Tüm tagları okuyabilir ve yazabilir
     - Yeni tag ekleyebilir
     - Tag silebilir
     - Kullanıcı yönetimi yapabilir

   - Guest:
     - Tagları sadece okuyabilir
     - Değer değiştiremez

3. Kimlik Doğrulama:
   - Kullanıcı adı/şifre ile giriş
   - Rol bazlı yetkilendirme
   - Oturum yönetimi

## Güvenlik

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

## OPC UA İstemcileri ile Test Etme

1. UaExpert ile Test:

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
   - İstemci uygulamasını yeniden başlatın
   - Güvenlik ayarlarını "None" olarak ayarlayın

4. Test Adımları:
   - Sunucuya bağlanın
   - Tag'leri görüntüleyin
   - Değerleri okuyun
   - Değerleri yazın (yetkiniz varsa)
   - Rol bazlı erişim kontrolünü test edin

## Lisans

Bu proje MIT Lisansı altında lisanslanmıştır. Daha fazla bilgi için LICENSE dosyasını inceleyebilirsiniz.
MIT Lisansı, yazılımı özgürce kullanma, değiştirme ve dağıtma hakkı verir, ancak orijinal telif hakkı ve lisans bildirimlerinin korunması gerekir.
