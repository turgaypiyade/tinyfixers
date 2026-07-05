using UnityEngine;

/// <summary>
/// Liderlik Panosu görsel kiti — TÜM sprite'lar tek asset'te (kullanıcı buradan değiştirir).
/// Boş bırakılan slot, UITheme rengi / rounded fallback ile çizilir; yani sprite'lar
/// gelmeden de ekran çalışır, sprite geldikçe güzelleşir.
///
/// Oluştur: Assets > Create > TinyFixers > Leaderboard Skin
/// (Mockup setup otomatik oluşturup bağlar: Settings/LeaderboardSkin.asset)
/// </summary>
[CreateAssetMenu(menuName = "TinyFixers/Leaderboard Skin", fileName = "LeaderboardSkin")]
public sealed class LeaderboardSkin : ScriptableObject
{
    [Header("Ekran arka planı")]
    [Tooltip("Tam ekran arka plan (üstte title bandı GÖMÜLÜ). Atanınca panelin düz rengi ve " +
             "üretilen başlık bandı gizlenir; yalnız başlık YAZISI bandın üzerinde kalır.")]
    public Sprite screenBackground;

    [Header("Satır arka planları (9-slice önerilir, ~880x130)")]
    [Tooltip("Normal satır (krem/açık).")]
    public Sprite rowBackground;
    [Tooltip("KENDİ satırın (yeşil).")]
    public Sprite selfRowBackground;
    [Tooltip("Haftalık top-3 büyük kart arka planı (opsiyonel; boşsa rowBackground).")]
    public Sprite topThreeCardBackground;

    [Header("Rütbe rozetleri (~90x90)")]
    [Tooltip("1. sıra altın madalya.")]
    public Sprite medalGold;
    [Tooltip("2. sıra gümüş madalya.")]
    public Sprite medalSilver;
    [Tooltip("3. sıra bronz madalya.")]
    public Sprite medalBronze;
    [Tooltip("4+ sıralar için düz rozet/plaka (boşsa yalnız sayı).")]
    public Sprite rankPlate;

    [Header("Avatar & amblem")]
    [Tooltip("Avatar çerçevesi (~110x110, içine avatar oturur).")]
    public Sprite avatarFrame;
    [Tooltip("Takım amblem çerçevesi — hexagon (~120x120).")]
    public Sprite teamEmblemFrame;

    [Header("Sekmeler & toggle (9-slice)")]
    [Tooltip("Seçili sekme (üstten yuvarlak, parlak).")]
    public Sprite tabSelected;
    [Tooltip("Seçili olmayan sekme (koyu).")]
    public Sprite tabUnselected;
    [Tooltip("Seçili alt-toggle hapı (turuncu/altın).")]
    public Sprite togglePillSelected;
    [Tooltip("Seçili olmayan alt-toggle hapı (gri-mor).")]
    public Sprite togglePillUnselected;
    [Tooltip("Sekmelerin kaynaştığı bant yüzeyi (9-slice; üst kenar DÜZ ve sekme dolgusuyla aynı renk).")]
    public Sprite connectedBand;
    [Tooltip("Dikiş yaması: seçili sekme-bant birleşimini örten KÜÇÜK DÜZ dolgu (sekme iç dolgusuyla " +
             "AYNI renk/doku, kontursuz-gölgesiz). Kapalı kapsül sekme sprite'ıyla bile kusursuz kaynaşma sağlar.")]
    public Sprite tabSeamPatch;

    [Header("Çipler & küçük parçalar")]
    [Tooltip("Zaman çipi (sol üst, saat ikonlu hap).")]
    public Sprite timerChip;
    [Tooltip("Kapasite çipi arka planı (takım, '49/50').")]
    public Sprite capacityChip;
    [Tooltip("Puan/kupa çipi arka planı (oyuncular sekmesi skoru).")]
    public Sprite trophyChip;
    [Tooltip("Haftalık 'Yarışma' başlık bandı (bordo).")]
    public Sprite weeklyHeaderBand;

