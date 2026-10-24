using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Su kanalı (ObstacleId.WaterChannel, LevelData.waterChannels) — görünüm, açılma, su akışı, tamamlanma.
/// Tasarım: Docs/WaterChannel_Plan.md.
///
/// Hücreler GridSpawner'da WaterChannel olarak damgalanır (her hücre kendi origin'i, tek vuruş, taş altı katman):
/// üstündeki taş eşleşince ya da special vurunca ObstacleStateService hücreyi temizler → burada hücre AÇILIR ve
/// hafızada tutulur: kapak kuru kanala döner, hücre NORMAL kalır (taşlar üstüne gelir, oynanır; delik değil). Su taşların
/// altındaki katmanda akar. Depo = yolun ilk hücresi (herhangi bir hücre; geçirgen, üstünde çeşme). Su depoya kesintisiz bağlı
/// açık hücrelere sırayla dolar; son hücre de dolunca su o hücrenin tahta kenarından (sol/sağ/alt) çıkar →
/// BoardController.NotifyWaterChannelCompleted (hedef = kanal sayısı).
/// Görseller: Resources/WaterChannelSkin. Hücre başına iki Image (zemin + su); kare başına güncelleme yok, yalnız
/// dolma anında kısa coroutine (kanal başına tek akış).
/// </summary>
public sealed class WaterChannelService
{
    private const float FillSecondsPerCell = 0.16f;   // sabit akış hızı (hücreler arası duraksamasız)
    private const float FoamLength = 0.34f;           // köpük şeridinin akış yönündeki boyu (hücre oranı)
    private const float FoamWidth = 0.86f;            // kanal genişliğini (10-90 = %80) biraz taşar
    private const int SplashDrops = 7;
    private const float SplashSeconds = 0.6f;
    private const float FlowSoundInterval = 0.42f;   // şırıltı ~0.5 sn; su ilerledikçe bu aralıkla yeniden çalar

    private sealed class Cell
    {
        public int x, y;
        public string shape;
        public char entry, exit;    // suyun girdiği / çıktığı kenar (L/R/T/B)
        public bool open, wet;
        public Image ground, water;
    }

    private sealed class Channel
    {
        public Cell[] cells;
        public bool filling, completed;
        public Image foam;          // akış ucu köpüğü (akış yokken gizli)
    }

    private readonly BoardController board;
    private readonly WaterChannelSkin skin;
    private readonly List<Channel> channels = new();
    private readonly Dictionary<int, (Channel channel, int index)> byCell = new();
    private readonly int width;
    private readonly float tileSize;
    private readonly RectTransform groundRoot;
    private readonly List<Image> dropPool = new();
    private float nextFlowSoundTime;
    private int lastOpenSoundFrame = -1;
    // Çıkış efektleri (damla + şelale) tahta kenarından DIŞARI taşar. Kanal kökleri BoardMask (RectMask2D) içinde —
    // çimen kökü "çerçevenin üstüne" kaldırılsa da BoardBorderRoot maskenin çocuğu, yani o da kırpılır. Bu yüzden
    // efektler BoardController.UnclippedFxRoot altında, groundRoot ile aynı çerçeveye hizalanmış kendi kökümüzde.
    private RectTransform exitFxRoot;

