# Wall / MetalWall — elle Overlay boyama

2026-11-06. Kullanıcı Wall'ı editörde elle Overlay olarak kuramadığını bildirdi. Diğer Overlay engellerini çalışan örnek olarak verdiğini netleştirdi; kapsam Wall/MetalWall fırçası ve bu fırçanın diğer kayıtları korumasıdır. Level tasarımı veya oyun mekaniği değişikliği değildir.

## Kullanım

1. **Obstacle → Lantern** ile alttaki fenerleri yerleştir.
2. **Overlay → Wall** veya **MetalWall** seç.
3. **Parça No** belirle, **Duvar ekle** seçeneğiyle hücrelere tıkla veya sol tuşla sürükle. Aynı numaralı, aynı türde ve kenardan bitişen hücreler birlikte yıkılır. Çapraz temas birleşmez.
4. Ayrı yıkılacak grup için **Yeni Parça** kullan. Mevcut duvarı başka numarayla yeniden boyamak grubunu değiştirir.
5. Altındaki feneri korumak için **Yalnız duvarı sil** seçeneğini kullan. Genel **Erase** tüm hücreyi temizleyen eski araçtır.
6. Wall hedefini eklediğinde parça sayısı önerilir. Daha sonra yolu değiştirirsen **Goals → Wall → Eşitle** ile hedefi güncelleyebilirsin.

Duvar hücreleri artık numaralı ve bağlı kenarlı çizilir; köşedeki küçük pencere alttaki feneri gösterir. Üstünde opak başka bir engel varsa duvar onun üstüne çizilmez. Mevcut diğer Overlay rozetleri korunur.

## Düzeltmenin kapsamı

- Önceki numaralı fırça, Overlay girişlerine numara veya ortak origin yazmıyordu. Elle boyanan üç bitişik hücre üç ayrı kapak oluyordu; eski gerçek yöntemle hata tekrar üretildi.
- `StackedObstacleEntry.wallPieceId` yalnız editör fırça numarasını saklar. Oyun mevcut `wallPieceOriginPlusOne` alanını kullanmaya devam eder. Runtime spawner, düşüş, hasar, VFX ve ses kodları değişmedi.
- Editör her açık boyama/yeniden numaralama/silme işleminden sonra 4-komşu bileşenleri yeniden kurar. Köprü ve origin silinmesi, farklı numaralar, farklı duvar türleri ve satır sınırları ele alınır.
- Önceden ortak origin ile yazılmış226 tarzı kayıtlar, yalnız gerçek düzenleme sırasında fırça numarasını kalıcı alana taşır. Inspector'ı görüntülemek assetleri yeniden yazmaz. Eski numarasız tek-hücre kapaklar kendiliğinden birleşmez.
- Alt katmanların obstacle/origin dizileri değiştirilmez. Duvar için tam örtme kontrolü bütün parçanın alanını kullanır; diğer türlerin mevcut tam örtme ve saydam çim istisnası korunur.
- Grup kurma ve önizleme işlemleri editörde çalışır. Yeni mobil update, tarama, coroutine, GameObject, AudioSource veya per-hit allocation yoktur. CPU/GC/FPS ölçülmedi.

## Doğrulama

- `csi Tools/check_wall_overlay_editor.csx --before`: eski gerçek boyama yöntemiyle üç bağımsız kapak sorunu tekrar üretildi.
- `csi Tools/check_wall_overlay_editor.csx`: **130** gerçek editör yöntem kontrolü. Wall ve MetalWall, farklı çizim sıraları, yeniden boyama, köprü/origin silme, yeniden bağlama, numara değiştirme, ayrı adalar, satır sınırı/hole, alt fenerin korunması,226 tarzı eski kayıtların base editinden sağ çıkması, mevcut numarasız kapakların korunması, diğer katmanları koruyan silme, parça hedef sayımı, tam örtme ve saydam çim, numaralı çizim ve üstteki opak engelin görünürlüğü. Stone/Grass çalışan örnekleriyle deduplikasyon ve katman sırası da kontrol edildi. Kütüphane ayak izleri gerçek assetten okunur.
- `csi Tools/check_stacked_wall_route.csx`: mevcut ortak runtime origin/damgalama/beneath/hedef/dalga kontrolleri **265** geçti.
- **6054** diğer proje dosyası, tüm level asset/meta dosyaları, katalog, isimler ve runtime spawner hash ile korundu. Kaynak değişiklikleri yalnız `LevelData.cs` içindeki editör metadata alanı ve `LevelDataEditor.cs` içindedir. `Before/` bu iki dosyanın bu tur öncesini saklar.
- Roslyn kontrolleri gerçek yöntemleri sunum doubles ile çalıştırır. Unity GUI/Play veya proje build'i çalıştırılmadı; canlı inspector etkileşimi ve cihaz testi yapılmadı. Yayın/commit/release baseline yok.

Önceki226 v2 doğrulamasının iki kaynak hash'i, bu açık editör isteğiyle gelen değişikliklere ve bu kayıtla ilişkilendirilerek güncellendi.226 asseti ve tasarım modeli aynı kaldı.

## Kullanıcı226 düzenlemeleri korunur

226 asseti bu editör görevinin başlangıcında eski v2 tasarım modelinden zaten farklıydı (maske,baseWall/partNo dizileri). Bu görevin başlangıç SHA’sı ile son SHA’sı aynı; kullanıcı düzenlemeleri korunur. Eski226 batch doğrulaması asset drift korumasında beklenen şekilde durdu; batch PASS iddiası yok, eski model/hash assete benimsetilmedi. Yeni editör yöntem kontrolleri130 ve mevcut runtime katman kontrolleri265 geçti.
