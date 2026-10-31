# 196 · Işık Çarkı / Wheel of Lights — Lamba Şeridi KAYAN tanıtımı

**Kullanıcı kararı (2026-10-31/11-01):** 191 kaymayan, 196 kayan Lamba Şeridi tanıtımı; "güzel bir şekil olsun".
Eski 196 (Basamaklı Arşiv: dolap/sandık/çamur) yedeği `Batches/196-LampStrip-Shift/before_LevelV1_00001960.asset`.

- **Tahta:** 9×10; üstte kubbe omuzları (6 hole), ortada 3×3 hole pencere. 22 hamle, 4 renk.
- **Lamba Şeridi ×1 — kapalı halka:** sütun 3–7, satır 6–10 (1 tabanlı) çevresi, 16 lamba; `shift=1`, saat yönü.
  Sol üst köşeden saat yönü: Mor·Sarı·Sarı·Kırmızı·Kırmızı | Mavi·Mavi·Yeşil·Yeşil | Mor·Sarı·Sarı·Kırmızı | Kırmızı·Yeşil·Yeşil.
- **Tanıtım fikri:** halkanın alt kenarına hiçbir taş değmiyor; oraya düşen lambalar ancak kayıp yukarı
  dönünce eşleşmeyle sönüyor. Oyuncu "lamba bana gelir" kuralını görerek öğreniyor. Üst kenar açık oyun alanına,
  yan kenarlar 2 genişlikli yan sütunlara bakıyor.
- **Çim ×12:** alt köşelerde 2×3 + 2×3 yastık; yan kenarın alt lambalarına erişimi biraz geciktirir.
- **Hedef:** LampStrip 1 · Grass 12.
- **Kontrol:** 90 uzunluk diziler; hole üstünde engel yok; halka alt kenara dayalı ve içi hole → dikey beslenmeyen
  hücre yok. Halka yolu runtime sırasıyla aynı (sol üstten saat yönü). Önizleme gerçek sprite, oklar yalnız önizleme;
  taş renkleri temsili.
- **Kod:** kapalı halka desteği eklendi (LampStripService: yürüyüş sırası + halkada sarmalayan lamba pop yerine kayar).
- **Oynanmadı:** denge ölçülmedi; Unity Play/build, sim, remote yayın yok.
