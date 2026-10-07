# Level 118 — Su Kuleleri (v2, zor)

`LevelCatalogPro` global 118 → `LevelP_001240.asset`. Tam 9×11 board, başlangıç ayarı 28 hamle. Çiçek/çim yok.

## Kompozisyon

Dört 4×4 metal kasa: her biri 12 hücrelik tek parça MetalWall halkası, ortasında 2×2 kırmızı bidon (WaterTank). Kasalar alt 8 satırda iki sütun hâlinde üst üste; aralarında 1 sütunluk orta koridor, üstte 3 açık sıra.

```text
ooooooooo
ooooooooo
ooooooooo
1111o2222
1NN1o2NN2
1NN1o2NN2
1111o2222
3333o4444
3NN3o4NN4
3NN3o4NN4
3333o4444
```

`o` normal taş, `1`–`4` MetalWall (56) halkaları, `N` WaterTank (52, 2×2; ortak sol-üst origin).

Önizleme: `Level118-Su-Kuleleri.png`.

## Hedefler ve zorluk

- 4 WaterTank (iki vuruş, yalnız special; kırılınca ~8 su birikintisi, birikinti hedefi HUD'da dinamik eklenir).
- 4 MetalWall halkası. Ardışık vuruş kuralı: halkanın bir hücresine üst üste hamlelerde vurulursa halka bütün olarak yıkılır.
- Üst kasalara üstten ve koridordan, alt kasalara yalnız orta koridordan ulaşılır (üst kasaların altı alt kasalara dayalı). Line special'lar halka ve bidona birlikte erişim sağlar.
- Kasalar alt satırlara dayandığı için altlarında beslenmesi gereken gölge hücre yok; dolum üstteki 3 sıra ve koridordan.

## Revizyon

v1 (dört açık bidon + mavi şapka + baykuş) kolay bulundu. v2'de bidonlar kullanıcının isteğiyle metal kasalara kapatıldı; açık alan 35 hücre.

## Statik kontrol

Diziler, 4 halka parçası ve origin'leri, 2×2 bidon origin'leri, hedef adetleri ve katalog bağlantısı (GUID korunarak) kontrol edildi. 35 açık taş simetrik sabitlendi; hazır üçlü/2×2 yok, 9 geçerli takas. Unity, bot, build veya simülasyon çalıştırılmadı; 28 hamle ilk tahmin.