    public WaterChannelService(BoardController board, LevelData level, RectTransform groundRoot, RectTransform sourceRoot,
        float tileSize)
    {
        this.board = board;
        skin = WaterChannelSkin.Shared;
        width = level.width;
        this.tileSize = tileSize;
        this.groundRoot = groundRoot;
        if (board == null || level.waterChannels == null || skin == null || groundRoot == null) return;

        foreach (var entry in level.waterChannels)
        {
            var path = entry.pathCellIndices;
            if (!WaterChannelEntry.Validate(level, path, out _)) continue;
            char exitSide = WaterChannelEntry.ExitSide(level, path);

            // Depo (path[0]): geçirgen hücre — taş içinden geçer; altında suyla dolu kanal ağzı, üstünde (taşların
            // üstünde, maske içinde) çeşme başlığı.
            int depot = path[0], dx0 = depot % width, dy0 = depot / width;
            board.MarkRuntimePassThroughHole(dx0, dy0);
            char toFirst = SideToward(depot, path[1]);
            string depotShape = toFirst == 'B' ? "V" : "H";
            CreateImage(groundRoot, $"ChannelDepotGround_{dx0}_{dy0}", dx0, dy0, tileSize, skin.Open(depotShape));
            CreateImage(groundRoot, $"ChannelDepotWater_{dx0}_{dy0}", dx0, dy0, tileSize, skin.Water(depotShape));
            if (skin.source != null && sourceRoot != null)
                CreateImage(sourceRoot, $"ChannelSource_{dx0}_{dy0}", dx0, dy0, tileSize, skin.source).preserveAspect = true;

            var channel = new Channel { cells = new Cell[path.Length - 1] };
            for (int i = 1; i < path.Length; i++)
            {
                char prevSide = SideToward(path[i], path[i - 1]);
                char nextSide = i == path.Length - 1 ? exitSide : SideToward(path[i], path[i + 1]);
                var cell = new Cell
                {
                    x = path[i] % width,
                    y = path[i] / width,
                    shape = ShapeKey(prevSide, nextSide),
                    entry = prevSide,
                    exit = nextSide,
                };
                cell.ground = CreateImage(groundRoot, $"Channel_{cell.x}_{cell.y}", cell.x, cell.y, tileSize, skin.Closed(cell.shape));
                cell.water = CreateImage(groundRoot, $"ChannelWater_{cell.x}_{cell.y}", cell.x, cell.y, tileSize, skin.Water(cell.shape));
                cell.water.gameObject.SetActive(false);
                channel.cells[i - 1] = cell;
                byCell[path[i]] = (channel, i - 1);
            }
            if (skin.foam != null)
            {
                channel.foam = CreateImage(groundRoot, "ChannelFoam", 0, 0, tileSize, skin.foam);
                var frt = channel.foam.rectTransform;
                frt.pivot = new Vector2(0.5f, 0.5f);
                frt.sizeDelta = new Vector2(tileSize * FoamLength, tileSize * FoamWidth);
                channel.foam.gameObject.SetActive(false);
            }
            channels.Add(channel);
        }

        if (channels.Count > 0)
        {
            // Birincil: OnObstacleDestroyed — ObstacleStateService HER yok edilmede tetikler (görsel olayı tetiklemeyen
            // hasar yolları da var). Yedek: görsel olay. Open() aynı hücre için tekrar çağrılırsa bir şey yapmaz.
            board.OnObstacleDestroyed += HandleObstacleDestroyed;
            board.ObstacleVisualChanged += HandleObstacleVisualChanged;
        }
    }

    public void Dispose()
    {
        if (board != null)
        {
            board.OnObstacleDestroyed -= HandleObstacleDestroyed;
            board.ObstacleVisualChanged -= HandleObstacleVisualChanged;
        }
        // Kök maskenin dışında (GridSpawner'ın temizlediği köklerde değil): yeniden kurulumda şelale/damlalar kalmasın.
        if (exitFxRoot != null) Object.Destroy(exitFxRoot.gameObject);
        exitFxRoot = null;
        dropPool.Clear();
    }

    // Maske dışı efekt kökü; her kullanımda groundRoot'un dünya konumu/ölçeğine hizalanır (tahta giriş kayması, ekran
    // ölçeği). Çocuklar groundRoot çocuklarıyla aynı hücre koordinatını kullanır: (x*tileSize, -y*tileSize), sol-üst çapa.
    private RectTransform ExitFxRoot()
    {
        if (groundRoot == null) return null;
        var host = board.UnclippedFxRoot;
        if (host == null) host = groundRoot;
        if (exitFxRoot == null)
        {
            var go = new GameObject("WaterChannelExitFx", typeof(RectTransform));
            exitFxRoot = (RectTransform)go.transform;
            exitFxRoot.SetParent(host, false);
            exitFxRoot.anchorMin = exitFxRoot.anchorMax = new Vector2(0.5f, 0.5f);
        }
        else if (exitFxRoot.parent != host) exitFxRoot.SetParent(host, false);
        exitFxRoot.gameObject.layer = host.gameObject.layer;   // Screen Space Camera: layer 0 çizilmez
        exitFxRoot.SetAsLastSibling();
        exitFxRoot.pivot = groundRoot.pivot;
        exitFxRoot.sizeDelta = groundRoot.rect.size;
        Vector3 gs = groundRoot.lossyScale, hs = host.lossyScale;
        exitFxRoot.localScale = Mathf.Abs(hs.x) > 1e-6f && Mathf.Abs(hs.y) > 1e-6f
            ? new Vector3(gs.x / hs.x, gs.y / hs.y, 1f)
            : Vector3.one;
        exitFxRoot.rotation = groundRoot.rotation;
        exitFxRoot.position = groundRoot.position;
        return exitFxRoot;
    }

