# v2 · 225 alt besleme + Dimerdal vuruş/kota görünümü

2026-11-05. Kullanıcı: “hepsi güzel”; sadece225'in alt boşluğunu besleyen geçiş ve Dimerdal vuruş animasyonu/üretim sonrası gri kapalı görünüm istedi. 222–224 görsel olarak onaylıdır ve aynen korundu. 225 için genel beğeni vardır; istenen yerel düzeltme uygulandı. Oynanmış denge onayı çıkarılmaz.

225: maske8×11,88aktif,25hamle,hedef16/1/16/16,mühür9/9/9,GUID/meta/isim/katalog korunur. Pusulalar iki1×8sütuna, kutular alt köşelere taşındı; x1/x6besleme şeritleri ve kutular arasında4normal hücre genişliği açıldı. Başlangıç boş yuva12→2,doğal dolan36→46. Alt iki sıra başlangıçtan dolu; son iki boş yuva mührün altındadır. Pin veya motor değişikliği yok.

`Before/` önceki asset/model/PNG ve üç Dimerdal kaynağını saklar. `verify.py` yalnız iki geometryarray satırı değişimine izin verir;3391diğer dosya aynı. Parent`verify.py` aktif revizyonu çağırır; parent`design.py`v1ile yeniden yazmayı reddeder. Güncel PNG açılıp incelendi; diğer üç PNG'nin hash'i aynı.

Dimerdal: kaynak kökü sabit; ayrı child görsel0.26sn geri teper/büyür, aynı aperture Imageparlar. Kabul edilen açma ve üretim vuruşu bunu başlatır. Son başarılı yerleşimden sonra ortak kotadaki tüm kutular0.30sn'de kapanır;gray UI material ve45%alpha. Rezervasyon/uçuşbitmeden kapanmaz; hata/iptal kotayı geri verir. Yeni/reveal görünüm grup halini devralır. StageHP/hedefte kalanDiamond,gravity,collection ve audio değişmedi.

Performans/teardown: her hostta frame/flash bir kez kurulur; tekrar vuruşta GameObject/Texture/Material üretilmez. Hit/close/fade tek mevcut servis döngüsünde yalnız aktif view listesinde; quota geçişiO(kutu),herkare boardscan yok. Spent materialResources assetidir ve paylaşılır;uGUI stencil türevlerinin yaşamını kendi MaskableGraphic sistemi yönetir. Disable/build geçişi transient pose/flash/list/jobs temizler.100tekrar hitte yeniGameObject olmadığını izole kontroller doğrular;cihaz CPU/GC/FPSölçülmedi.

Kontroller:409mevcut üretim/yerleşim/yaşam döngüsü+39MatchFinder+46feedback/kota/reveal/cancellation;sharedTarpStatuecollectrutini.Unitydoubles kullanıldı; Unity/GPUcompile/Play/cihaz/simülasyon yok. Yeni shader kaynak kontratı alfa/stencil/RectMask2D+clip/grayscale ile denetlendi. Shader görseli henüz cihazda görülmedi. Build,yayın,releasebaseline yok.
