# Missions — önceden hazırlanmış bulanık başlangıç görüntüsü

## Kullanıcı isteği

Arkadaki tamamlanmış şekil, gölgeli/paslı görünümden fazla kolay anlaşılıyordu.
Kullanıcı daha fazla pas yerine bulanıklık istedi: görevlerle görüntünün ne olacağını
keşfetmek istiyor. Denemeyi onayladı ve performansın öncelikli olduğunu özellikle belirtti.

## Uygulanan deneme

Sekiz `WonderDefinition` asset'ine küçük bir `unrestoredTexture` atandı. Kaynak resimler,
görev bölgeleri, sıra, yıldız maliyetleri ve kayıt anahtarları değiştirilmedi.
Missions restorasyonu ve Journey kartları aynı önceden hazırlanmış görüntüyü kullanır.
Tamir edilen bölge mevcut animasyonuyla netleşir; çevrenin mevcut kademeli geçişi korunur.
Tamamlanmış resim orijinal kaynaktan çizilir. Kıvrılıp düşen mevcut tabaka da aynı bulanık
görüntüyü kullanır; ayrı bir katman/nesne eklenmedi.

`Tools/bake_wonder_blur.py` kaynakları çevrimdışı işler: uzun kenar en fazla 512 px,
Gaussian sigma = uzun kenar × 0.032, doygunluk 0.65, kontrast 0.72, parlaklık 0.85.
Kaynaklar tekrar üretilmez/yeniden çizilmez. Üretilen RGB PNG'ler proje içinde kalıcıdır.
Geri almak için ilgili WonderDefinition'daki `unrestoredTexture` alanını boşaltmak yeterli;
eski paslı görünüm yedek olarak korunmuştur. Kaynak resim değişirse bake tekrar çalıştırılmalıdır.

## Performans ve ömür yönetimi

- Runtime blur, RenderTexture, GrabPass, ek shader pass veya ek UI Image yok.
- Kapalı pikselde yeni yol tek bilinear doku örneği alır; eski `weatheredImage` içindeki
  üç katmanlı prosedürel patina hesabından önce ortak bir materyal koşuluyla döner.
  Bu **beklenen maliyet değişimidir**, ölçülmüş GPU/FPS iyileşmesi değildir.
- Tamamlanmış bölgenin mevcut erken dönüşü bu örneklemeyi de atlar. Bölge sahipliği ve
  kaynak/temizleme efektlerinin mevcut shader hesapları devam eder.
- Yeni kare başına C# işlem/dizi/nesne/materyal üretimi ve board taraması yok.
  Doku görünüm kurulurken mevcut materyale bağlanır. Mevcut kıvrılma materyali tekrar kullanılır;
  her görev başında sadece doku ve mod bilgisi eşitlenir.
- İptal/disable davranışı değiştirilmedi. Görünüm ve kıvrılma materyalleri mevcut `OnDestroy`
  temizliğiyle bırakılır. Ödünç alınan asset dokuları `Destroy` edilmez.
- Dokular non-readable, mipmap kapalı, clamp + bilinear. iOS ve Android ASTC 6×6 override'ı var.
  Enum değeri 50 kurulu Unity API'sinden doğrulandı.
- 8 PNG toplamı **585.667 bayt**. Hesaplanan ASTC mip-0 veri yükü toplam **605.440 bayt
  (0,58 MiB)**; doku/metaveri/hizalama ek yükleri dahil değildir. RGBA32'ye açılmaları halinde
  toplam piksel belleği **5.337.088 bayt (5,09 MiB)** olur. Catalog sekiz tanımı doğrudan
  tuttuğu için yalnız birinin bellekte olacağını varsaymıyoruz.

## Doğrulama ve sınırlar

- `python3 -B Tools/bake_wonder_blur.py --check`: 8 kaynak/çıktı piksel eşleşmesi, asset GUID
  bağlantıları, boyutlar ve mobil importer ayarları geçti.
- `python3 -B Tools/review_wonder_tasks.py`: 64 görevin bölgeleri, TR/EN, ikonlar ve önceki
  görev dışı veriler korundu. Audit hash'i sadece yeni sunum doku alanını hariç tutar.
- `csi Tools/check_wonder_task_regions.csx`: 177.392 mevcut üretim bölge/yol kontrolü geçti.
- `csi Tools/check_obstacle_scatter.csx`: değişen C# görünüm/scene kodu kurulu Unity/UI/TMP
  API'lerine bağlandı; assembly üretilmedi. Mevcut 1.982 davranış kontrolü geçti.
- Değişen dosyaların whitespace kontrolü geçti. Repodaki başka seviye asset'lerinde bulunan
  mevcut whitespace değişikliklerine dokunulmadı.

`mis1.jpg` … `mis8.jpg` ve `All-Missions.jpg`, başlangıç → 3 görev → tamamlanmış durumunu
gösteren **statik uygulama önizlemeleridir**. Gerçek Unity ekran görüntüsü değildir; shader'ın
kenar gürültüsünü, kıvrılma animasyonunu, UI tint'ini ve cihaz sıkıştırmasını taklit etmezler.
Sekiz görselin tamamı görsel olarak incelendi. Mevcut görev dikdörtgenlerinin yaklaşık nesne
maskesi olması korunur; bu çalışma nesne segmentasyonu eklemez.

Unity arayüzü, Play Mode, proje build'i ve gerçek shader/GPU derlemesi çalıştırılmadı.
Cihaz FPS'i ve GPU süresi ölçülmedi; kullanıcının aynı Missions sahnesinde cihaz testi gerekir.

Yeniden üretme: `python3 -B Tools/bake_wonder_blur.py --preview` (Pillow gerekir).
