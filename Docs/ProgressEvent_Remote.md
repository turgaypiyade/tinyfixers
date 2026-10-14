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
| `goals[].minutes` | >0 → süreli ödül (ör. `Lives` + 15 = 15 dk sınırsız can) |
| `goals[].descTr / descEn` | (İsteğe bağlı) bar açıklaması |

**Hedef türleri:** `TileCleared_Gear` (sarı) · `TileCleared_Core` (kırmızı) · `TileCleared_Bolt` (mavi) ·
`TileCleared_Plate` (yeşil) · `TileCleared_Any` · `LineH` · `LineV` · `LineAny` (roket kullan) · `ObstacleCleared`
(engel kır) · `SpecialCreated` (special oluştur) · `LevelWon` (level kazan) · `FirstTryWin` (ilk denemede kazan).

**Ödül türleri:** `Coins` · `Lives` · `Stars` · `Joker_LineH` · `Joker_Line` · `Joker_PulseCore` ·
`Joker_SystemOverride` · `Booster_Hammer` · `Booster_Row` · `Booster_Column` · `Booster_Shuffle`.

Tanınmayan tür adı gelen hedef **atlanır** (eski sürümler yeni türlerle kırılmaz). İkonlar türden çözülür
(oyundaki asset'te o türe atanmış görsel varsa o, yoksa ikon kütüphanesi) → indirme gerekmez.

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
