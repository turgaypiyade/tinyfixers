# Bridge Repair oyuncu karakteri

Oyuncunun yürüyen/tamir eden karakteri `PlayerProfile.AvatarId` seçimini kullanır. `AvatarLibrary.characterIds`, avatar sprite'larıyla aynı indekste hayvan kimliği tutar; kostüm çeşitleri aynı hayvana gider. Mevcut sıra: koç, ayı, domuz, tavşan, maymun, koç, ayı, maymun, tavşan, domuz. Bu sıra BridgeRepairConfig karakter sırasıyla aynı değildir.

`PlayerAvatarProvider.CurrentCharacterId` ortak avatar kütüphanesinden kimliği okur. `BridgeRepairRace.BuildContestants` oyuncuyu bu kimlikle eşleştirip ilgili karakteri oyuncu koltuğuna taşır; kalan bot karakterleri ayrı kalır. Yeni rastgele sayı tüketilmez. Eksik eşleştirme/tanımda ilk karakter sabit fallback'tir. Avatar ID sınırları portre seçimiyle aynı şekilde ele alınır.

Yarış haritası her açılışta, kazanma ekranı da gösterilmeden önce aynı `BuildContestants` metodunu kullanır. Bu nedenle devam eden koşuda da profil seçimi bir sonraki açılışta geçerlidir; ek kayıt/migrasyon, yeniden katılım veya ilerleme sıfırlama gerekmez. Yeni frame döngüsü, event aboneliği veya görsel üretimi eklenmedi; beş tanımlı hayvan içindeki kimlik araması yalnız yarışmacılar hazırlanırken yapılır. Cihaz performansı ölçülmedi.

`csi Tools/check_bridge_profile_character.csx`: 359 kontrol; on avatarın beş farklı koşu seed'inde doğru hayvanı seçmesi, portre/isim uyumu, tekrar açılış, farklı bot karakterleri, profil değişirken şeritlerin korunması, yeniden sıralanmış config, sınır dışı ID ve eksik tanımlar doğrulandı. Mevcut sprite referansları ve profil kayıt anahtarları korundu. Unity oynatımı/build çalıştırılmadı.
