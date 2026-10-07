# Level 111 — Mühürlü Kale

Asset: `LevelP_001170.asset`. 9×11 tam board, 99 aktif hücre, başlangıç ayarı 23 hamle. Normal level; 110 önceki Boss Duel kuralı uyarınca ayrılmıştır, bu çalışma 110 için bir boss oluşturmaz. Katalogda 111'e bağlanır; 110 kaydı bu çalışma kapsamında eklenmez.

## Açılış: normal hamle alanı yerine special zinciri

Başlangıçta **95/99 hücre engellidir**: 59 GrassFlower, 18 SculptingSpecial ve çamuru örten 18 chest3. Yalnız dört hücre açıktır; dördünde de hazır special vardır. Normal taşla yapılabilecek bir açılış eşleşmesi yoktur.

- Dikey komşu Override çifti: `(4,4)` ve `(4,5)`.
- İki LineV: `(1,5)` ve `(7,5)`.
- Koordinatlar sıfır tabanlı, üstten aşağı y'dir. Oyuncu iki Override'ı birbirine sürükleyerek combo yapar.

Override + Override board genelindeki etkiyle çiçek katmanını aşındırır ve hazır LineV'leri dalga varışında tetikler. LineV'ler iki yan sütunu tarayarak devam eder. GrassFlower hücresine tek geçerli vuruş yalnız çiçekleri döker; alttaki Grass için bir vuruş daha gerekir. Aynı zincirde tekrar vurulan yerler tamamen açılabilir; bütün örtünün ilk dalgada kalkması beklenmez. Line'ların hedef engellere vereceği net toplam hasar ve runtime zamanlama Unity'de ölçülmedi.

```text
GGGGGGGGG
SSSGGGSSS
SSSGGGSSS
SSSGGGSSS
GGGGOGGGG
GVGGOGGVG
GGGGGGGGG
CCCGGGCCC
CCCGGGCCC
CCCGGGCCC
GGGGGGGGG
```

`G` GrassFlower, `S` SculptingSpecial, `C` altında Mud bulunan chest3, `O` SystemOverride, `V` LineV.

## Görsel düzen ve devam eden zorluk

Dört adet 3×3 hedef bloğu kale kuleleri gibi gruplanır. Üstte krem/gri special taşları, altta sıcak ahşap sandıklar; aralarındaki yeşil örtü geçişleri kapatır. Sağ-sol simetri korunur. Açılış dört special ile yapılır; diğer taşlar elle sabitlenmez, mevcut spawn/refill sistemi kullanılır.

- **59 Grass temizleme hedefi:** board'a 59 GrassFlower yerleştirilir. Çiçek + çim toplam iki katmandır; hedef Grass (42) olarak kalır, yalnız çiçeklerin dökülmesi hedefi tamamlamaz.
- **18 SculptingSpecial:** yalnız special ile kırılır. Normal match'lerle bitirilemez; açılış sonrası yeni special üretimi gerekir.
- **18 chest3:** üç vuruşlu ikinci hedef bölgesi.
- **18 Mud:** sandıkların altında iki katmanlı zemin. Sandıkların kırılması aynı bölgedeki işi bitirmez.

Override çiftinin genel tile toplaması, sabit over-tile blocker içeriğini korur. Bu nedenle hedef blokları bağımsız, GrassFlower geçişlerde tutulur. İlk combo geçiş örtüsünü aşındırır; kalan çim ve kulelerin hedef yükü sonraki oyuna kalır. Line sütunları her kulede yalnız bir sütuna denk gelir; geri kalan 12 SculptingSpecial line yollarının dışındadır. Refill/cascade'in oluşturacağı ilave etkiler rastgeledir.

Dört renk havuzu kullanılır. Ek EggBird, RocketBasket veya OverrideBatteryBox yardım paketi verilmez; başlangıç special'ları alan açma aracıdır. 23 hamle zor seviye için ilk tahmindir, doğrulanmış kazanma oranı değildir.

SculptingSpecial ilk 45, GrassFlower ilk 105, chest3 ilk 14, Mud ilk 4'te kullanılmıştır. Yeni engel tanıtılmaz. 108 ve 109 asset'leri değiştirilmedi.

## Oynanış geri bildirimi ve revizyon

Kullanıcı ilk Grass sürümünü kolay buldu; ilk hamlede hedeflerin büyük kısmının gittiğini bildirdi. Bunun üzerine 59 Grass hücresi GrassFlower'a çevrildi. Hedef yine 59 Grass; hamle sayısı 23, special düzeni ve diğer engeller aynen korundu. Bu revizyonun zorluğu henüz oynanarak doğrulanmadı.

## Doğrulama sınırı

Maskenin/dizilerin boyutu, sağ-sol simetri, 95 kapalı hücre, dört açık pinned special, komşu Override çifti, iki LineV konumu, 18 chest-over-Mud katmanı, goal adetleri, geçmiş engel kullanımı ve katalog bağlantısı statik kontrol edildi. Runtime üretilecek normal renkler sabitlenmediği için refill sonrası hamle veya kazanma sayısı hesaplanmadı.

Kod dayanakları: `GridSpawner` gravity-izole hücrede de pinned special spawn eder; `CanTapActivateSpecial` örtüsüz special'a izin verir; `OverrideOverrideCombo` diğer special'ları dalga varışında zincire alır; `SpecialCellUtils.AddAllTiles`/`SpecialUtils.CanAffectCell` örtü ve blocker hedeflemeyi belirler. Bunlar kod incelemesidir, canlı oynanış doğrulaması değildir.

`Level111-Muhurlu-Kale.svg` mevcut engel/special ikonlarıyla yerleşim şemasıdır; gerçek oyun ekran görüntüsü değildir. GrassFlower önizleme sprite'ı kullanılır; örtü altındaki rastgele taşlar ve sandık altı Mud şemada ayrıca çizilmez.

Unity, bot, build veya oynanış simülasyonu çalıştırılmadı. İlk combo zinciri, refill ve 23 hamlenin zorluğu kullanıcı tarafından oynanarak değerlendirilecek.
