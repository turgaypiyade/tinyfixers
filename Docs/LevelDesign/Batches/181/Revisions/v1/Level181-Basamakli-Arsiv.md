# Level 181 — Basamaklı Arşiv

**REDDEDİLDİ — 2026-10-20:** Kullanıcı mevcut levellara benzer buldu. Katalog genelindeki inceleme 7/23/48/68'de benzer şekil/alan ilişkileri, 121'de aynı ColorChest+Wardrobe+oyma taşı grubunu buldu. Bu dosya ilk taslağın teknik kaydı ve karşı örneğidir; tamamlanmış veya onaylı level sayılmaz. [İnceleme](Reviews/181-Repetition/Review.md), [karşılaştırma](Reviews/181-Repetition/Comparison.png). Asset yeni revizyon için korundu.

Katalog 181 → `LevelP_001850.asset`, chapter 7. **9×11, 99 aktif, hole yok, 27 hamle, dört renk, Normal.** Reddedilen ilk taslak. [İlk önizleme](Level181-Basamakli-Arsiv.png).

Yeni tek hücreli renkli sandıklar (71–74) dinleniyor. Eski 2×2 ColorChest (21) son olarak 154'te kullanılmış; burada üç dolapla birlikte tek bir basamaklı arşiv oluşturuyor. Son gruptaki iki kanat/orta koridor kompozisyonu yerine alt ve sol kenara bağlı asimetrik bir kütle seçildi. Üst beş satır tamamen açık; sağdaki alan aşağıya doğru devam ediyor.

```text
ooooooooo
ooooooooo
ooooooooo
ooooooooo
ooooooooo
WWooooooo
WWooooooo
CCWWooooo
CCWWooooo
CCCCWWooo
CCCCWWooo
```

`o` otomatik normal taş, `W` 2×2 Wardrobe, `C` 2×2 eski ColorChest. Bütün gövdeler birbirine ve board kenarına bağlıdır; normal taşların içinde bağımsız engel adası yoktur. Sabit başlangıç renkleri, hazır special ve yardımcı engel eklenmedi.

Oyuncu geniş üst/sağ alanda special kurup basamağın dışındaki dolapları boşaltabilir veya içteki sandıkların kalan renklerine çalışabilir. Dolapların kalkması çalışma alanını genişletir; sıvı taban ise erken açılan alanı hâlâ hedef olarak tutar. Sağ alttaki üç katmanlı bölüm ayrı bir ilerleme kararı sağlar.

| Hedef | Adet | Davranış |
| --- | ---: | --- |
| Eski ColorChest · 21 | 3 | İlk hit kapağı açar. Sonrasında kalan dört renk ayrı ayrı tüketilir; normal hit uygun renkle, special kalan renklerden biriyle ilerler. |
| Wardrobe · 27 | 3 | Kapak sonrası mevcut library'de dolap başına 10 eşya. Normal hit bir, special iki eşya çıkarır. |
| SculptingStone · 28 | 4 | Sağ alt dolabın altında; hücre başına üç vuruş. |
| Mud · 25 | 24 | Arşivin tamamının altında; hücre başına iki aşama. |

Koordinatlar sıfır tabanlı, y yukarıdan aşağıya: Wardrobe origin'leri 45 `(0,5)`, 65 `(2,7)`, 85 `(4,9)`; ColorChest origin'leri 63 `(0,7)`, 81 `(0,9)`, 83 `(2,9)`. Her gövde 2×2. Mud her hücrede ayrı taban origin'idir.

Beş gövdenin katmanı `Mud → ColorChest/Wardrobe`. Sağ alt gövdede `Mud → dört SculptingStone → Wardrobe`: taşlar 85/86/94/95 hücrelerinde `stackOrder=1`, tek dolap 85 origin'inde `stackOrder=2`. Üst gövde alttaki dört taşı tamamen örter. Sıvı üstte kullanılmadı; hareketli engel yok.

**27 hamle ilk denge ayarıdır.** Geniş alanda üretilen special'ların dolap eşyalarına verimli vurmasını, renk sandıklarının ardından açılan çamurla birlikte planlamak amaçlanıyor. Normal levellardaki %40–50 yönü hedef olmaya devam ediyor; bu levelin geçiş oranı ölçülmedi.

Statik doğrulama `Batches/181/verify.py` ile geçti: Unity YAML ayrıştırma, yedi veri dizisi, origin/footprint, tam örtme ve katman sırası, hedef adetleri, tanıtılmış engeller, katalog/GUID, isim ve önceki dosya koruması. **75 başlangıç taşı hücresinin tamamı doğrudan dikey dolum alabiliyor.** Önceki 1.859 ayar/script dosyası byte düzeyinde korundu; katalog yalnız 181 eklenerek uzatıldı. Gerçek sprite'larla önizleme render edilip incelendi; renkler temsilidir. Unity Play/build veya oyun/bot/kaskat simülasyonu çalıştırılmadı.

Performans incelemesi: yeni runtime/VFX kodu yok. Mevcut `CollectCellsForOrigin` her hitte board tarayıp List/ToArray tahsisi yapıyor. Wardrobe görünümü açılışta eşya/kapak GameObject'leri ve iki Sprite.Create üretir; bunlar havuzlanmıyor. Üç dolapta toplam 30 eşya ve altı kapak bulunur. Normal tamamlanmada eşya/kapak objeleri coroutine sonunda, dolap görünümü son eşyanın düşüş süresinden sonra yok edilir. Parçalar dolabın çocuğudur; görünüm/scene yok edilmesiyle objeler ve bu component'teki coroutine'ler sonlanır. Ayrı OnDisable/reset temizliği ve üretilen yarım sprite'ların açık Destroy çağrısı yok; mevcut sınıra rağmen ölçülmüş performans/eksiksiz havuz güvenliği iddiası yapılmaz. Bu asset çalışmasında ortak servis değiştirilmedi; cihaz profili alınmadı.

181'in authoring kaydı, önceki katalog ve doğrulama çıktısı `Batches/181/` içindedir. İsim kalıcı ürün kataloğuna eklendi. 173–180 rollback aracı daha sonraki katalog değişikliği nedeniyle güvenlik kontrolünde duracaktır; 181'i korumadan eski toplu geri alma uygulanmaz.
