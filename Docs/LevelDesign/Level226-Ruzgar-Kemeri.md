# 226 · Rüzgâr Kemeri / Windlit Arch — v2

2026-11-06 · V1 kullanıcı tarafından reddedildi. Bu revizyon henüz oynanmadı/onaylanmadı.

**9×11,68 aktif hücre;28 hamle Normal. Hedef:1 duvar parçası,24 fener,16 un.** Katalog `LevelCL_0226`,bölüm10;isim/GUID/katalog/hamle korundu. Asset [LevelV1_00002260.asset](../../Assets/_Project/Settings/ProLevels/LevelV1_00002260.asset).

## Tasarımın dört sorusu

1. **İlk görüntü:** Sağ koldan tabana dönen iki hücre genişliğinde bağlı duvar yolu; yukarıda6×3 çalışma yüzü,sağda ayrı değirmen adası.
2. **İlk karar:** Yolun üst ağzındaki veya sol alt ucundaki bir duvar hücresine odaklanırken adaya yatay special hizala.
3. **Açılınca:** Aynı hücre beşinci vuruşu alınca tek24hücre parça mevcut dalgayla yıkılır;her hücrenin altından kendi gerçek feneri çıkar.
4. **Kalan iş:**24 iki aşamalı fener ve iki değirmenin çuvallarından gelen8+8un. Değirmenlere yalnızspecial ile uzanılır;unun gelecekteki rastgele yeri önceden çizilmez.

## Akış ve katman

Duvar sıfır tabanlıx4–5/y3–10 dik kolu ilex0–3/y9–10 tabanını birleştirir. Bütün duvar hücreleri ortak origin31 kullanır;her birinin altında ayrı originli Fener75 vardır. Yolun dik kolu tabana dayanır;altında engellenmiş normal alan yoktur.36 normal başlangıç yuvasının36’sı doğal beslenir. Renk pini,hazırspecial ve boş başlangıç yuvası yoktur.24 gizli fener toplam hedefi karşılar;ek/refill kotası0’dır. Var olan sütun seçimi4/5 ekstra fener gerektirmez.

İki Windmill80,x7/y4 vex7/y6’dadır;ada yalnız bu sekiz hücreyi içerir. Ana tahtayla ortogonal veya çapraz normal taş teması yoktur;x6hole şeridi ayırır. Ana çalışma alanında her değirmen satırına yatay special hattı bulunur. Mevcut LineH kodu hole’lar ve duvar hücreleri üzerinden ilerler;akış koduna dokunulmadı. Ada normal taş çalışma alanı değildir;değirmenlerin altında beslenmesi gereken boş hücre bırakılmadı.

## Gerekli küçük katman desteği

Eski stacked Wall damgalaması her kapak hücresini ayrı parça yapıyordu. İsteğe bağlı `wallPieceOriginPlusOne` ortak origin bilgisini taşır;0eski davranışı korur. GridSpawner yerleştirme hücresini değiştirmeden gerçek duvar origin’ini ve beneath kaydını ortak kurar. Editör hedef önerisi bunu tekparça sayar. Düşüş/kaskat,animasyon,efekt ve ses kodu değişmedi. Alan okumasıO(1);yeni tahta taraması,efekt veya per-hit allocation yok. Mevcut duvar dalgası tek coroutine ve mevcut FX havuzlarını kullanır;1356 sıra/iptal/havuz kontrolü geçti. CPU/GC/FPS ölçülmedi.

## Kontroller ve sınırlar

Gerçek yeni stamp/origin/editör hedef/beneath/dalga yöntemleriyle265kontrol geçti. Eski yöntemle24ayrıorigin sorunu tekrar üretildi. YAML/dizi/katman/bağlılık/hedef/başlangıç besleme/special erişim ve5278dosya koruma kontrolü geçti;1–225ve227–230assetleri,katalog,adlar aynı. Üç eski genel test betiğinin Dimerdal test doubles eksikleri bu değişiklikten önce de vardır;[teknik kayıt](Batches/226-230-Lantern-Wind-Crystal/Revisions/v2-wall-route/legacy-harness-status.json).

Gerçek sprite PNG ve referans açılıp karşılaştırıldı. Yakın örnekler111konumsal,84kırpılmışsilüet;U/J benzeri kütle tek başına yenilik iddiası değildir. Buradaki oyun işi tekparça yolun açılması ve ayrı adaya special erişimidir. Sağ kesit statiktir,oynanmış an değildir;serbest taş renkleri temsilidir.28hamle ilk tahmin. Unitybuild/Play/cihaz/bot/düşüş-kaskat simülasyonu,yayın/baseline/commit yok.

[Yeni önizleme](Level226-Ruzgar-Kemeri.png) · [V2 teknik kayıt](Batches/226-230-Lantern-Wind-Crystal/Revisions/v2-wall-route/README.md)
