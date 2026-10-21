# ColorCrate normal Obstacle fırçası

Kullanıcının Overlay itirazı üzerine başlangıç kalan vuruşu normal Obstacle paletine eklendi. 1/2/3 seçimi aşama sprite’ını önizler; aynı sandığa yeniden tıklamak başlangıç durumunu günceller. Ortak kütüphane kuralları kapalı foldout altındadır.

- Normal sandık verisi: `LevelData.colorCrateInitialHits`. Eksik/boş liste eski varsayılan davranışını korur. Varsayılan tam HP için ek kayıt tutulmaz. Silme/üzerine boyama kaydı temizler; obstacle dizisi boyut nedeniyle sıfırlanınca başlangıç verisi de sıfırlanır.
- Mevcut katmanlı sandık: aynı origin üzerindeki en üst generic entry aynı türse Obstacle fırçası yalnız başlangıç HP değerini günceller. Altındaki Mud ve stack order korunur. Gerçek yeni üst katmanlar Overlay ile eklenmeye devam eder.
- Runtime: başlangıç kayıtları initialization sırasında mevcut dictionary’ye bir kez alınır; yeniden kurulumda dictionary temizlenip kullanılır. Yeni Update/efekt/GameObject ya da hit başına liste/board taraması eklenmedi. Editor authoring sırasında liste kopyalama vardır. Performans profillenmedi.
- Önceki üç kaynak dosyası `Before/` içinde korunur. Bu değişiklik hiçbir level assetini veya ortak library’yi değiştirmez.

Doğrulama: `validation.json` içinde 2.406 korunan dosyanın hash’leri aynı; üç kaynağın lexical delimiter kontrolü ve diff whitespace kontrolü geçti. Kaynak akışı incelendi; normal/katmanlı başlangıç değerleri aynı runtime lookup’a bağlanır ve editor stage sprite’ı seçilen HP ile çizilir. Bunlar Unity derleme veya etkileşimli editör testi değildir. Kullanıcının talebi olmadığı için Unity compilation, Play, build ve bot/sim çalıştırılmadı.
