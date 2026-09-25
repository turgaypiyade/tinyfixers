# Gerçek oyun simülasyonu

`TinyFixers > Level Difficulty — Real Game Bot` veya `TinyFixers > Run Sim Bot`
menüsü gerçek oyun sahnesini oynatan bot penceresini açar. Saf C# simülasyon araçları
`TinyFixers > Legacy Headless` altında kalır.

Bir LevelData seç, oyun sayısını ve bot politikasını ayarla, ardından
`Play Mode'da başlat` düğmesini kullan. Referans ölçüm için oyun hızını 1× tut.
`Adım zaman aşımı (sn)` sahne yükleme, tahta hazırlığı, hamle çözümleme ve
hamlesizlikte yeniden karıştırma beklemelerini sınırlar; tüm oyunun süre sınırı değildir.

JSON ve CSV çıktıları `Library/LevelSimulation` altına yazılır. `Son raporu oku`
sonuçları gösterir. Yalnızca `Won` ve `Lost` kazanma oranının paydasına girer;
`Error`, `Timeout`, `NoMoves` ve `Cancelled` ayrıca raporlanır. Bu oran bot
performansıdır; insan oyuncunun kazanma olasılığı olarak kalibre edilmemiştir.

## 2026-09-22 devam çalışması

- Pencerenin level, oyun sayısı, seed, bot, hız ve zaman aşımı ayarları domain reload
  boyunca korunacak şekilde serialize edildi.
- İlk sahnenin Awake/Start hatalarının ilk oyun başlamadan silinmesi düzeltildi.
- Bot ve iç içe çalışan çözümleme coroutine'lerindeki istisnalar oyun sonucuna alınır.
- Sahne yükleme ve hamlesizlik çözümlemesi sırasında iptal/zaman aşımı izlenir.
  Tamamlanamayan sahne yüklemesinden sonra koşu durur.
- İptalde ve Play Mode elle durdurulduğunda devam eden oyun kısmi rapora alınır.
  Tamamlanmış sonuçlar korunur; kısmi rapor toplam planlanan oyun sayısını gösterir.
- Rapor okuma/yazma hataları pencerede gösterilir. Çıkışta zaman ölçeği, rastgele
  sayı üreteci ve arka planda çalışma ayarı geri yüklenir.
- Bot yeni hamle seçmeden önce `Flow.IsSettling`, devam eden sütun düşüşleri,
  hücre rezervasyonları ve taşların runtime durumlarını da bekler. Ayrı devam eden
  görsel işler bitmeden tahta üzerinde hamle önizlemesi yapmaz.
- Hamle izlerinde `emptySlots`, oyun sonucunda `emptyCellsAtEnd` boş taş hücrelerini
  koordinatlarıyla kaydeder. Hemen üstte engel varsa `blockedAbove` ile belirtilir;
  bu alan tek başına hücrenin dolabilir olduğunu söylemez.
- Botun varsayımsal takas sorguları ve bekleme sorguları ayrıntılı MatchFinder
  dökümleri üretmez; gerçek çözümleme çağrılarının tanılama logları korunur.

## Unity içinde doğrulanacak senaryolar

1. Tek level, iki oyun, 1× hız: iki sonuç, doğru sample/seed değerleri, JSON ve CSV;
   tamamlanan koşuda `completed=true`.
2. İlk oyun hazırlanırken veya hamle çözülürken `Koşuyu durdur`: devam eden oyun
   `Cancelled`, `completed=false`; kazanma oranına eklenmez.
3. İkinci oyun sırasında Unity'nin Play düğmesiyle çık: ilk sonuç korunur, ikinci
   sonuç kısmi raporda yer alır; önceki Play Mode başlangıç sahnesi geri yüklenir.
4. Hamlesiz tahta yeniden karıştırma akışında iptal veya zaman aşımı: koşu takılmaz;
   iptal `Cancelled`, süre aşımı `Timeout` olur.
5. Bot sorgusunda/çözümleme coroutine'inde istisna: oyun `Error` olarak kaydedilir;
   sonraki oyun yeni sahneyle başlar.
6. Rapor dosyası okunamıyorsa veya çıktı dizinine yazılamıyorsa: hata görünür,
   başarılı kayıt mesajı gösterilmez.
7. Play Mode'a girip çıktıktan sonra seçili level ve bot ayarları korunur.
8. `LevelP_00920`, seed 42, 1×: special zincirleri ve ayrı devam eden düşüşler
   bitmeden bot yeni hamle başlatmamalı. Boşluk sürerse hamle izindeki koordinatları
   kontrol et; engellerle çevrili hücre ile doldurulabilir açık hücreyi ayır.
9. Devam eden düşüş/rezervasyon takılırsa sonuç `Timeout` olmalı; bekleme nedeninde
   ilgili aktivite/sütun/hücre bulunmalı, koşu bunu `Lost` olarak saymamalı.

Bu devam çalışmasında kaynak/diff kontrolleri yapıldı. Unity Play Mode senaryoları
ve build çalıştırılmadı.
