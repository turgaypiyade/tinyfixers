# Dimerdal — üretim sonunda parçalanma · 2026-11-05

Kullanıcı, önceki gri ve düşük alfa kapanışın KeyGenerator'a benzediğini söyledi ve üretim sonunda kutuların kırılıp yok olmasını önerdi. Bu uygulama gri kapanışın yerini alır. Level düzeni revizyonu değildir.

Son gerekli elmas başarıyla tahtaya yerleştiğinde ortak kotaya bağlı bütün görünür Dimerdal kutuları kaldırılır. `ObstacleStateService.TryRetireDimerdal` mevcut `ClearObstacleFromLevel` yolunu kullanır: bütün footprint hücreleri açılır, alt katman geri yüklenir ve normal yok edilme olayı bir kez gönderilir. Servis mevcut resolve'u yeniden ister; düşüş motoru değişmez. Örtü altındaki kaynak, örtü açıldıktan sonra kaldırılır. İptal/timeout veya yalnız rezervasyon kota tamamlanması sayılmaz.

`GridSpawner.HandleObstacleDestroyed` gerçek renkli `DimerdalView` çerçevesini host yok edilmeden yakalar ve mevcut `ObstacleShatterFx` havuzuna verir. Şeffaf host Image efekt kaynağı değildir. Küçük kutu iki, büyük kutu üç büyük parçaya ayrılır; kısa parlama sonrası parçalar savrularak söner. Gri shader/material artık kullanılmadığı için kaldırıldı. Kabul edilen vuruşun büyüme/silkelenme/parlama tepkisi korunur. Yeni ses veya ses kaynağı eklenmedi.

## Yaşam döngüsü ve maliyet

Emeklilik yalnız kota geçişi, yeni kayıt veya örtü/yok edilme olayı sonrası değerlendirilir. Her kare board taraması yoktur. Kaynak origin listesi yeniden kullanılır; kayıt, senkron alt-katman reveal olayı aynı origin'i yeniden kaydetmeden önce bırakılır. Build/disable epoch kontrolü eski emeklilik turunun yeni board'a devam etmesini engeller; olay aboneliği kaldırılır, mevcut uçuş ve animasyon temizliği sürer.

Mevcut shatter Image havuzu ve sprite kesim önbelleği kullanılır. Mevcut maliyet kırılma başına bir coroutine, üç küçük dizi ve önbellek ilk kullanımında kesilmiş sprite'lardır; sıfır allocation iddiası yoktur. Shatter iterator'üne `finally` eklendi: normal bitiş, açık `Dispose` ile iptal ve kısmi hazırlık hatasında alınmış parçalar/parlama iade edilir. Scene kökleri ve Unity coroutine durdurma davranışı cihazda test edilmedi; izole iterator kontrolü Unity yaşam döngüsünün tamamını kanıtlamaz. CPU/GC/FPS ölçülmedi.

## Doğrulama

- `csi Tools/check_dimerdal_shatter.csx`: 409 mevcut üretim/yerleşim/kota kontrolü; 64 gerçek view/service/uçuş/örtü/iptal kontrolü; 7 gerçek `ClearObstacleFromLevel` footprint/katman/olay sırası ve aynı origin'de yeniden kaynak kaydı kontrolü. Gerçek shatter iterator'üyle normal bitiş, açık iptal, kısmi hazırlık temizliği ve nesne tekrar kullanımı kontrol edildi. GridSpawner kaynak yakalama sırası da incelendi.
- `csi Tools/check_dimerdal_matching.csx`: 39 gerçek eşleşme/special oluşturma/toplama kontrolü ve mevcut 409 regresyon kontrolü geçti.
- `csi Tools/check_dimerdal_collection.csx`: gerçek ortak TarpStatue/AncientSeal toplama rutini, havuzlu görseller ve Dimerdal sürücüsü birlikte geçti.
- `protected-levels.json`: bu tur başlangıcındaki 1.067 Settings/level/ad dosyasının hash'i. Bütün level assetleri, katalog, isimler ve ObstacleLibrary aynı kalmalıdır. `owned-runtime-hashes.json` bu talebin beş runtime dosyasını sabitler; 225 v2 statik doğrulayıcısı yalnız bu açık hash'leri sonraki yetkili runtime revizyonu olarak kabul eder.

Testler gerçek C# yöntemlerini Unity doubles ile çalıştırır. Unity build/Play, cihaz testi, bot veya gravity/cascade simülasyonu, yayın, commit veya release baseline yapılmadı. 225 v2 düzeni ve 222–224 görsel onayı korunur. Animasyonun cihaz görünümü ve oyun dengesi kullanıcı tarafından henüz değerlendirilmedi.
