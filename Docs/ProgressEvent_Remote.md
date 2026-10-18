# Progress Event — sunucudan yönetim (2026-10-14)

Hedef ve ödüller **yeni sürüm yayınlamadan** Firestore'dan değişir. Kod: `Core/Backend/RemoteProgressEvent.cs`
(okuma + önbellek + sezon çözümü), `Core/Events/Progress/ProgressEventService.cs` (sayım, staging, basamak).

## Nereye yazılır

Firebase Console → Firestore → `config` koleksiyonu → **`progressEvent`** dokümanı → alan adı **`json`**, tür
**string** → aşağıdaki JSON'u (tek satırlık hâlini) yapıştır. Kural hazır: `config/*` herkes okur, kimse yazamaz.

- Uygulama açılışta ve uygulamaya her dönüşte (10 dk'da bir) okur, cihaza önbelleğe yazar.
- **Bozuk JSON** yazılırsa uygulama son geçerli sezonu kullanmaya devam eder (log: `[RemoteProgressEvent]`).
- Doküman **yoksa / `json` boşsa**: oyundaki asset (`Resources/Events/ProgressEventScheduler`) yedek olarak çalışır.
- `"enabled": false` → event herkeste kapanır (acil durdurma).
- Level sürerken gelen değişiklik **level bitince** uygulanır (o level'ın kazanımları bozulmaz).

## Nasıl döner (bitmez)

- `anchorUtc`'den itibaren `seasonHours`'luk dilimler; her dilimde `seasons` listesinden sıradaki sezon. Liste bitince
  başa sarar → Console'a hiç dokunulmasa da event sürekli döner. `anchorUtc` gelecekteyse o ana dek event yok.
- Sezon değişince ilerleme sıfırlanır (sezon kimliği + dilim no).
- **Basamak (ladder):** sezonun tüm hedefleri biterse, aynı hedefler `growthPercent` kadar büyüyerek (ödül aynı)
  yeniden açılır; en fazla `maxRounds` tur. Yeni tur bir sonraki level başında açılır (menüdeki bar şovu bozulmasın).
- Hedefler **herkes için aynı** (oyuncuya göre ölçeklenmez — kullanıcı kararı). Hedefler SIRALI: yalnız aktif hedef sayar.
- Yalnız **kazanılan** level'ın kazanımları kalıcıdır (vazgeçilirse silinir).

## Alanlar

| Alan | Anlam |
|---|---|
| `enabled` | false → event kapalı |
| `minLevel` | Bu level'dan önce event görünmez/saymaz |
| `anchorUtc` | İlk sezonun başlangıcı, UTC (`2026-10-12` ya da `2026-10-12T09:00:00Z`) |
| `seasonHours` | Sezon süresi (saat, 1–1440) |
| `ladder.enabled / growthPercent / maxRounds` | Basamak turları |
| `seasons[].id` | Sezon kimliği (değiştirirsen o sezonun ilerlemesi sıfırlanır) |
| `seasons[].nameTr / nameEn` | Event başlığı |
| `goals[].type` | Hedef türü (aşağıda) |
| `goals[].target` | Hedef sayısı (1–100000) |
| `goals[].reward` | Ödül türü (aşağıda) |
| `goals[].amount` | Ödül adedi |
| `goals[].minutes` | Can ve level başı jokerlerde >0 → süreli ödül. Coin, yıldız ve board booster’larında süre yoktur; adet verilir. |
| `goals[].descTr / descEn` | (İsteğe bağlı) bar açıklaması |

**Hedef türleri:** `TileCleared_Gear` (sarı) · `TileCleared_Core` (kırmızı) · `TileCleared_Bolt` (mavi) ·
`TileCleared_Plate` (yeşil) · `TileCleared_Any` · `LineH` · `LineV` · `LineAny` (roket kullan) · `ObstacleCleared`
(engel kır) · `SpecialCreated` (special oluştur) · `LevelWon` (level kazan) · `FirstTryWin` (ilk denemede kazan).

**Ödül türleri:** `Coins` · `Lives` · `Stars` · `Joker_LineH` · `Joker_Line` · `Joker_PulseCore` ·
`Joker_SystemOverride` · `Booster_Hammer` · `Booster_Row` · `Booster_Column` · `Booster_Shuffle`.

Tanınmayan tür adı gelen hedef **atlanır** (eski sürümler yeni türlerle kırılmaz). İkonlar türden çözülür
(oyundaki asset'te o türe atanmış görsel varsa o, yoksa ikon kütüphanesi) → indirme gerekmez.

## Hedef/ödül tutarlılığı düzeltmesi — 2026-10-17

ProgressGoalType içinde yıldız harcama hedefi yoktur; cüzdan harcama olayına da abone olunmaz. Yerel asset’te mavi taş hedefinin ikonu yanlış atanmış, ödülü 10 yıldız ve ödül ikonu da kalpmiş. Kırmızı taş hedefinde çekiç ikonu kullanılıyormuş. Hedef görselleri artık yerel/sunucu kaynağından bağımsız olarak ortak TileIconLibrary’deki gerçek hedef türünden çözülür; eksikse yalnız authored hedef ikonu kullanılabilir, ödül ikonu hedef yerine konmaz.

Yerel yedek etkinlikte 250 mavi taş ödülü **150 altın** oldu. 250 kırmızı taşın çalışmayan **15 dakika çekiç** tanımı **1 çekiç** oldu. Diğer hedef sayıları ve süreleri korunarak mevcut hedef ilerlemesinin anlamı değişmedi; kayıt/claim geçmişi sıfırlanmadı, önceden verilen yıldızlar geri alınmadı.

| Yerel hedef | Ödül |
| --- | --- |
| 150 sarı taş kır | 15 dakika sınırsız can |
| 25 roket kullan | 15 dakika PulseCore |
| 250 mavi taş kır | 150 altın |
| 250 kırmızı taş kır | 1 çekiç |
| 20 roket kullan | 1 PulseCore |

Süreli ödül desteği Lives ve Joker_LineH/Line/PulseCore/SystemOverride ile sınırlıdır. Yanlış süre tanımı gelirse ProgressEvent gerçek adet ödülünü verir; kullanılmayan bir expiry kaydı yazmaz. Bu kontrol hem yerel hem remote hedeflerde geçerlidir. Sunucudaki `config/progressEvent` dokümanı okunup değiştirilmedi; remote açıkken hedef sayıları ve ödül türleri hâlâ o dokümanın kontrolündedir.

Boş hedef açıklaması yerine 12 hedef türünün TR/EN açıklaması kullanılır. PEGoalRow prefabındaki eksik descriptionText bağlantısı eklendi; sayaçtan ayrı satırda görev yazılır. Sunucudan verilen özel açıklama korunur. UI ölçüleri statik kontrol edildi, Unity’de görsel testi yapılmadı.

`Tools/check_progress_event_rules.csx`: 37 üretim metodu kontrolü (ikon, açıklama, süre/adet, sıralı sayım, staging, tek ödül). `check_progress_event_assets.py`: beş yerel hedef/ödül eşleşmesi, TR/EN anahtarları, prefab ID ve açıklama bağlantısı. Yeni per-frame arama, oyun VFX’i veya gameplay allocation eklenmedi. Build/Unity çalıştırılmadı.

## Hedef ayarı (makul seviye)

Kaba ölçü (ölçülmedi, tahmin): kazanılan bir level'da renk başına ~30–40 taş, 1–3 roket, 3–5 special,
level'a göre 10–40 engel. Örnek sezonlar ~25–35 kazanılan level ister (3 günde uğraşan oyuncu bitirir,
hafif oyuncu ilk 2–3 hedefi alır). Telemetri gelince sayıları buna göre çek.

## Örnek (3 sezon, 72 saat)

```json
{
  "enabled": true,
  "minLevel": 8,
  "anchorUtc": "2026-10-12",
  "seasonHours": 72,
  "ladder": {
    "enabled": true,
    "growthPercent": 15,
    "maxRounds": 10
  },
  "seasons": [
    {
      "id": "tamir",
      "nameTr": "Tamir Haftası",
      "nameEn": "Fix-Up Week",
      "goals": [
        {
          "type": "TileCleared_Gear",
          "target": 150,
          "reward": "Coins",
          "amount": 50
        },
        {
          "type": "SpecialCreated",
          "target": 20,
          "reward": "Booster_Hammer",
          "amount": 1
        },
        {
          "type": "TileCleared_Core",
          "target": 250,
          "reward": "Lives",
          "amount": 1,
          "minutes": 15
        },
        {
          "type": "LineAny",
          "target": 25,
          "reward": "Joker_PulseCore",
          "amount": 1
        },
        {
          "type": "ObstacleCleared",
          "target": 120,
          "reward": "Coins",
          "amount": 200
        }
      ]
    },
    {
      "id": "hasat",
      "nameTr": "Hasat Görevleri",
      "nameEn": "Harvest Tasks",
      "goals": [
        {
          "type": "TileCleared_Plate",
          "target": 150,
          "reward": "Coins",
          "amount": 50
        },
        {
          "type": "ObstacleCleared",
          "target": 60,
          "reward": "Booster_Row",
          "amount": 1
        },
        {
          "type": "TileCleared_Bolt",
          "target": 250,
          "reward": "Lives",
          "amount": 1,
          "minutes": 15
        },
        {
          "type": "LevelWon",
          "target": 8,
          "reward": "Joker_Line",
          "amount": 1
        },
        {
          "type": "SpecialCreated",
          "target": 40,
          "reward": "Coins",
          "amount": 200
        }
      ]
    },
    {
      "id": "usta",
      "nameTr": "Usta Yolu",
      "nameEn": "Master Path",
      "goals": [
        {
          "type": "TileCleared_Any",
          "target": 400,
          "reward": "Coins",
          "amount": 50
        },
        {
          "type": "LineAny",
          "target": 15,
          "reward": "Booster_Column",
          "amount": 1
        },
        {
          "type": "FirstTryWin",
          "target": 4,
          "reward": "Lives",
          "amount": 1,
          "minutes": 30
        },
        {
          "type": "SpecialCreated",
          "target": 30,
          "reward": "Joker_SystemOverride",
          "amount": 1
        },
        {
          "type": "LevelWon",
          "target": 10,
          "reward": "Coins",
          "amount": 250
        }
      ]
    }
  ]
}
```

Tek satırlık hâli (Console'a yapıştır):

```
{"enabled":true,"minLevel":8,"anchorUtc":"2026-10-12","seasonHours":72,"ladder":{"enabled":true,"growthPercent":15,"maxRounds":10},"seasons":[{"id":"tamir","nameTr":"Tamir Haftası","nameEn":"Fix-Up Week","goals":[{"type":"TileCleared_Gear","target":150,"reward":"Coins","amount":50},{"type":"SpecialCreated","target":20,"reward":"Booster_Hammer","amount":1},{"type":"TileCleared_Core","target":250,"reward":"Lives","amount":1,"minutes":15},{"type":"LineAny","target":25,"reward":"Joker_PulseCore","amount":1},{"type":"ObstacleCleared","target":120,"reward":"Coins","amount":200}]},{"id":"hasat","nameTr":"Hasat Görevleri","nameEn":"Harvest Tasks","goals":[{"type":"TileCleared_Plate","target":150,"reward":"Coins","amount":50},{"type":"ObstacleCleared","target":60,"reward":"Booster_Row","amount":1},{"type":"TileCleared_Bolt","target":250,"reward":"Lives","amount":1,"minutes":15},{"type":"LevelWon","target":8,"reward":"Joker_Line","amount":1},{"type":"SpecialCreated","target":40,"reward":"Coins","amount":200}]},{"id":"usta","nameTr":"Usta Yolu","nameEn":"Master Path","goals":[{"type":"TileCleared_Any","target":400,"reward":"Coins","amount":50},{"type":"LineAny","target":15,"reward":"Booster_Column","amount":1},{"type":"FirstTryWin","target":4,"reward":"Lives","amount":1,"minutes":30},{"type":"SpecialCreated","target":30,"reward":"Joker_SystemOverride","amount":1},{"type":"LevelWon","target":10,"reward":"Coins","amount":250}]}]}
```
