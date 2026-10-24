# Pulse–Pulse jel aktarımı · 2026-10-23

Kullanıcı jelli Pulse–Pulse birleşmesinin patlattığı alana jel aktarımında sorun olup olmadığını kontrol etmemizi istedi.

Gerçek oyuncu swap yolu `SpecialResolver.ResolveSpecialSwap` içindeki Pulse–Pulse dalıdır. Eski `PulsePulseCombo.Execute` oyuncunun bu yolu değildir. Resolver iki kaynağı da `NoteGelSpreadOrigin` ile bildirir; tek geniş patlama için `SpecialChainRunner`'a yalnız ilk Pulse (`a`) emitter olarak verilir, ikinci Pulse (`b`) consume listesinde kalır. Runner hedef taşları ve varışta zincirlenecek special'ları açıkça bulaşık işaretlerken yalnız emitter'ın `IsGelSpreadSourceTile` sonucunu kullanır.

Yalnız ikinci kaynak jelliyse hamle-geneli kapsam açılsa da ilk emitter kuru kalıyordu. Böylece hedeflere/special'lara kalıcı bulaş aktarımı atlanıyordu. Çoğu normal hedef hamle kapsamından jel alabilir; bu nedenle her patlamanın tamamen jelsiz olduğu iddia edilmez. Önceki jelsiz eşleşmeden muafiyeti kalan hedef, ya da gecikmeli kırılmadan önce kapsam sıfırlanması, eksik açık işareti görünür yapar.

Nihai değişiklik yalnız resolver'ın Pulse–Pulse dalında: ikinci kaynak gerçekten yayılabilir jel üzerindeyse veya `GelContaminated` ise ilk emitter da bulaşık işaretlenir. Runner'ın mevcut hedef ve arrival-special bulaştırması bu iki kaynaklı birleşmenin özelliğini taşır. Birinci kaynağın mevcut davranışı korunur. Salt etki alanından geçilen jel, hole veya örtü altındaki mühürlü seed yeni combo kaynağı sayılmaz. Bulaş gerçek kırılma anında mevcut boyama yoluyla çizilir; animasyon, yarıçap ve dalga zamanlaması değişmedi.

`csi Tools/check_pulsepulse_gel.csx`: eski resolver snapshot'ıyla68 kontrol ve güncel kaynakla72 kontrol geçti. Gerçek Pulse–Pulse resolver dalı, gerçek `ExplodePulse` toplama coroutine'i ve BoardController'ın kaynak/işaret/boyama metotları doubles ile çalıştırıldı. Partner-only eksiklik eski kaynakla tekrarlandı; iki kaynak sırası, açık jel/taşınan bulaş, gecikmede kapsam sıfırlanması, önceki jelsiz-match muafiyeti, zincir special'ı, kuru kaynak, mühürlü seed, alan dışı/hole/caged/obstacle-owned movable kontrolleri kapsandı. Bu bot veya board/cascade simülasyonu değildir.

Kaynakta ekleme O(1) kaynak kontrolü ve bool atamasıdır. Ek board taraması, target listesi, per-frame işlem, coroutine, VFX objesi, materyal veya ses kaynağı eklenmedi. Bayrak mevcut TileView pool/disable/Init reset'iyle temizlenir; bu değişiklik yeni yaşam süresi sahibi oluşturmaz. Performans ölçümü yapılmadı.

1.898 kaynak/Settings snapshot'ından yalnız resolver değişti; diğer1.897 dosya aynı. Mevcut kullanıcı değişiklikleri ve level yerleşimleri korundu. Syntax ve ilgili `git diff --check` geçti. Unity Play/build, gerçek Canvas/cihaz görünümü veya oyun içi olay kaydıyla doğrulama yapılmadı.
