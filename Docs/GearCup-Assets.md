# Gear Cup — görsel inceleme

13 kaynak PNG incelendi: 6 ekran parçası ve 7 tavşan karesi. Kaynak PNG ve import ayarları değiştirilmedi. Bu belge oyun entegrasyonu değildir.

## Ekran parçaları

| Dosya | Kullanım | Kaynak boyutu |
|---|---|---|
| `Futuristic Blue Arena Stage.png` | Boş ekran arka planı | 941 × 1672 |
| `Futuristic Gold-Accented Sci-Fi Podium.png` | Bağımsız karakter platformu | 2048 × 768 |
| `Glossy Golden Gear Icon.png` | Bağımsız dönen dişli / puan ikonu | 1254 × 1254 |
| `Glossy Silver Shield Badge.png` | Takım sıralamasının küçük boş kalkanı | 1254 × 1254 |
| `Glossy Silver Shield UI Badge.png` | Ödül kartlarının geniş boş sıra rozeti | 1774 × 887 |
| `Glowing Cyan Elliptical Portal Ring.png` | Bağımsız platform ışık halkası; platformla konumu ve ölçeği ayrıca ayarlanmalı | 2048 × 768 |

## Tavşan kareleri

Bütün kareler 1024 × 1536. Ayak tabanı, kaynak PNG üzerinde alfa ≥ 128 sınırıyla ölçüldü; görüntünün üst kenarından piksel cinsindendir. Düşük alfa gürültüsü ölçüme katılmadı.

| Sıra | Dosya | Poz | Ayak tabanı Y |
|---|---|---|---|
| 0 | `Cheerful Bunny in Sporty Streetwear.png` | Ana açık göz | 1508 |
| 1 | `Half-Blinking Teal Jacket Bunny.png` | Yarı kapalı göz | 1508 |
| 2 | `Happy Bunny Blink Render.png` | Kapalı göz | 1508 |
| 3 | `Cheerful Rabbit with Raised Hand.png` | Kol kaldırma / hazırlık | 1509 |
| 4 | `Cheerful Bunny in Teal Bomber Jacket.png` | Uzanma | 1509 |
| 5 | `Cheerful Bunny Mascot Reaching Out.png` | Dokunma / küçük itme | 1508 |
| 6 | `Joyful Rabbit Reaching Skyward.png` | Gövdeyle yukarı uzanma | 1479 |

## Sonuçlar

- Göz seti ve ilk kol pozları taban hizasında 0–1 piksel farkla uyuşuyor. Üretim sırasında gövde dokusunda küçük değişiklikler var; nihai ekrandaki boyutta değerlendirilmelidir.
- Son gövdeyle uzanma karesinin tabanı ana pozdan 29 piksel yukarıda. Önizlemedeki +29 piksel Y düzeltmesi bu farkı telafi eder; bu bir kaynak görsel düzenlemesi değildir.
- Son pozun kafa, gövde ve denge kolu da değişiyor. Dikey hizalama tek başına bu geçişi yumuşatmaz; son poza geçerken ara kare gerekir. Uzanma/dokunma pozları birbirine yakın, kaynaklar silinmedi.
- Beyaz ve koyu zemin üzerinde şeffaflık kontrol edildi. Karakterler, platform, halka, dişli ve rozetler ayrı katmanlar; metin gömülü değil.
- Modern kıyafetli domuzun dört karesi sonradan eklendi; aşağıdaki ek envanterde listelenmiştir.
- Panel/satır gövdeleri için mevcut Team/Rank görselleri; bilgi/kapatma/arama, takım amblemleri ve ödül ikonları için mevcut oyun görselleri kullanılacak. Metin, sıra ve sayılar lokalizasyona bağlı olacak.

## Performans ve kapsam

- 7 tavşan kaynağı sıkıştırmasız RGBA8 olarak toplam 42 MiB eder. Bu kaynak boyutundan yapılan hesaptır; Unity veya iPhone üzerinde ölçülmüş GPU belleği değildir. Nihai görünür boyuta göre import çözünürlüğü ve mobil sıkıştırma seçilmeli.
- Animasyon oynatımında sprite referansları önceden yüklenmeli; kare değişiminde PNG çözme, Instantiate/Destroy veya yeni texture üretimi yapılmamalı. Ekran kapanınca animasyon durmalı.
- İnceleme için bir HTML önizlemesi üretildi; ham kaynakları içerdiğinden yaklaşık 25 MB. Bu dosya oyun paketine eklenmedi.
- Unity Play Mode, build ve cihaz performans testi çalıştırılmadı.

## Domuz kareleri (ek envanter)

1024 × 1536 boyutundaki dört PNG kaynaklarına dokunulmadan bağlandı. Import ayarları en çok 1024, Full Rect, sıkıştırma açık, mipmap ve Read/Write kapalıdır. İlk envanterdeki import ayarı notu bu sonradan yapılan entegrasyonu kapsamaz.

| Sıra | Dosya | Poz | Ayak tabanı Y |
|---|---|---|---|
| 0 | `Cheerful Pig in Varsity Jacket.png` | Nötr | 1497 |
| 1 | `Cheerful Pig Mascot Ready to Celebrate.png` | Hazırlık | 1492 |
| 2 | `Cheerful Pig in Varsity Jacket2.png` | Eller göğüs hizasında | 1486 |
| 3 | `Celebrating Cartoon Pig Mascot.png` | Kutlama | 1494 |

Taban ölçümü alfa ≥ 128 üzerinden yapılır. Unity Y konumu, her pozda tabanı nötr kareyle hizalar. Kaynak PNG üzerinde yeniden boyutlandırma veya piksel düzenlemesi yapılmaz. Dört ana poz dönüşte tekrar kullanılır; göz kırpma karesi yoktur.
