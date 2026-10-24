# Kuyruktaki special efektleri ve taş havuzu — 2026-10-22

Kullanıcı: boş taş hücresi basit oyunda görülmüyor; yoğun patlama ve taş düşüşlerinde görülüyor. Görsel eski Android test sürümünden. Önceki PulseCore fitil temizliği bu değişiklikte korunur.

## Kaynakta bulunan açıklık

`MatchClearAction` normal taşların `LifetimeVersion` değerini kurulumda tutuyordu. Presentation yolu bu kimlikleri kullanmıyor; `BoardAnimator.PlayClearPresentation` çalışmaya başlarken yeniden okuyordu. Pulse/radial efektleri de başlangıçta `RunForCurrentLifetime` kullanıyordu. Aynı GameObject bekleme sırasında havuza girip yeni taş olarak çıkarsa eski işlem yeni kullanımı geçerli sayabiliyordu. Special formation'ın reveal/finally adımları da yalnız null/aktif kontrolüyle yeni kullanıma dokunabiliyordu.

Örnek sıra: hedef A için işlem kuyruğa girer → başka patlama A'yı temizler → havuz aynı nesneyi B için verir → eski efekt başlarken B'nin kimliğini yakalar → B'yi animasyonla gizleyebilir veya temizleyebilir. Ekrandaki olayın birebir bu sıra olduğu cihaz kaydıyla doğrulanmadı.

## Değişiklik

- Tamamlanmış plan, `MatchClearAction` kurulurken hedef ve yaratılan special kimliklerini bir kez kaydeder. Daha sonraki capture çağrıları yeni kullanımı benimsemez.
- Final clear, pulse/radial animasyonları, formation/reveal/finally ve kaynak ikonlarını gizleme yolları bu kaydı kullanır. Bilinmeyen/yeni kullanımdaki taş atlanır.
- Bekleme sırasında yeniden kullanılan taşın eski coroutine'i mevcut lifetime wrapper ile iptal edilir. Formation'ın final temizliği de yeni taşın runtime durumunu sıfırlamaz.
- Mevcut havuz ve fitil temizliği değişmedi. Planın kimlik sözlüğü, playback sırasında zaten oluşturulan sözlüğün yerini alır; yeni kare başına tarama, AudioSource, GameObject veya VFX eklenmedi. Var olan ghost üretimi bu düzeltmede değiştirilmedi. Cihaz performansı profillenmedi.

## Doğrulama

Bağımsız C# kontrolleri üretim kaynağından alınan metotları Unity doubles ile çalıştırır; Unity projesi build edilmez ve board/cascade simülasyonu yapılmaz.

- `csi Tools/check_presentation_lifetime.csx`: 266 kontrol. Eski zamanlamayı temsil eden geç capture kontrolü, yeni kullanıma animasyonun ulaştığını gösterir; kuyrukta capture bu yolu reddeder. Gecikmede reuse, aktif/deaktif hedef, formation reveal/finally ve canlı normal akışlar denetlendi. Sekiz C# kaynak dosyası syntax açısından ayrıştırıldı.
- `csi Tools/check_tile_lifetime.csx`: 27 kontrol.
- `csi Tools/check_tile_pool_reserve.csx`: 13 kontrol.
- `csi Tools/check_override_sorting.csx`: 115 kontrol. Test doubles'a daha önce eklenen fitil/çocuk deaktive etme API'si tamamlandı; üretim fitil kodu değiştirilmedi.
- `git diff --check` geçti.

Toplam 421 kontrol. Unity Play/build, gerçek renderer/Canvas davranışı veya Android üzerinde yeniden üretim yapılmadı. Yoğun zincirler ve düşüşlerle güncel cihaz sürümünde doğrulama gerekli; eski ekran görüntüsünün kesin kök nedeni veya tüm boş hücrelerin çözüldüğü iddia edilmez.
