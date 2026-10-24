# Water Lily hint identity — 2026-10-22

Kullanıcı Water Lily'nin çimi kullanan tek obstacle olduğunu, overlay'de ayrı Grass ve Water Lily olarak tanıtılmaması gerektiğini belirtti.

`GridSpawner.ConvertGrassFlowersToGrass`, Water Lily'yi runtime Grass tabanına dönüştürüp hücresini Water Lily kaydında tutuyor. Hint toplama hem bu runtime Grass'ı hem kayıtlı GrassWaterLily'yi eklediği için nilüfer-only sahnede iki tanıtım oluşuyordu.

Hücre bazlı `GetHintObstacleId` yalnız kayıtlı nilüferin runtime Grass'ını GrassWaterLily olarak tanır. Hint manager bu kimliği üst hücrelerde, dönüştürülmüş authored katmanlarda ve saklı katmanlarda kullanır. `CollectStampedBeneathIds` varsayılan davranışını korur; yalnız hint çağrısı kimlik dönüşümünü ister. Böylece gerçek Grass bulunan karışık levellarda Grass tanıtımı kaybolmaz; çizim ve oyun mantığı hâlâ gerçek runtime katmanlarını görür.

Highlight toplama da aynı hücre kimliğini kullanır. Grass tanıtımı artık bütün grass root'unu ve altındaki nilüfer grafiğini toplamaz. Water Lily tanıtımı kendi nilüfer ve alt çim görsellerini birlikte vurgular. Diğer obstacle, hedef ve hasar kuralları değiştirilmedi. Prelevel hedef ikonları zaten authored goal.obstacleId üzerinden seçiliyor; hedefleri otomatik silme/yeniden yazma eklenmedi.

Performans: HashSet üzerinden O(1) hücre kimliği sorgusu; yeni Update, coroutine, event aboneliği, VFX veya pool eklenmedi. Mevcut başlangıç hint taraması ve gerektiğinde highlight toplama kullanılır; kare başına yeni board taraması veya allocation eklenmedi. Performans cihazda ölçülmedi.

`csi Tools/check_obstacle_hint_collection.csx`: 22 üretim-yöntemi kontrolü geçti. Nilüfer-only, gerçek Grass ile karışık sahne, kanal alt/üst katmanı, movable altı, authored stack, varsayılan raw collector, null çıktı, kayıt temizliği ve mevcut engel ailelerinin deduplikasyonu kapsandı. Üç değişen C# dosyası syntax kontrolünden geçti. `git diff --check` geçti. Unity build/Play veya bot/kaskat simülasyonu çalıştırılmadı; gerçek overlay görünümü Unity'de henüz denenmedi.