    private void HandleObstacleDestroyed(int originIndex, ObstacleId obstacleId)
    {
        if (obstacleId == ObstacleId.WaterChannel) Open(originIndex);
    }

    private void HandleObstacleVisualChanged(ObstacleVisualChange change)
    {
        if (change.obstacleId == ObstacleId.WaterChannel && change.cleared) Open(change.originIndex);
    }

    // Hücre açılır ve hafızada kalır: koyu görünüm açık kanala döner; hücre normal kalır (taşlar üstünde).
    private void Open(int cellIndex)
    {
        if (!byCell.TryGetValue(cellIndex, out var hit)) return;
        var cell = hit.channel.cells[hit.index];
        if (cell.open) return;
        cell.open = true;
        if (cell.ground != null) cell.ground.sprite = skin.Open(cell.shape);
        // Tek vuruş birden çok kanal hücresini açabilir (special): aynı karede tek ses.
        if (lastOpenSoundFrame != Time.frameCount)
        {
            lastOpenSoundFrame = Time.frameCount;
            PlaySfx(skin.openSound, skin.openSoundVolume);
        }

        if (!hit.channel.filling && !hit.channel.completed && board.isActiveAndEnabled)
            board.StartCoroutine(Flow(hit.channel));
    }

    // Su depodan sırayla, sabit hızla ilerler; ucunda dalgalı köpük gezer. Kapalı hücreye gelince durur (o hücre açılınca
    // akış kaldığı yerden sürer). Son hücre de dolunca su kenardan dışarı fışkırır. Akış oyunu durdurmaz ama level-end
    // bekler (GoalOrbFlight işi): son hamlede tamamlanan kanal sayılmadan "hamle bitti" kararı verilmesin.
    private IEnumerator Flow(Channel channel)
    {
        channel.filling = true;
        board.BeginGoalOrbFlight();
        bool firstOfRun = true;
        try
        {
            for (int i = 0; i < channel.cells.Length; i++)
            {
                var cell = channel.cells[i];
                if (cell.wet) continue;
                if (!cell.open)
                {
                    yield return FadeFoam(channel);
                    // Söndürme sırasında açıldıysa: Open() akış sürüyor diye yeni akış başlatmadı → burada devam et.
                    if (!cell.open) yield break;
                }
                if (Time.time >= nextFlowSoundTime)
                {
                    nextFlowSoundTime = Time.time + FlowSoundInterval;
                    PlaySfx(skin.flowSound, skin.flowSoundVolume * Random.Range(0.85f, 1f));
                }
                yield return FillCell(channel, cell, firstOfRun);
                cell.wet = true;
                firstOfRun = false;
            }
            var last = channel.cells[channel.cells.Length - 1];
            PlaySfx(skin.splashSound, skin.splashSoundVolume);
            board.StartCoroutine(Splash(last));
            if (last.exit == 'B') board.StartCoroutine(StartWaterfall(last));
            yield return FadeFoam(channel);
            channel.completed = true;
            board.NotifyWaterChannelCompleted();
        }
        finally
        {
            channel.filling = false;
            board.EndGoalOrbFlight();
        }
    }

    private IEnumerator FillCell(Channel channel, Cell cell, bool easeIn)
    {
        var img = cell.water;
        if (img == null) yield break;
        img.gameObject.SetActive(true);

        bool straight = cell.shape == "H" || cell.shape == "V";
        if (straight)
        {
            img.type = Image.Type.Filled;
            if (cell.shape == "H")
            {
                img.fillMethod = Image.FillMethod.Horizontal;
                img.fillOrigin = cell.entry == 'L' ? (int)Image.OriginHorizontal.Left : (int)Image.OriginHorizontal.Right;
            }
            else
            {
                img.fillMethod = Image.FillMethod.Vertical;
                img.fillOrigin = cell.entry == 'T' ? (int)Image.OriginVertical.Top : (int)Image.OriginVertical.Bottom;
            }
            img.fillAmount = 0f;
        }
        else img.color = new Color(1f, 1f, 1f, 0f);

        var foam = channel.foam;
        if (foam != null && !foam.gameObject.activeSelf)
        {
            foam.gameObject.SetActive(true);
            foam.color = new Color(1f, 1f, 1f, 0f);
        }

        float duration = easeIn ? FillSecondsPerCell * 1.6f : FillSecondsPerCell;
        for (float t = 0f; ; t += Time.deltaTime)
        {
            float k = Mathf.Clamp01(t / duration);
            float p = easeIn ? k * k * (3f - 2f * k) : k;   // ilk hücre yumuşak kalkış, sonra sabit hız
            if (straight) img.fillAmount = p;
            else img.color = new Color(1f, 1f, 1f, Mathf.Clamp01(p * 1.3f));
            if (foam != null) PlaceFoam(foam, cell, p, t);
            if (k >= 1f) break;
            yield return null;
        }
        img.fillAmount = 1f;
        img.color = Color.white;
    }

