# 89–90 / Kullanıcı oynanış düzeltmesi

23 Ekim2026: kullanıcı iki leveli zor buldu. İstenen yerel değişiklikler uygulandı:

| Level | Asset / gerçek katalog anahtarı | Değişiklik |
| --- | --- | --- |
| 89 Gizli Jel Bahçesi | `LevelP_00991.asset` / `LevelCL_089` |28→33 hamle; sağ alt çalışma alanının üstüne4. satır/5–9. sütunlarda5 oynanabilir hücre. Aktif alan41→46; tam-board jel hedefi de46. |
| 90 Mor Düello | `LevelP_00500.asset` / `LevelCL_090` | Yalnız28→33 hamle. Boss board'u, hedefler, sağlık ve saldırı ayarları aynı. |

Koordinatlar sol üstten1 tabanlıdır. 89'daki kasa,16 oyma taşı,16 çim ve örtülü jel düzeni korundu; hücreler engel/pin eklemeden normal doluma açıldı. Yeni satır mevcut sağ alt alanın üst girişini bir satır yukarı taşır,4×4 kasanın sağ kenarına bitişik çalışma alanı sağlar.

[Önce/sonra önizlemesi](Before-After.png), güncel proje sprite'larıyla çizildi ve incelendi. Taş renkleri temsilidir; kasa görseli şematiktir, oyun ekran görüntüsü değildir.

`verify.py` mask/alan/hedef/katman ve değişiklik kapsamını doğrular. 90'ın hamle satırı dışında bütün baytları aynı;89'da yalnız maske, hamle ve tam-board jel miktarı değişti. Ruby YAML ayrıştırması geçti. Snapshot'taki1.899 Settings/Scripts/isim dosyasından yalnız bu2 asset değişti; diğer1.897 dosya korundu. Kullanıcının önceden değiştirdiği89 asseti de `Before/` içinde olduğu gibi saklandı.

Runtime/VFX veya köşe çizim kodu değiştirilmedi.5 yeni hücre mevcut tile pool/refill akışını kullanır; performans ölçümü veya yeni denge sonucu yok. Unity Play/build ya da bot/kaskat simülasyonu çalıştırılmadı.
