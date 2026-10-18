# Level 154 — Renkli Sandıklar

`LevelCatalogPro` global 154 → `LevelP_001580.asset`, chapter 7. Kullanıcının görselindeki düzen **8×8** olarak korundu; 32 hamle, dört renk. [Önizleme](Level154-Renkli-Sandiklar.png).

```text
CCooooCC
CCooooCC
CCCCCCCC
CCCCCCCC
HH....HH
HH....HH
HHHHHHHH
HHHHHHHH
```

`C` ColorChest (21, 2×2) · `H` HatLauncher (31, 1×1) · `o` başlangıç taşı · `.` gerçek hole.

- Üst köşelerde iki, hemen altında kesintisiz sırada dört ColorChest: toplam altı sandık. Kapalı sandık önce açılır; ardından dört farklı renk çıkarılır. Bizdeki ColorChest, referanstaki dört renkli şişe sandığının karşılığıdır.
- Alt bölümde iki hücre kalınlığında U oluşturan 24 HatLauncher vardır. İki yanda 2×2, altta iki tam sıra; U’nun içindeki 4×2 bölge hole’dur, doldurulacak normal hücre değildir.
- Hedefler: **6 ColorChest + 48 EnergyOrb**. Şapkalar mevcut servisle her geçerli vuruşta bir enerji verir; hedef dolunca tükenirler, kaldırılmaları hedef değildir. 48 toplam grup kapasitesidir; şapka başına 48 değildir.

## Açılış

Üst ortadaki sekiz normal taş sabittir. Referanstaki iki satırın da sarı-kırmızı-mavi-yeşil olması bizim swap/match-3 sisteminde sıfır geçerli hamle verdiğinden renkler oynanabilir şekilde uyarlandı:

```text
Sarı   Yeşil  Sarı  Sarı
Kırmızı Sarı Sarı  Mavi
```

Kendiliğinden üçlü veya aynı renk 2×2 yok; dört geçerli takas var. Örneğin (3,0) ↔ (3,1), üstte dörtlü sarı oluşturabilir; koordinatlar sıfır tabanlıdır. Hazır special yoktur. Refill dört renk kullanır; sandık renklerini tamamlayabilmek için renk havuzu daraltılmadı.

Oyuncu küçük üst alanda eşleşme/special kurarak sandıkları açar. Sandıklar kalktıkça normal taş alanı genişler. Yan şapkalara normal komşu eşleşmelerle, altta normal hücresi bulunmayan şapkalara special ile ulaşılır. Kalıcı şapkaların altında normal dolum cebi bırakılmaz.

## Kontroller ve sınırlar

56 aktif hücre, 8 hole; yedi hücre dizisi 64 eleman. Altı 2×2 sandığın ayak izleri/origin’leri, maske bağlılığı, hedefler, pinler, kütüphane sprite’ları, GUID ve katalog bağlantısı statik doğrulandı. Önceki level assetleri ve ObstacleLibrary hash karşılaştırmasında korundu.

Yeni runtime/VFX kodu eklenmedi; 24 mevcut HatLauncher kaynağı ve altı mevcut ColorChest görünümü kullanılır. Şapka servisi interceptor’larını OnDisable’da bırakır; mevcut orb uçuşu resolve döngüsünü bekletmez. Hücreye yeni frame taraması veya üretim yolu eklenmedi. Cihaz performansı ölçülmedi.

32 hamle ve 48 enerji ilk oynanış ayarıdır. Unity Play/build, bot veya oyun simülasyonu çalıştırılmadı; kazanma oranı ölçülmedi. Önizleme yerleşim şemasıdır.