    // Köpüğü akış ucuna koy: düz parçada giriş kenarından çıkış kenarına doğru, köşede iki kenarın ortak köşesi
    // merkezli çeyrek yay boyunca (yarıçap = hücrenin yarısı = kanalın ortası). Hafif yalpalama canlılık katar.
    private void PlaceFoam(Image foam, Cell cell, float p, float time)
    {
        Vector2 e = SideMid(cell.entry), x = SideMid(cell.exit), pos, dir;
        if (cell.shape == "H" || cell.shape == "V")
        {
            dir = (x - e).normalized;
            pos = Vector2.Lerp(e, x, p) - dir * (FoamLength * 0.25f);
        }
        else
        {
            Vector2 c = new Vector2(cell.entry == 'R' || cell.exit == 'R' ? 1f : 0f, cell.entry == 'B' || cell.exit == 'B' ? 1f : 0f);
            float a0 = Mathf.Atan2(e.y - c.y, e.x - c.x), a1 = Mathf.Atan2(x.y - c.y, x.x - c.x);
            float da = Mathf.DeltaAngle(a0 * Mathf.Rad2Deg, a1 * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            float a = a0 + da * p;
            pos = c + 0.5f * new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            dir = new Vector2(-Mathf.Sin(a), Mathf.Cos(a)) * Mathf.Sign(da);
        }
        var rt = foam.rectTransform;
        rt.anchoredPosition = new Vector2((cell.x + pos.x) * tileSize, -(cell.y + pos.y) * tileSize);
        float wobble = Mathf.Sin(time * 22f) * 4f;
        rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(-dir.y, dir.x) * Mathf.Rad2Deg + wobble);
        float s = 1f + Mathf.Sin(time * 15f) * 0.05f;
        rt.localScale = new Vector3(s, 1f / s, 1f);
        var col = foam.color;
        col.a = Mathf.MoveTowards(col.a, 1f, Time.deltaTime * 6f);
        foam.color = col;
    }

