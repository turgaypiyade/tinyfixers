# RhodesStatue — Rodos Heykeli

`ObstacleId.RhodesStatue = 70`. Mevcut obstacle kütüphanesine eklenen **2×2, sabit, dört vuruşlu** engel; yeni özel servis veya prefab gerektirmez.

LevelData editöründe **Obstacle Palette → RhodesStatue** seçilir ve bir origin hücresine yerleştirilir. Dört hücre aynı origin'i paylaşır; yerleştirmek için boş 2×2 ayak izi gerekir. Obstacle goal'e RhodesStatue eklenirse miktar hücre sayısı değil **heykel sayısıdır**. HUD, prelevel ve sonuç ekranının hedef ikonu sağlam heykeldir. TR/EN tanıtım ipucu kayıtlıdır. İlk production tanıtımı [161 — Rodos Atölyesi](LevelDesign/Level161-Rodos-Atolyesi.md): 9×11, tek heykel, iki hazır çizgi, 28 hamle. [162 — Büyük Restorasyon](LevelDesign/Level162-Buyuk-Restorasyon.md) dört heykel ve katmanlı yükle devam eder; önceki seviyeler korunur.

## Hasar

Başlangıç: sağlam heykel. Birinci special isabeti kolu kırılmış görsele, ikinci isabet ağır hasara, üçüncü isabet iskelete geçer. Dördüncü isabette bütün 2×2 engel kalkar, bir hedef kredisi gelir. Her aşama `SpecialOnly`; normal match, doğrudan booster ve sıradan Scripted hasarı reddedilir. PatchBot'un mevcut special isabeti desteklenir. Zincir/combo ve yardımcı patlamaların mevcut vuruş sayısı kuralları korunur.

`ObstacleResolutionService` genel olarak booster isabetini special context'iyle tekrar deniyordu. Bu yeni heykelde **yalnız ID 70 için** o fallback kapatıldı; diğer obstacle'ların booster davranışı değişmedi. Genel line/pulse isabet zamanı, per-origin tekrar engelleme ve hedef sayımı mevcut yoldan sürer. Örtüler/alt katmanlar mevcut stacking sistemiyle çalışır; sıvı taban kuralı aynıdır.

## Görsel dosyası

Güncel runtime görünümü `RhodesStatueView` ile `Resources/RhodesStatue/S1…S4_Base` ve uygun aşamalardaki `_Sword` / `_Shield` parçalarından kurulur. Kılıç ve kalkan, aynı kökün iki Image çocuğunda vuruşla hareket eder; `OnDisable` hareketi durdurup pozları sıfırlar. Kütüphanenin stage referansları bu Resources tabanlarını kullanır. HUD, prelevel ve sonuç ekranının ortak `goalIconSprite` referansı doğrudan `Resources/RhodesStatue/S1_Base.png` sprite'ıdır; ek runtime yüklemesi veya sprite üretimi gerekmez. Aşağıdaki sheet eski sanat kaynağıdır.

[RhodesStatue_Stages.png](../Assets/_Project/Art/UI/Obstacles/RhodesStatue/RhodesStatue_Stages.png) kullanıcının son gönderdiği çerçeveli PNG'nin değiştirilmeden kopyasıdır. Şu an **2172×724**, tek sıra, soldan sağa dört eşit **543×724** dilim. Sprite alt varlıkları: `RhodesStatue_Stage1…Stage4`; hepsi aynı rect/pivot ölçüsünde. Görsel 2×2 hücrelik Image alanında oranını korur; uzun karakter kareye esnetilmez. Hasarla root boyutu değişmez.

Kullanıcı görselleri düzenleyebilir. Aynı sheet boyutu ve dört eşit dilim korunursa bu PNG'yi yerinde değiştirmek yeterlidir; `.meta` silinmemeli, sprite ID/GUID bağlantıları korunmalıdır. Boyut/dizilim değişirse slice rect'leri de güncellenmelidir. Kod dosya adına göre runtime yükleme veya yeni Sprite.Create yapmaz.

Kırılmada mevcut taş sesleri ve altı paylaşılan taş parçası kullanılır; `BoardBreakFxService` pool'u ve standart shatter yolu yeniden kullanılır. Yeni Update, coroutine host'u, scene taraması veya kare başına allocation eklenmedi. Genel obstacle hasar yolunun mevcut footprint toplama maliyeti değişmedi; cihaz performansı ölçülmedi.

## Kontrol

- `ruby Tools/check_rhodes_statue_assets.rb`: Unity YAML, dört slice/reference, 2×2/kural ayarı, goal ikonu, parçalar, tanıtım çevirileri ve yeni level dizileri.
- `csi Tools/check_rhodes_statue_rules.csx`: gerçek ortak C# metotlarıyla dört aşama seçimi, normal/special/booster/scripted ayrımı, resolver fallback ve ortak footprint. Başka engellerin booster fallback'inin korunduğu da denetlenir.
- Unity source/API tanıları; build veya assembly emit yok. Unity'de görsel oynatım yapılmadı.
