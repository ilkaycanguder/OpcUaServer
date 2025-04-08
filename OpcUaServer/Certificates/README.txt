OPC UA Server için SSL Sertifikası Kullanımı
================================================

Bu dizin OPC UA Server için kullanılacak özel SSL sertifikalarını barındırır.

Özel sertifika kullanmak için:

1. Sertifika Formatı:
   - Sertifikanızın PKCS#12 (.pfx) formatında olması gerekmektedir.
   - Sertifika adınızı "your_certificate.pfx" olarak değiştirin veya kod içinde belirtilen yolu güncelleyin.

2. Sertifika Oluşturma:
   - Geliştirme/test için OpenSSL kullanarak kendi kendine imzalı bir sertifika oluşturabilirsiniz:
     ```
     openssl req -x509 -newkey rsa:2048 -keyout key.pem -out cert.pem -days 365
     openssl pkcs12 -export -out your_certificate.pfx -inkey key.pem -in cert.pem
     ```

   - Üretim ortamı için güvenilir bir Sertifika Otoritesinden (CA) sertifika almanız önerilir.

3. Kodda Yapılması Gereken Düzenlemeler:
   - Program.cs dosyasında bulunan "certificatePassword" değişkenini sertifikanızın şifresiyle güncelleyin.
   - Özel bir sertifika adı kullanıyorsanız "customCertificatePath" yolunu da güncellemeniz gerekir.

4. Sertifika İzinleri:
   - Windows sistemlerde, sertifika dosyasına OPC UA Server'ın çalıştığı kullanıcının okuma izni olduğundan emin olun.

5. Uyarılar:
   - Sertifika şifrelerini kod içinde saklamak güvenlik riski oluşturabilir. Üretim ortamında şifreleri dış bir 
     yapılandırma dosyasından veya güvenli bir saklama mekanizmasından yüklemeyi düşünün.
   - Özel sertifika bulunamadığında otomatik olarak kendi kendine imzalı bir sertifika oluşturulacaktır.

6. Güvenli İletişim:
   - OPC UA istemcilerinin sunucuya güvenli bir şekilde bağlanabilmesi için, sertifikanızın istemci tarafından güvenilir
     olarak kabul edilmesi gerekmektedir.
   - Kendi kendine imzalı sertifikalar için, istemci tarafında sertifikayı manuel olarak güvenilir hale getirmeniz gerekebilir. 