    [Header("Haftalık hediye kutuları (~90x90)")]
    public Sprite giftTier1;
    public Sprite giftTier2;
    public Sprite giftTier3;

    [Header("Satır banner sanatı")]
    [Tooltip("Satırın sağındaki dekoratif art bölgesi için varsayılan (entry.bannerArt yoksa).")]
    public Sprite defaultRowBanner;

    // ─────────────────────────────────────────────────────────────────
    //  YERLEŞİM — mockup setup her üretimde BURADAN okur.
    //  Ayarları sahnedeki objelerde DEĞİL burada yap; yeniden üretim ezmez.
    // ─────────────────────────────────────────────────────────────────

    [Header("Yerleşim — Ekran (px, 1080x1920 referans)")]
    [Tooltip("Başlık yazısının üstten mesafesi (çentik/safe-area altına iter).")]
    public float titleTopOffset = 70f;
    [Tooltip("Zaman çipinin pozisyonu (sol-üstten).")]
    public Vector2 timerChipPos = new Vector2(16f, -6f);
    public Vector2 timerChipSize = new Vector2(150f, 44f);
    [Tooltip("Sekme sırasının üstten başlangıcı (body içinde).")]
    public float tabsTopY = 80f;
    [Tooltip("Sekme yüksekliği.")]
    public float tabsHeight = 96f;
    [Tooltip("Sekme sırasının ekran kenarlarından payı (px). Artır → tüm sekmeler daralır.")]
    public float tabsSideMargin = 12f;
    [Tooltip("İki sekme arasındaki boşluk (px).")]
    public float tabGap = 8f;
    [Tooltip("Bandın üstten başlangıcı. Kaynaşma için tabsTopY + tabsHeight - bandTopY = örtüşme (~12px) olmalı.")]
    public float bandTopY = 164f;
    public float bandHeight = 96f;
    [Tooltip("Bandın ekran kenarlarından DIŞARI taşması (px). Pozitif = kenar bevel'leri ekran dışında kalır, bant tam kaplar.")]
    public float bandSideOverflow = 24f;
    [Tooltip("Sekme yazısının üstten iç payı (yazılar ÜSTE dayalı; bandın altında kalan kısma inmez).")]
    public float tabLabelTopPadding = 12f;
    [Tooltip("Dikiş yamasının genişlik iç payı (px). 0 = tab butonuyla birebir aynı genişlik.")]
    public float seamPatchInset = 0f;
    [Tooltip("Dikiş yamasının yüksekliği (px) — İNCE tutulur, bandın üst çizgisine ortalanır.")]
    public float seamPatchHeight = 18f;
    [Tooltip("Seçili sekmenin yukarı uzama miktarı.")]
    public float selectedTabRaise = 14f;
    [Tooltip("Liste alanının üstten offseti (band bitişi).")]
    public float listTopOffset = 266f;
    [Tooltip("Liste alanının alttan offseti (pinli self satırına yer).")]
    public float listBottomOffset = 130f;
    [Tooltip("Toggle haplarının boyutu ve merkezlerinin ekran ortasından uzaklığı.")]
    public Vector2 togglePillSize = new Vector2(340f, 62f);
    public float togglePillSpread = 190f;

    [Header("Yerleşim — Satır (px)")]
    public float rowHeight = 120f;
    [Tooltip("HAFTALIK sekmesinde top-3 kartlarının yüksekliği (RM'deki büyük kartlar).")]
    public float weeklyTopThreeRowHeight = 200f;
    [Tooltip("Haftalık top-3 kartındaki hediye kutusu boyutu.")]
    public float giftIconSize = 96f;
    public float rankBadgeSize = 72f;
    public float rankBadgeX = 12f;
    public float avatarX = 96f;
    public float avatarSize = 94f;
    [Tooltip("Bölüm/isim/alt-isim bloğunun soldan başlangıcı.")]
    public float infoX = 206f;
    [Tooltip("Puan bloğunun sağdan iç payı.")]
    public float scoreRightPad = 16f;
}
