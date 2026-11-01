# 201–205 · Lamba Şeridi ekleme önerisi (2026-11-01)

Kullanıcı: "201 ve 205 arasında yeni bir obstacle tanıtıyorduk ama lambayı hiç kullanmamış olduk... lamba kullanımı mümkünse
boardu tekrar tasarla. 201 belki kalabilir kuş yuvası ama onunla birlikte lambayı da ekleyelim yavaş yavaş".

Kademeli: kaymayan tek şerit → iki şerit → şerit bendi → kayan halka → kayan + mor (Zor).

| Lv | Değişiklik | Hedef |
|---|---|---|
| 201 | Kuş evleri + 50 kuş aynen; sağ kenarda 6 lambalı kaymayan dikey şerit (alt kenara iner) | Kuş 50 · Lamba 1 |
| 202 | Avlu duvarlarında, boya kasalarının üstünde iki 3'lü kaymayan lamba direği (üstleri hole) | Kuş 18 · Boya 2 · Çamur 10 · Lamba 2 |
| 203 | Duvar bendi (3×2) yerine iki katlı kaymayan lamba bendi (iki yatay 3'lü şerit) | Kuş 12 · Kargo 4 · Lamba 2 · Çamur 8 |
| 204 | Dipteki 3×3 fener grubu yerine KAYAN mini ışık çarkı (8 lamba, ortası hole) | Kuş 16 · Kanal 1 · Lamba 1 |
| 205 Zor | Köşe mühür+pusula 2×2'leri yerine iki KAYAN 2×2 lamba kutusu (her birinde 2 mor, ters yönler) | Kuş 24 · Lamba 2 (tek başına duran roket sepeti kaldırıldı — kullanıcı) |

**Uygulandı (2026-11-01, kullanıcı onayı).** Kaynak yedekler `before_*.asset`. `python3 variants.py preview` / `python3 variants.py apply <lv>`.
Çizici: `lampview.py` (131-205 çizicisi + Lamba Şeridi katmanı). İlk 202 denemesi (lento) orta sütunun boşluk
üstünden beslenmesini kesiyordu → reddedildi, yan direklere geçildi.

## v2 (2026-11-01) — kullanıcı: "201 hariç seviyeler çok kolay oldu... tüm geçmiş güzel obstacle'lara bak, birkaç tane daha
kullanmaya çalış" + "water channel aşağıya dönük biterse iyi olur"
Teşhis: v1 lamba eklerken işi azalttı (fener, mühür, pusula, duvar gitti; yerine kısa kaymayan şeritler geldi).
Kaldıraç: akışı kesmeyen örtüler (çim 42, çiçekli çim 58 = çiçek + çim), morlar yalnız special yerinde, hamle −1/−2.
| Lv | v2 | Hedef / hamle |
|---|---|---|
| 202 | lamba direkleri + yanlarında 2×2 çim çalılıkları | Kuş 18 · Boya 2 · Çamur 10 · Çim 8 · Lamba 2 / 24 |
| 203 | üst 2 sıra çim bandı; bent lambalarının ortası mor | Kuş 12 · Kargo 4 · Lamba 2 · Çamur 8 · Çim 18 / 24 |
| 204 | kanal aşağı dönerek (6,10)'da biter; çark 1 mor; evlerin yanı ve sol alt çiçekli çim | Kuş 16 · Kanal 1 · Lamba 1 · Çiçek 8 · Çim 8 / 25 |
| 205 | hunide 20 hücre çiçekli çim; 4 mor; roket sepeti yok | Kuş 24 · Lamba 2 · Çiçek 20 · Çim 20 / 25 |
Reddedilen denemeler: 202'de çamur üstüne PlasticTwoStage (hareketli engel → avluya düşer); 204'te sağ üst 2×2 dolap
(altındaki 3 hücrenin beslemesini kesiyordu). v1 önizlemeleri `Revisions/`.

## 205 v3 (2026-11-01) — kullanıcı: "sol ve sağda fener... ekrandakinden fazla fener hedefi olsun.. sütun bazlı döküm aktif,
sütun parametresi 0 olsun ki sadece o sütundan aksın... fener benim sevdiğim obstacle'lardan biri"
Kenar sütunları (0 ve 8) baştan sona Lantern 75 (hareketli, 2 vuruş) — ekranda 22, hedef 30; `movableSpawnColumns` yalnız
0 ve 8, `movableSpawnFallbackMoves: 0` (katı), `movableSpawnFillColumns: 1`. Lamba kutuları bir sütun içe (1–2 / 6–7);
altlarındaki (1,2)/(7,2) beslenemediği için hole. Hunide 2 sıra çiçekli çim (10). Roket sepeti yok. 27 hamle.
Hedef: Kuş 24 · Lamba 2 · Fener 30 · Çiçek 10 · Çim 10.