    private static Vector2 SideMid(char side)
        => side == 'L' ? new Vector2(0f, 0.5f) : side == 'R' ? new Vector2(1f, 0.5f)
         : side == 'T' ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 1f);

    private static IEnumerator FadeFoam(Channel channel)
    {
        var foam = channel.foam;
        if (foam == null || !foam.gameObject.activeSelf) yield break;
        for (float t = 0f; t < 0.2f; t += Time.deltaTime)
        {
            var c = foam.color; c.a = 1f - t / 0.2f; foam.color = c;
            yield return null;
        }
        foam.gameObject.SetActive(false);
    }

    // Çıkış: su son hücrenin dış kenarından dışarı fışkırır — damlalar dışa doğru atılır, yerçekimiyle düşüp söner.
    // Damla Image'ları havuzdan (tek seferlik kurulum, sonra yeniden kullanılır).
    private IEnumerator Splash(Cell cell)
    {
        if (skin.droplet == null || ExitFxRoot() == null) yield break;
        Vector2 mid = SideMid(cell.exit);
        Vector2 outDir = cell.exit == 'L' ? Vector2.left : cell.exit == 'R' ? Vector2.right : Vector2.down;   // UI: y yukarı
        Vector2 origin = new Vector2((cell.x + mid.x) * tileSize, -(cell.y + mid.y) * tileSize);

        var drops = new Image[SplashDrops];
        var vel = new Vector2[SplashDrops];
        for (int i = 0; i < SplashDrops; i++)
        {
            var img = TakeDrop();
            drops[i] = img;
            img.rectTransform.anchoredPosition = origin + new Vector2(Random.Range(-0.25f, 0.25f), Random.Range(-0.25f, 0.25f)) * tileSize * (outDir.x == 0 ? new Vector2(1f, 0f) : new Vector2(0f, 1f));
            float spread = Random.Range(-0.6f, 0.6f);
            Vector2 side = new Vector2(-outDir.y, outDir.x);
            vel[i] = (outDir * Random.Range(2.2f, 3.6f) + side * spread + Vector2.up * Random.Range(0.6f, 1.6f)) * tileSize;
            float sz = tileSize * Random.Range(0.14f, 0.22f);
            img.rectTransform.sizeDelta = new Vector2(sz, sz);
        }
        float gravity = tileSize * 14f;
        for (float t = 0f; t < SplashSeconds; t += Time.deltaTime)
        {
            float dt = Time.deltaTime, fade = 1f - t / SplashSeconds;
            for (int i = 0; i < SplashDrops; i++)
            {
                vel[i].y -= gravity * dt;
                var rt = drops[i].rectTransform;
                rt.anchoredPosition += vel[i] * dt;
                rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(vel[i].y, vel[i].x) * Mathf.Rad2Deg + 90f);
                drops[i].color = new Color(1f, 1f, 1f, fade);
            }
            yield return null;
        }
        for (int i = 0; i < SplashDrops; i++) drops[i].gameObject.SetActive(false);
    }

    // Çıkış alt kenardaysa: tahtanın altından level bitene kadar sürekli dökülen şelale. Animasyon tamamen shader'da
    // (_Time) — burada yalnız bir kez kurulup yarım saniyede belirir.
    private IEnumerator StartWaterfall(Cell cell)
    {
        var root = ExitFxRoot();
        if (skin.waterfallMaterial == null || skin.waterfallTexture == null || root == null) yield break;
        var go = new GameObject("ChannelWaterfall", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        go.layer = root.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(root, false);
        rt.SetAsFirstSibling();   // damlalar şelalenin önünde
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        // Tahtanın alt kenarından (son hücrenin dış kenarı) başlayıp çerçevenin üstünden aşağı dökülür; genişlik = kanal
        // (%80). Kök tahtanın önünde: içeri taşarsa taşın üstüne binerdi, o yüzden tam kenardan.
        rt.anchoredPosition = new Vector2((cell.x + 0.5f) * tileSize, -(cell.y + 1f) * tileSize);
        rt.sizeDelta = new Vector2(tileSize * 0.8f, tileSize * 1.5f);
        var raw = go.GetComponent<RawImage>();
        raw.texture = skin.waterfallTexture;
        raw.material = skin.waterfallMaterial;
        raw.raycastTarget = false;
        for (float t = 0f; t < 0.5f; t += Time.deltaTime)
        {
            if (raw == null) yield break;
            raw.color = new Color(1f, 1f, 1f, t / 0.5f);
            yield return null;
        }
        if (raw != null) raw.color = Color.white;
    }

    private Image TakeDrop()
    {
        foreach (var d in dropPool)
            if (!d.gameObject.activeSelf) { d.gameObject.SetActive(true); return d; }
        var img = CreateImage(ExitFxRoot(), "ChannelDrop", 0, 0, tileSize, skin.droplet);
        img.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        dropPool.Add(img);
        return img;
    }

    // a hücresinden b hücresine bakan kenar (y aşağı doğru artar).
    private char SideToward(int a, int b)
    {
        int dx = b % width - a % width, dy = b / width - a / width;
        return dx < 0 ? 'L' : dx > 0 ? 'R' : dy < 0 ? 'T' : 'B';
    }

    private static string ShapeKey(char s1, char s2)
    {
        bool l = s1 == 'L' || s2 == 'L', r = s1 == 'R' || s2 == 'R', t = s1 == 'T' || s2 == 'T', b = s1 == 'B' || s2 == 'B';
        if (l && r) return "H";
        if (t && b) return "V";
        return (l ? "L" : "R") + (t ? "T" : "B");
    }

    private void PlaySfx(AudioClip clip, float volume)
    {
        if (board == null) return;
        var audio = board.Audio;   // Unity nesnesi: ?. kullanma (sahte-null)
        if (audio != null) audio.PlayOneShotClip(clip, volume);
    }

    private static Image CreateImage(RectTransform parent, string name, int x, int y, float tileSize, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = parent.gameObject.layer;   // Screen Space Camera culling
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x * tileSize, -y * tileSize);
        rt.sizeDelta = new Vector2(tileSize, tileSize);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        return img;
    }
}
