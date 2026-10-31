# ProLevels V1 — 12–15 öncesi başlangıç akışı kontrolü

> Tarihsel hazırlık kaydıdır. İlk taslaklar sonradan kullanıcı tarafından reddedildi.
> Güncel v2 ve kontrolleri [batch notunda](../../Batches/ProLevels-V1-011-015/README.md);
> henüz kullanıcı onayı yoktur.

28 Ekim 2026, ilk inceleme anının kaydı. Kullanıcı önce kuralları ve mevcut başlangıcı
kontrol etmemizi istedi; bu aşamada yeni level oluşturulmadı.
**Daha sonraki "yalnız11–15" isteğiyle12–15 oluşturuldu;11 korundu. Güncel tasarımlar ve
durum: `../../Batches/ProLevels-V1-011-015/README.md`. Aşağıdaki boş slot tespiti öncesini anlatır.**

## Geçerli düzen ve kullanıcı sınırları

- Katalog: `Assets/_Project/Settings/ProLevels/LevelCatalogProV1.asset`.
- Yeni çalışma dosyaları: aynı klasörde `LevelV1_*.asset`; eski `LevelP_*` havuzu referanstır.
- Onluk set başına en az2/en fazla3 yeni obstacle; 5 Zor, 9 Daha Zor, 10 Boss Duel.
- 12–14 Normal hedef %55–85, 15 Zor hedef %35–55. Bu yüzdeler ölçülmüş kazanma oranı değildir.
- İzinli engeller: tek-hit sandık4, iki-hit sandık5, üç-hit sandık6, çamur25, yağ14.
- Bu başlangıç dönemi için eski 170+ Normal %40–50 yönü geçerli değildir.

## Dosyalardan doğrulanan durum

| Sıra | V1 dosya sonu | Tahta | Hamle | İlk görülen ID |
|---|---|---|---|---|
| 1 | 00000010 | 9×9 | 30 | Sandık1 / ID4 |
| 2 | 00000020 | 9×9 | 27 | Sandık2 / ID5 |
| 3 | 00000030 | 9×9 | 27 | — |
| 4 | 00000040 | 9×10 | 30 | Sandık3 / ID6 |
| 5 | 00000050 | 9×9 | 30 | — |
| 6 | 00000060 | 9×9 | 30 | Çamur / ID25 |
| 7 | 00000070 | 9×9 | 33 | — |
| 8 | 00000080 | 9×10 | 30 | — |
| 9 | 00000090 | 9×9 | 22 | — |
| 10 | 00000100 | 9×7 | 25 | Boss Duel; yağ atışı kapalı |
| 11 | 00000110 | 9×10 | 30 | Yağ / ID14 |

12–15 katalogda gerçekten boş (`levelData: {fileID: 0}`).
`LevelV1_00000120.asset` … `LevelV1_00000150.asset` dosyaları yok.
16'da ID8, 17'de ID11, 18'de ID7 var; 12–15'in havuzuna bu sonraki plastikler alınmayacak.

**Tanıtım sayımı ayrımı:** ilk10'da ayrı ID olarak4/5/6/25 görülüyor; sandığın aşamalarını
tek ailede toplarsak sandık+çamur var. 11–20'de14/8/11/7 ilk kez görülüyor; plastikler
renk varyantları. Bu envanter, aile ve varyantları ayrı tutar. Kullanıcının "2–3 obstacle"
kuralından ayrıca bir sayım tanımı çıkarmak için geçmiş levellar değiştirilmez. 12–15'in
bu tur izinli havuzu zaten açıkça verildiğinden mevcut dört boşluk için yeni bir tanıtım
varsayımı gerekmez.

## Agent'ın tasarım değerlendirmesi — kullanıcı onayı değil

- İlk2/4'te benzer V/kase alt kütlesi, 3/7'de aynalı basamak, 8'de tam sütunlar var.
  Yeni12–15'in dördünü de bu şablonların birer kopyası yapmak yerine ayrı okunaklı fikirler seçilmeli.
- 11'in 80 aktif hücresinde23 tek-hit sandık ve36 yağ var;36 yağ4 tam alt sırayı kaplıyor.
  Başlangıçta21 hücre örtüsüz/etkileşime açık. Bu sayı hamle/special/refill davranışını simüle
  etmez; yağın altındaki tutulan taşlar ve sandık aşamaları nedeniyle oyun yükü ayrıca değişir.
- 12 için rahat çalışma alanıyla bildik mekanikleri pekiştirme; 13–14 için küçük kontrollü
  yük/yerleşim farklılıkları; 15 için başlangıç dönemine göre bir Hard zirvesi uygun yön olabilir.
  Bu bir tasarım önerisidir; henüz kompozisyon/hamle/hedef kararı verilmedi.
- Sandıklar bağlı bant, kenar veya tanınır şekli tamamlamalı. Sıvı tabanda, katı üstte;
  dolum/erişim ve katman sonrası kalan hedef ayrıca kontrol edilmeli. Her Normal'e bütün
  beş izinli ID'yi doldurmak zorunlu değil.

## Kontrol kapsamı

`Inventory.json`: ilk20 slotun katalogdan çözülen asseti, hedefleri, mevcut katmanları,
hamlesi, aktif/açık hücreleri ve ilk görülen ID'ler. GUID/isimden sıra tahmin edilmedi.
`Existing-001-011.png`: 1–11'in güncel proje sprite'larıyla incelenen açılış kompozisyonları;
normal taş renkleri temsili, oyun ekran görüntüsü değildir.

767 mevcut V1/eski üretim leveli, katalog ve meta dosyasının SHA-256'ı korundu.
Kurallar/tasarım durumu güncellendi. Unity arayüzü, Play/build, bot/kaskat simülasyonu,
geçiş oranı veya cihaz performansı ölçümü yapılmadı.
