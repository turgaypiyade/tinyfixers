# 226 v2 · bağlı duvar yolu ve ayrı değirmen adası

V1 kullanıcı tarafından reddedildi; v2 henüz kullanıcı tarafından oynanmadı/onaylanmadı. Önceki asset, kod, model ve PNG `Before/` altında saklanır.

Yeni 226: 9×11 / 68 aktif, 28 hamle; tek 24 hücre Wall55 (ortak origin31), altında24 Fener75, sağda hole ile ayrılmış iki Windmill80. Hedefler1/24/16; 36/36 doğal başlangıç taşı, pin0, boş0. Gizli24 fener hedefi karşılar; ek üretim0. Değirmenlere ana oyun alanından yalnız special ile erişilir. Yıkım mevcut beşinci hit ve komşuluk dalgasını kullanır.

## Yetkili değişiklikler

Yalnız226 asseti ve üç gerekli C# dosyası: LevelData.StackedObstacleEntry isteğe bağlı ortak duvar origin'i (0 eski davranış); GridSpawner.StampStackedEntry ortak kapak origin'i ve alt fener kaydı; LevelDataEditor.CountGoalObstacles tek parça sayımı. `Tools/review_level_patterns.py` aynı metadata'yı okur. Düşüş/akış, duvar animasyonu, audio ve FX kodu değişmedi. Ortak origin okuması O(1); yeni tarama, efekt veya per-hit allocation yok. Editör HashSet'i yalnız editör sayımındadır. CPU/GC/FPS ölçülmedi.

## Tekrarlanabilir kontroller

- `python3 Docs/LevelDesign/Batches/226-230-Lantern-Wind-Crystal/verify.py`: YAML/diziler/origin/katman/hedef, 36/36 doğal dolum, ada ayrılığı/special hizası, 5278 kapsam dışı oyun dosyası ve diğer levelların korunması.
- `csi Tools/check_stacked_wall_route.csx --before`: eski üretim stamp'inin24 bağımsız duvar parçası yarattığı tekrar üretildi.
- `csi Tools/check_stacked_wall_route.csx`: 265 gerçek yöntem kontrolü; ters sırayla stamp, 24 ayrı alt fener/origin/HP koruma, bütün hücrelerden dalga bağlılığı, eski alansız kayıtlar/geçersiz değerler ve editör hedefi.
- `csi Tools/check_wall_progressive_collapse.csx`: 1356 mevcut duvar zamanlayıcı/sırayla hücre bırakma/FX havuzu/iptal/hold ownership kontrolü.

Üç eski genel harness, DimerdalRules/Service/View doubles güncel olmadığı için derlenmedi. İlgili yedi üretim Spawner yöntemi Before ile byte-byte aynıdır; bu226 revizyonundan kaynaklanan bir regresyon değildir. `legacy-harness-status.json` tam kapsamı listeler. Bu kayıt genel regresyonların geçtiği iddiasını taşımaz.

Gerçek sprite önizlemesi ve kullanıcı referansı açıldı; v1 reddi ile v2 agent değerlendirmesi ayrı kayıtlıdır. `Docs/LevelDesign/Reviews/226-Wall-Route-v2/` tekrar envanteri;111 ve84 gerçek sprite karşılaştırması incelendi. Yakınlık puanı özgünlük veya onay yüzdesi değildir. Parent author ve not yazıcısı eski226'yı geri basamaz; aktif v2 author bu dizindedir.

Unity build/Play/cihaz, bot, düşüş/kaskat simülasyonu, yayın, release baseline ve commit yok. 1–225/227–230, katalog/TR–EN adlar ve GUID korundu. 28 hamle oynanmış denge ayarı değildir.
