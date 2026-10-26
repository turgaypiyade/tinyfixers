# Firestore kuralı — Takım can istekleri

Kod: `FirebaseTeamCloud.RequestLife / HelpLife`, `FirebaseTeamService.OnLifeRequests`, `TeamLifeInbox.ReceiveRealHelpers`.

Veri: `teams/{teamId}/lifeRequests/{uid}` — her oyuncunun takımda tek aktif isteği (doküman id = kendi uid).
- `requesterId` (= uid), `requesterName` (≤ 80), `needed` (1-5), `helpers` {uid: ad}, `createdAt` (sunucu zamanı)
- Sahibi yeni istekte dokümanı baştan yazar (helpers boş).
- Takım arkadaşı yalnız KENDİ uid'ini `helpers`'a bir kez ekler; istek 24 saat açık, en fazla `needed` yardımcı.

Firebase Console ▸ Firestore ▸ Rules içinde `match /teams/{teamId} { ... }` bloğunun İÇİNE (members/chat'in yanına) ekle:

```
      // Can isteği: her oyuncunun takımda tek aktif isteği (doküman id = kendi uid).
      match /lifeRequests/{uid} {
        allow read: if request.auth != null;
        allow delete: if request.auth != null && request.auth.uid == uid;   // hesap silme
        // Sahibi: yeni istek (yardımcılar boş, sunucu zamanı).
        allow create, update: if request.auth != null && request.auth.uid == uid
          && request.resource.data.keys().hasOnly(['requesterId','requesterName','needed','helpers','createdAt'])
          && request.resource.data.requesterId == uid
          && request.resource.data.requesterName is string && request.resource.data.requesterName.size() <= 80
          && request.resource.data.needed is int
          && request.resource.data.needed >= 1 && request.resource.data.needed <= 5
          && request.resource.data.helpers is map && request.resource.data.helpers.size() == 0
          && request.resource.data.createdAt == request.time;
        // Takım arkadaşı yardımı: yalnız kendi uid'ini helpers'a bir kez ekler.
        allow update: if request.auth != null && request.auth.uid != uid
          && exists(/databases/$(database)/documents/teams/$(teamId)/members/$(request.auth.uid))
          && request.resource.data.diff(resource.data).affectedKeys().hasOnly(['helpers'])
          && request.resource.data.helpers.diff(resource.data.helpers).addedKeys().hasOnly([request.auth.uid])
          && request.resource.data.helpers.diff(resource.data.helpers).changedKeys().size() == 0
          && request.resource.data.helpers.diff(resource.data.helpers).removedKeys().size() == 0
          && request.resource.data.helpers[request.auth.uid] is string
          && request.resource.data.helpers[request.auth.uid].size() <= 80
          && request.resource.data.helpers.size() <= resource.data.needed
          && request.time < resource.data.createdAt + duration.value(24, 'h');
      }
```

Kural yayınlanmadan istek/yardım yazımları reddedilir (oyun çökmez; `[TeamCloud]` uyarısı loglanır, istek yalnız yerelde kalır).
Sorgu `createdAt > şimdi-24s` tek alan filtresi — ek indeks gerekmez.

## Dikkat: repodaki `firestore.rules` bozuk
`a8dcac6b` commit'inde dosyanın içeriği kuralların yerine bir Instagram erişim anahtarıyla değişmiş ve GitHub'a gitmiş.
Son sağlam sürüm: `git show 2839bf5c:firestore.rules`. Anahtar yenilenmeli (bkz. sohbet notu).
