# Level 113 — Arlekin Uçurtma

`LevelCatalogPro` global 113 → `LevelP_001190.asset`. Normal level, tam 9×11 board (99 hücre), başlangıç ayarı 28 hamle. 112'de tanıtılan MetalWall'un ikinci kullanımıdır.

## Kompozisyon

Arlekin desenli uçurtma: dört üçgen kumaş bölgesi çaprazlama kırmızı/mavi yıldızlı şapka, ortada dikey MetalWall omurga, yatay GrassFlower çıta ve altta üç çiçekli kuyruk.

```text
oooo1oooo
oooR1Booo
ooRR1BBoo
oRRR1BBBo
FFFFFFFFF
oBBB2RRRo
oBBB2RRRo
ooBB2RRoo
oooB2Rooo
oooo2oooo
oooFFFooo
```

`o` normal taş, `R` Plastic_Red (12), `B` Plastic_Blue (11), `F` GrassFlower (58), `1`/`2` MetalWall (56) parçaları (4 ve 5 hücre).

Önizleme: `Level113-Arlekin-Ucurtma.png` (mevcut sprite'larla yerleşim şeması, oyun ekran görüntüsü değil; MetalWall basit plaka olarak çizildi).

## Hedefler ve kararlar

- 2 MetalWall parçası. Ardışık vuruş kuralı: hamlede vurulmayan hücre başa döner, bu yüzden bir parçaya üst üste odaklanmak gerekir.
- 15 Plastic_Red + 15 Plastic_Blue. Şapka yalnız kendi rengindeki (kırmızı = Core, mavi = Bolt) komşu normal eşleşmeyle kırılır → renk seçimi puzzle'ı. Şapkalar hareketlidir; desen oyunla değişir.
- 12 Grass (GrassFlower): çiçek + çim, iki katman. Çıta taş akışını kesmez.
- Chest kullanılmadı. Yardımcı ya da hazır special yok.

## Statik kontrol

Dizi uzunlukları, iki MetalWall parçasının bağlantısı ve origin'leri, hedef adetleri ve katalog bağlantısı kontrol edildi. 48 açık normal taş + 12 örtü altı taş elle sabitlendi (sağ-sol simetrik renkler). Açılışta hazır üçlü veya aynı renk 2×2 yok; normal taşlar arasında 10 geçerli takas var, üst ve alt yarıda. Omurganın kapattığı 4. sütundaki iki örtü altı hücre (`(4,4)`, `(4,10)`) sabit taşla başlar; sonraki dolumu çapraz akışa kalır.

Unity, bot, build veya simülasyon çalıştırılmadı. 28 hamle ilk tahmindir; kullanıcı oynayarak ayarlayacak.
