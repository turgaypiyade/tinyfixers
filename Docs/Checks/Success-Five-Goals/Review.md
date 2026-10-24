# SuccessPopup — beş hedef karşılaştırması · 2026-10-23

Kullanıcı PreLevelPopup ve oyun HUD'unda5 hedefin iyi göründüğünü, SuccessPopup'ta sığmadığını belirtti. Alan genişliğinin küçük olabileceğini tahmin etti; ölçüleri karşılaştırmamızı ve çalışan dizilişi korumamızı istedi.

Prefab ve canlı01_Game sahne kayıtları kontrol edildi:

| Ölçü | PreLevel | Success |
| --- | --- | --- |
| Hedef container |600×130 |650×200 |
| Slot başlangıç genişliği |105 |120 |
|5 hedefte slot başına etkin genişlik |120 |96 |
|5 slot toplamı |600 |480 |

PreLevel, popup yüksekliği/genişliği ve105 birimlik slot üzerinden ölçeği sınırlar; layout wrapper ölçeklenmiş boyutu rezerve eder. Success mevcut satır ölçeğini4/5 uygular. Success container'ı daha küçük değildir; yalnız container genişliğini artırmanın gerekli olduğu kaynak ölçüleriyle doğrulanmadı.

Somut fark: Success'ın ilk4 ikonunun kendi slotundaki yatay ofsetleri−100/−90/−80/−70. Eksik5. slot birinci slottan kopyalanınca yeniden−100 ofseti alıyordu.120 genişlik,100 genişlikte ikon ve0,8 ölçekle son iki ikon arasında−8 birim boşluk, yani8 birim üst üste binme oluşur. [Serileştirilmiş ölçü raporu](Comparison.json) prefab ve sahne için aynı sonucu verir. Bu ölçü hesabıdır; gerçek cihaz piksel ölçümü değildir.

`EnsureGoalResultSlot` yeni slotu artık en yakın önceki mevcut slottan kopyalar; ilk slota fallback korunur.5. slot4.'ün ofsetlerini alır ve aynı hesapta aralık+16 olur. Mevcut4 slotun konumları, ikon/tik ölçüleri, container ölçüleri ve çalışan layout/ölçek kodu değiştirilmedi. Geniş çaplı normalizasyon denemesi karşılaştırmadan sonra geri alındı; nihai kod yalnız kopya şablonu seçimini değiştirir.

Yeni slot mevcut yoldaki gibi bir kez oluşturulur, sonraki popup açılışlarında yeniden kullanılır. Yeni Update, layout taraması, görsel materyali, ses veya VFX eklenmedi. Tam controller syntax ve değişen metodun kurulu Unity UI metadata bağlaması geçti; assembly üretilmedi. Unity Play/build ve telefonda gerçek görünüm doğrulaması yapılmadı.
