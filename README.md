# OPC UA Çözümü

Bu proje, OPC UA protokolünü kullanarak endüstriyel otomasyon sistemleri için bir sunucu-istemci çözümü sunar. PostgreSQL veritabanı ile entegre çalışarak, endüstriyel verilerin gerçek zamanlı izlenmesini ve kontrolünü sağlar.

## Proje Yapısı

Çözüm aşağıdaki projelerden oluşmaktadır:

### OpcUaServer

OPC UA protokolünü kullanan bir sunucu uygulaması. Veritabanındaki etiketleri (tag) OPC UA düğümleri olarak sunar ve istemcilerden gelen bağlantıları kabul eder.

- **Program.cs**: Sunucu yapılandırması ve başlatma işlemlerini içerir
- **MyServer.cs**: OPC UA sunucu sınıfı
- **MyNodeManager.cs**: OPC UA adres uzayını ve düğümleri yöneten sınıf
- **OpcUaServer.Config.xml**: Sunucu yapılandırma dosyası

### OpcUaClient

Komut satırı tabanlı OPC UA istemci uygulaması.

- **Program.cs**: İstemci uygulamasının ana kodu
- **OpcUaClient.Config.xml**: İstemci yapılandırma dosyası

### OpcUaClientWPF

Grafiksel kullanıcı arayüzüne sahip OPC UA istemci uygulaması.

- **MainWindow.xaml/MainWindow.xaml.cs**: Ana pencere ve uygulama mantığı
- **Converters.cs**: XAML veri dönüşümleri için yardımcı sınıflar

### OPCCommonLibrary

Sunucu ve istemci uygulamaları tarafından paylaşılan ortak kod kütüphanesi.

- **OpcTag.cs**: OPC etiketlerini temsil eden veri modeli
- **DatabaseHelper.cs**: PostgreSQL veritabanı işlemleri için yardımcı sınıf

## Özellikler

- OPC UA protokolü üzerinden güvenli iletişim
- PostgreSQL veritabanı entegrasyonu
- Gerçek zamanlı veri izleme ve kontrol
- Sohbet benzeri mesajlaşma özelliği
- Etiket (tag) değerlerinin izlenmesi ve değiştirilmesi
- WPF tabanlı modern kullanıcı arayüzü

## Gereksinimler

- .NET Framework veya .NET Core
- PostgreSQL veritabanı
- OPC UA kütüphaneleri (OPC Foundation UA .NET Standard)

## Kurulum

1. PostgreSQL veritabanını kurun ve aşağıdaki bağlantı bilgilerini kullanarak yapılandırın:

   ```
   Host: localhost
   Port: 5432
   Username: postgres
   Password: 123456
   Database: OPCUABase
   Schema: TESASch
   ```

2. Veritabanında gerekli tabloları oluşturun:

   ```sql
   CREATE SCHEMA IF NOT EXISTS "TESASch";
   CREATE TABLE IF NOT EXISTS "TESASch"."comp_tag_dtl" (
       "id" SERIAL PRIMARY KEY,
       "TagName" VARCHAR(100) NOT NULL,
       "TagValue" INTEGER NOT NULL
   );
   ```

3. Çözümü Visual Studio'da açın ve derleyin.

## Kullanım

1. Önce OpcUaServer uygulamasını başlatın.
2. Ardından OpcUaClientWPF uygulamasını başlatın.
3. İstemci otomatik olarak sunucuya bağlanacak ve mevcut etiketleri (tag) listeleyecektir.
4. Etiket değerlerini değiştirmek için ilgili etikete tıklayın ve yeni değer girin.
5. Mesajlaşma özelliğini kullanmak için mesaj kutusuna metin girin ve gönder düğmesine tıklayın.

## Güvenlik

Uygulama, OPC UA protokolünün güvenlik özelliklerini kullanır:

- Sertifika tabanlı kimlik doğrulama
- Mesaj şifreleme
- İmzalama

## Lisans

Bu proje [MIT Lisansı](LICENSE) altında lisanslanmıştır. Daha fazla bilgi için `LICENSE` dosyasını inceleyebilirsiniz.

MIT Lisansı, yazılımı özgürce kullanma, değiştirme ve dağıtma hakkı verir, ancak orijinal telif hakkı ve lisans bildirimlerinin korunması gerekir.
