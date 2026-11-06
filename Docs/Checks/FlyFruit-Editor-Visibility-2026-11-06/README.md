# FlyFruit editör görünürlüğü · 2026-11-06

Kullanıcı FlyFruit seçemediğini ve FlyEgg’in yanlış sprite gösterdiğini bildirdi. Önceki doğrulama yalnız stages[0].sprite bağlantısını kontrol etmişti; eski engelden kalmış legacySprite doğru değildi. Unity ilk importta yeni sprite referansı henüz çözümlenmeyince eski baykuş/oyma taşı yedeği kullanılabiliyordu.100–102tanımlarının legacySprite alanları kendi ilk stage sprite’larıyla eşitlendi; kopyadan kalan wardrobeItemSprites temizlendi. Diğer engel tanımları korunur. Hazırlama aracının tanım yazıcısı da düzeltildi.

LevelEditor Obstacle Palette üstüne sürekli görünür üç isimli seçim eklendi: Elma, Sinek Yumurtası, Sinek Kutusu. Engel ara alanı hem Türkçe adı hem FlyFruit/FlyEgg/FlyEggBox enum adını bulur. Elma seçilince Width/Height hemen görünür; mevcut yerleştirme/resize/erase ve otomatik meyve Goals/PreLevel davranışı korunur.

Kural AGENTS.md’ye yazıldı: yeni engel yalnız runtime’da çalışarak tamamlanmış sayılmaz; editörde görülebilir/seçilebilir/yerleştirilebilir/yapılandırılabilir olmalı. Kullanıcı manuel editör/oyun testini kendisi yapar; açıkça istemedikçe bilgisayar/UI kontrolü yapılmaz. Kullanıcı bu tercihi belirtmeden önce UI’dan eski yanlış görseller gözlendi; sonraki düzeltmeler için yeniden UI testi yapılmadı. Kullanıcının231dahil hiçbir leveli değiştirilmedi.

Sonraki ses/boyut değişiklikleri ayrı FlyFruit-Audio-2026-11-06 kaydındadır; eski asset snapshot’larına yeni yetkili değişiklikleri sessizce benimsetmeyin.

## Kullanıcı düzeltmesi: yerleştirmeden önce boyut seçimi

Kullanıcı mevcut elmaların boyutunu sonradan değiştirmenin doğru olmadığını,Safe gibi önceden Width/Height seçip yerleştirmek istediğini belirtti. Toolbar'a Elma modu eklendi;paletten Elma seçmek de bu moda geçer. Width/Genişlik ve Height/Yükseklik sliderları yalnız fırçayı ayarlar. Mevcut elma boyut listesi ve tıklayarak resize yolu kaldırıldı. Tıklanan hücre yeni elmanın sol üst köşesidir.

TryStampFruit bütün dikdörtgeni yazmadan önce doğrular: board sınırı,açık hücre,taban engel/örtü/kasa/mühür/tüp/mıknatıs,kanal ve pinli taş/special korunur. Dolu alanda hiçbir footprint hücresi veya boyut metadata'sı değişmez;inspector uyarı verir. Mevcut elmayı değiştirmek için açıkça silip yeni boyutla yerleştirmek gerekir. Overlay yolunda elma seçili kalmışsa aynı güvenli yerleştirmeye yönlenir. Goals/PreLevel herfootprinti1elma saymaya devam eder. Yeni üretim-yöntemi kontrolleri mevcut katman çözümleyicisiyle boş,örtülü,kısmen dolu,sınır dışı,pinli ve kanallı yerleştirmeyi kapsar. UI çalıştırılmadı;son manuel doğrulama kullanıcıda.
