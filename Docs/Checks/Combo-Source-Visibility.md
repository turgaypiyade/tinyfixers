# Combo kaynak ikonları — 2026-10-23

Kullanıcı: Pulse–Pulse ve Override–Override animasyonu special'ları büyütüp yukarı kaldırırken bazen hücrelerinde ikinci ikon kalıyor.

Kaynakta `HideTileVisualForCombo` yalnız CanvasGroup alpha/input değerlerini kapatıyordu. Paralel düşüş/engel yenilemesi `BoardController.RestoreTilePresentation` üzerinden alpha'yı tekrar açabiliyordu. `KeepHiddenForCombo` koruması mevcut Line–Pulse orbit yolunda kullanılıyor, ortak combo gizleme metodunda işaretlenmiyordu. Pop ve Pulse impact animasyonları da alpha'yı tekrar yazabiliyordu.

Ortak combo kaynak gizlemesi artık mevcut `KeepHiddenForCombo` bayrağını da tutuyor. Böylece Pulse–Pulse charge başlamadan yapılan gizleme, paralel yenilemeler boyunca korunuyor. Override VFX girişinde gerçek iki kaynak, lift başlamadan gizleniyor; resolver dışından gelen çağrılar da kapsanıyor. Pop/impact devam ederken gizlenen kaynakta alpha yazımı duruyor; formation görsel restore metodu da gizlemeyi koruyor. Temizleme verisi normal şekilde sonuçlanıyor; gizli kaynak için ikinci pop atlanması hücrenin boşaltılmasını engellemiyor.

Gizleme VFX bitiş callback'inde açılmıyor: görsel animasyon iptal olsa da kaynağı tüketen clear akışı hâlâ sahibi. Havuz iadesi, disable ve Init mevcut `ResetVisualState` üzerinden bayrağı, alpha ve input durumunu sıfırlıyor. Lifetime korumaları eski coroutine'in yeni taş kullanımına ulaşmasını engelliyor. Açık kurtarma yolu `ForceFullBoardSync` mevcut bayrağı sıfırlamaya devam ediyor.

Yeni tarama, Update, coroutine, VFX objesi, materyal veya ses kaynağı eklenmedi. Mevcut CanvasGroup tekrar kullanılıyor; ek çalışma kaynak başına bool ataması/kontrolü. Pop'u önceden tüketilmiş kaynakta atlamak gereksiz görsel işi azaltır; cihaz performansı ölçülmedi. Diğer kirli dosya değişiklikleri ve level yerleşimleri korundu.

Doğrulama (Unity doubles ile gerçek üretim metotları; board/cascade simülasyonu değil):

- `csi Tools/check_combo_source_visibility.csx`: 304 kontrol. Alpha-only kontrolü eski yeniden görünme yolunu üretir; yeni yolda iki kaynak paralel restore boyunca gizli kalır. Gecikmede/aktif animasyonda gizleme, iptal, tekrar gizlemede CanvasGroup kullanımı, gerçek reset metodu ve yeni lifetime kontrol edildi.
- `csi Tools/check_tile_lifetime.csx`: 30 kontrol. Gizli combo kaynağı için pop atlanınca hücrenin tam bir kez bırakılması dahil; 30/60/120 FPS.
- `csi Tools/check_presentation_lifetime.csx`: 266 kontrol.
- `csi Tools/check_override_sorting.csx`: 115 kontrol.
- İlgili kaynaklarda syntax ve `git diff --check` geçti.

Unity Play/build çalıştırılmadı. Gerçek Canvas/render davranışı ve kullanıcının cihazdaki aralıklı örneği henüz yeniden doğrulanmadı.
