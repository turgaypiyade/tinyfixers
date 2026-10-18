# Her engelin hint’i olmalı

Yeni obstacle tamamlanma ölçütü: ObstacleLibrary kaydıyla birlikte `Data/Tutorial/ObstacleHintLibrary.asset` girdisi, TR/EN başlık/açıklaması ve ilk karşılaşmada çalışan runtime algılama gerekir. Yalnızca görsel veya hedef ikonu eklemek yeterli değildir. Hasar/yutma gibi oyuncunun bilmesi gereken kural değişirse açıklama aynı değişiklikte güncellenir.

`01_Game` sahnesinde hint açık ve doğru Library bağlı. Oyuncu onayladıktan sonra `obstacle_hint_seen_<id>` kaydedilir; sonraki karşılaşmada tekrar gösterilmez. Denemek için sahnedeki **ObstacleHintManager → Reset Obstacle Hints (Show Again Next Level)** context menüsü kullanılabilir. Bütün eski hintleri tekrar açacağı için kayıtlar kullanıcı adına sıfırlanmadı.

2026-10-17 incelemesi: 63 tanımlı engelin tamamında benzersiz hint ve TR/EN metin var. Nilüfer (69) ve bilardo deliğinin (67) eksik kayıtları tamamlandı. Rodos (70), brandalı heykel (68), lazer, boya kasası, Cargo, HatLauncher ve jel yönleri mevcut.

Nilüfer/GrassFlower kurulumda runtime Grass’a dönüştüğü için yalnız level.obstacles taraması onları kaçırıyordu. Hint toplayıcı artık kayıtlı çiçek hücrelerini de okur. Hint sonrası vurgulama gerçek nilüfer/çiçek görsel kökünü kullanır. Bu işlem ilk kurulumda yapılır; kare başına yeni tarama eklenmedi.

Kontroller: `python3 Tools/check_obstacle_hint_coverage.py` bütün Library, TR/EN metinleri ve sahne bağlantısını doğrular. `csi Tools/check_obstacle_hint_collection.csx` gerçek toplama metoduyla dönüşmüş Grass, yeni engeller, katmanlar ve tekilleştirmeyi kontrol eder. UI açılışı/tıklama ve sahne görünümü Unity’de ölçülmedi; Play Mode/build çalıştırılmadı.
