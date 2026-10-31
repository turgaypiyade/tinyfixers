# 191 · Işıklı Kapı / Gate of Lights — Lamba Şeridi tanıtımı (kaymayan) · v2

**Kullanıcı kararı (2026-11-01):** 191 düzeni kullanıcıdan: "9×9 ya da 9×10; lamba sütunu, boşluk, lamba sütunu,
ortada 3 boşluk, ..." → lamba | taş | lamba | 3 taş | lamba | taş | lamba. Önceki onaylı v1 (iki Γ kemer, 9×11,
26 hamle) yedeği `Batches/191-LampStrip/Revisions/v1-gamma/`; en eski 191 (Boya Terasları) `Revisions/before/`.

- **Tahta:** 9×10, hole yok. Üstte 3 sıra açık oyun alanı. 24 hamle, 4 renk.
- **Lamba Şeridi ×4:** sütun 1, 3, 7, 9 (1 tabanlı), satır 4–10, her biri 7 lamba; shift=0.
  Sol çift tepeden aşağı Mor·Sarı·Sarı·Kırmızı·Kırmızı·Yeşil·Yeşil, sağ çift Mor·Mavi·Mavi·Yeşil·Yeşil·Sarı·Sarı.
  Komşu şeritler aynı satırda aynı renk → aradaki tek taş sütunundaki dikey eşleşme iki şeride birden vurur.
  Dört mor lamba aynı satırda (4. satır): o satırdan geçen yatay roket hepsini birden söndürür.
- **Çamur ×18:** orta 3 sütun, satır 5–10 (3×6 tek blok).
- **Hedef:** LampStrip 4 · Mud 18.
- **Kontrol:** 90 uzunluk diziler, origin = parça en küçük index, şeritler dibe kadar iner → altlarında ölü hücre yok;
  tek genişlikli taş sütunları üstten dolar. Önizleme gerçek sprite; taş renkleri temsili.
- **Risk:** kenar şeritler (1. ve 9. sütun) yalnız tek genişlikli sütunlardaki dikey eşleşme ya da special ile söner;
  tanıtım için sert gelirse hamle artırılır ya da kenar şeritler kısaltılır.
- **Oynanmadı:** Denge ölçülmedi; Unity Play/build, sim, remote yayın yok.
