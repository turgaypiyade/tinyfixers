#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Sprites;
using Object = UnityEngine.Object;

/// <summary>Image, task mapping, localization and slot budgets in one Editor-only workspace.</summary>
public sealed class WonderMissionDesignerWindow : EditorWindow
{
    [SerializeField] WonderDefinition source, draft;
    [SerializeField] WonderCatalog catalog, budgetDraft;
    [SerializeField] string englishTitle = "";
    [SerializeField] string[] englishTasks = Array.Empty<string>();
    [SerializeField] int selected, selectedRect = -1, completed, slot;
    [SerializeField] bool showRegions = true, drawRegion, original;
    [SerializeField] Vector2 sidebarScroll;
    Material previewMaterial;
    WonderTaskRegions regions;
    float[] coverage;
    int[][] costs;
    List<string> errors;
    bool cacheDirty = true, dragging, creating, resizing, playing;
    Vector2 dragStart;
    Rect dragRect;
    int mouseControl, dragUndoGroup;
    double playbackStart, lastRepaint;
    float effectProgress;
    string status = "";
    const string Folder = "Assets/_Project/Settings/Wonders";
    static readonly string[] Actions = { "Onar / kaynak", "Parlat / aç", "Boya", "Işıklandır", "Temizle", "Açık yüzeyde parlama" };

    [MenuItem("TinyFixers/Wonders/Mission Designer")]
    static void Open()
    {
        var window = GetWindow<WonderMissionDesignerWindow>("Mission Designer");
        window.minSize = new Vector2(980, 700);
        window.Show();
    }

    void OnEnable()
    {
        saveChangesMessage = "Mission Designer taslağını ve TR/EN metinlerini kaydetmek ister misiniz?";
        Undo.undoRedoPerformed += OnUndoRedo;
        EditorApplication.update += Tick;
        if (catalog == null)
        {
            var guid = AssetDatabase.FindAssets("t:WonderCatalog").FirstOrDefault();
            if (guid != null) catalog = AssetDatabase.LoadAssetAtPath<WonderCatalog>(AssetDatabase.GUIDToAssetPath(guid));
        }
        if (budgetDraft == null && catalog != null) CloneBudget();
        if (draft == null && Selection.activeObject is WonderDefinition definition) Load(definition);
        cacheDirty = true;
    }

    void OnDisable()
    {
        Undo.undoRedoPerformed -= OnUndoRedo;
        EditorApplication.update -= Tick;
        Stop();
        if (previewMaterial != null) DestroyImmediate(previewMaterial);
        previewMaterial = null;
    }

    void OnDestroy()
    {
        if (draft != null) DestroyImmediate(draft);
        if (budgetDraft != null) DestroyImmediate(budgetDraft);
    }

    void OnUndoRedo()
    {
        if (budgetDraft == null && catalog != null) CloneBudget();
        cacheDirty = true; hasUnsavedChanges = draft != null; Stop(); Repaint();
    }
    void OnLostFocus()
    {
        if (!dragging || draft == null) return;
        var task = draft.tasks[selected];
        Record("Resim bölgesini düzenle");
        if (creating && selectedRect >= 0 && selectedRect < (task.regions?.Length ?? 0)
            && (task.regions[selectedRect].width < .003f || task.regions[selectedRect].height < .003f))
        { task.regions = task.regions.Where((_, i) => i != selectedRect).ToArray(); selectedRect = -1; }
        dragging = false;
        if (GUIUtility.hotControl == mouseControl) GUIUtility.hotControl = 0;
        FinishDragUndo();
        Changed();
    }

    void BeginDragUndo(string label)
    {
        Undo.IncrementCurrentGroup();
        dragUndoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(label);
        Record(label);
    }
    void FinishDragUndo()
    {
        Undo.FlushUndoRecordObjects();
        Undo.CollapseUndoOperations(dragUndoGroup);
        Undo.IncrementCurrentGroup();
    }
    void Tick()
    {
        if (!playing) return;
        double now = EditorApplication.timeSinceStartup;
        if (now - lastRepaint < 1.0 / 30.0) return;
        lastRepaint = now;
        effectProgress = Mathf.Clamp01((float)(now - playbackStart) / 3f);
        if (effectProgress >= 1f) { playing = false; completed = selected + 1; }
        Repaint();
    }

    void Stop() { playing = false; effectProgress = 0; }
    void Record(string label)
    {
        Undo.RecordObjects(new Object[] { this, draft, budgetDraft }.Where(o => o != null).ToArray(), label);
    }
    void Changed()
    {
        hasUnsavedChanges = true;
        cacheDirty = true;
        status = "";
        Repaint();
    }
    void CloneBudget()
    {
        if (budgetDraft != null) DestroyImmediate(budgetDraft);
        budgetDraft = catalog == null ? null : Instantiate(catalog);
        if (budgetDraft != null) budgetDraft.hideFlags = HideFlags.HideAndDontSave;
    }

    bool CanLeave()
    {
        if (!hasUnsavedChanges) return true;
        int answer = EditorUtility.DisplayDialogComplex("Kaydedilmemiş taslak", "Mevcut taslakta değişiklikler var.", "Kaydet", "Vazgeç", "Değişiklikleri bırak");
        return answer == 2 || (answer == 0 && Save(false));
    }

    void Load(WonderDefinition definition)
    {
        // Parse before discarding the current draft, so a broken localization file cannot lose work.
        JObject json;
        try { json = JObject.Parse(File.ReadAllText(WonderMissionAuthoring.LocalizationPath)); }
        catch (Exception ex) { status = ex.Message; return; }
        if (draft != null) DestroyImmediate(draft);
        source = definition;
        draft = definition != null ? Instantiate(definition) : CreateInstance<WonderDefinition>();
        draft.hideFlags = HideFlags.HideAndDontSave;
        if (definition == null)
        {
            draft.wonderId = "new_wonder";
            draft.displayName = "Yeni Harika";
            draft.tasks = new[] { new WonderTask { name = "Zemini Hazırla", regions = Array.Empty<Rect>() } };
            draft.totalStages = 1;
        }
        if (draft.tasks == null || draft.tasks.Length == 0)
            draft.tasks = Enumerable.Range(0, Mathf.Max(1, draft.totalStages))
                .Select(i => new WonderTask { name = "Görev " + (i + 1), starCost = Mathf.Max(1, draft.GetStarCost(i)), regions = Array.Empty<Rect>() }).ToArray();
        englishTasks = new string[draft.tasks.Length];
        for (int i = 0; i < draft.tasks.Length; i++)
        {
            if (draft.tasks[i] == null) draft.tasks[i] = new WonderTask();
            draft.tasks[i].name = WonderMissionAuthoring.ReadText(json, "tr", $"wonder_{draft.wonderId}_task_{i + 1}", draft.tasks[i].name);
            englishTasks[i] = WonderMissionAuthoring.ReadText(json, "en", $"wonder_{draft.wonderId}_task_{i + 1}", "");
        }
        draft.displayName = WonderMissionAuthoring.ReadText(json, "tr", $"wonder_{draft.wonderId}_name", draft.displayName);
        englishTitle = WonderMissionAuthoring.ReadText(json, "en", $"wonder_{draft.wonderId}_name", "");
        CloneBudget();
        selected = completed = 0;
        selectedRect = -1;
        Stop();
        cacheDirty = true;
        hasUnsavedChanges = definition == null;
        status = "";
    }

    void RefreshCache()
    {
        if (!cacheDirty || draft == null) return;
        selected = Mathf.Clamp(selected, 0, draft.tasks.Length - 1);
        completed = Mathf.Clamp(completed, 0, draft.tasks.Length);
        slot = Mathf.Clamp(slot, 0, Mathf.Max(0, budgetDraft != null ? budgetDraft.Count - 1 : 0));
        // Material arrays and geometric sampling are rebuilt on edits, never on each repaint.
        int physicalCount = draft.tasks.Where(t => t.action != WonderTaskAction.Shine).Sum(t => t.regions?.Length ?? 0);
        regions = physicalCount <= WonderTaskRegions.MaxRects ? WonderTaskRegions.From(draft) : null;
        coverage = WonderMissionAuthoring.Coverage(draft);
        errors = WonderMissionAuthoring.Errors(draft, englishTitle, englishTasks, budgetDraft);
        costs = budgetDraft == null ? Array.Empty<int[]>() : Enumerable.Range(0, Mathf.Max(1, budgetDraft.Count))
            .Select(i => WonderMissionAuthoring.Costs(draft, budgetDraft.TotalStarsFor(i))).ToArray();
        cacheDirty = false;
    }

    void OnGUI()
    {
        DrawToolbar();
        if (draft == null)
        {
            EditorGUILayout.HelpBox("Mevcut bir WonderDefinition seçin veya Yeni Harika ile başlayın. Resmi sprite olarak atayıp görev bölgelerini doğrudan resimde çizebilirsiniz.", MessageType.Info);
            return;
        }
        RefreshCache();
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(390)))
            {
                sidebarScroll = EditorGUILayout.BeginScrollView(sidebarScroll);
                DrawDetails();
                EditorGUILayout.EndScrollView();
            }
            using (new EditorGUILayout.VerticalScope()) DrawCanvas();
        }
        if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, MessageType.Info);
    }

    void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            var next = (WonderDefinition)EditorGUILayout.ObjectField(source, typeof(WonderDefinition), false, GUILayout.Width(245));
            if (next != source && CanLeave()) Load(next);
            if (GUILayout.Button("Seçileni Aç", EditorStyles.toolbarButton) && Selection.activeObject is WonderDefinition w && CanLeave()) Load(w);
            if (GUILayout.Button("Yeni Harika", EditorStyles.toolbarButton) && CanLeave()) Load(null);
            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(draft == null))
            {
                if (GUILayout.Button("Kaydet", EditorStyles.toolbarButton)) Save(false);
                if (GUILayout.Button("Farklı Kaydet", EditorStyles.toolbarButton)) Save(true);
                if (GUILayout.Button("Yeniden Yükle", EditorStyles.toolbarButton) && CanLeave()) Load(source);
            }
        }
    }

    void DrawDetails()
    {
        EditorGUILayout.LabelField("Harika ve resim", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        // Existing progress/localization keys use this ID; duplicate with Save As to create another wonder.
        string id;
        using (new EditorGUI.DisabledScope(source != null)) id = EditorGUILayout.TextField("Kimlik", draft.wonderId);
        string tr = EditorGUILayout.TextField("Ad (TR)", draft.displayName);
        string en = EditorGUILayout.TextField("Ad (EN)", englishTitle);
        var sprite = (Sprite)EditorGUILayout.ObjectField("Resim", draft.backgroundSprite, typeof(Sprite), false);
        var icon = (Sprite)EditorGUILayout.ObjectField("Varsayılan ikon", draft.taskIcon, typeof(Sprite), false);
        if (EditorGUI.EndChangeCheck())
        {
            Record("Harika bilgisi");
            draft.wonderId = id; draft.displayName = tr; englishTitle = en; draft.backgroundSprite = sprite; draft.taskIcon = icon;
            Changed();
        }
        if (GUILayout.Button("Dışarıdan resim içe aktar…")) ImportImage();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Görev sırası", EditorStyles.boldLabel);
        for (int i = 0; i < draft.tasks.Length; i++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var old = GUI.backgroundColor;
                if (i == selected) GUI.backgroundColor = TaskColor(i);
                if (GUILayout.Button($"{i + 1}. {draft.tasks[i].name}", GUILayout.Height(24)))
                { selected = i; selectedRect = -1; completed = i; Stop(); Repaint(); }
                GUI.backgroundColor = old;
                if (costs.Length > 0) GUILayout.Label(costs[slot][i] + " ★", GUILayout.Width(40));
            }
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("+ Görev")) AddTask();
            using (new EditorGUI.DisabledScope(selected <= 0))
                if (GUILayout.Button("↑")) MoveTask(-1);
            using (new EditorGUI.DisabledScope(selected >= draft.tasks.Length - 1))
                if (GUILayout.Button("↓")) MoveTask(1);
            using (new EditorGUI.DisabledScope(draft.tasks.Length <= 1))
                if (GUILayout.Button("Görevi sil")) RemoveTask();
        }
        EditorGUILayout.LabelField("Seçili görev", EditorStyles.boldLabel);
        var task = draft.tasks[selected];
        EditorGUI.BeginChangeCheck();
        string taskTr = EditorGUILayout.TextField("Görev (TR)", task.name);
        string taskEn = EditorGUILayout.TextField("Görev (EN)", englishTasks[selected]);
        var taskIcon = (Sprite)EditorGUILayout.ObjectField("İkon", task.icon, typeof(Sprite), false);
        var action = (WonderTaskAction)EditorGUILayout.Popup("Açılış efekti", (int)task.action, Actions);
        int weight = EditorGUILayout.IntField("Maliyet ağırlığı", draft.GetStarCost(selected));
        if (EditorGUI.EndChangeCheck())
        {
            Record("Görevi düzenle");
            task.name = taskTr; englishTasks[selected] = taskEn; task.icon = taskIcon; task.action = action; task.starCost = Mathf.Max(1, weight);
            Changed();
        }
        EditorGUILayout.HelpBox("Ağırlık, sıranın toplam yıldızı içindeki paydır. Görevler ucuzdan pahalıya sıralanmak zorunda değil. Parlama, daha önce açılan yüzeyi cilalar; yeni alan açmaz.", MessageType.None);
        var rects = task.regions ?? Array.Empty<Rect>();
        for (int i = 0; i < rects.Length; i++)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Toggle(selectedRect == i, (i + 1).ToString(), "Button", GUILayout.Width(26))) selectedRect = i;
                EditorGUI.BeginChangeCheck();
                var r = EditorGUILayout.RectField(rects[i]);
                if (EditorGUI.EndChangeCheck()) { Record("Bölge koordinatları"); rects[i] = r; Changed(); }
                if (GUILayout.Button("×", GUILayout.Width(24)))
                {
                    Record("Bölgeyi sil"); task.regions = rects.Where((_, index) => index != i).ToArray(); selectedRect = -1; Changed(); break;
                }
            }
        }
        EditorGUILayout.LabelField("Koordinatlar: sol-alt (0,0), sağ-üst (1,1).", EditorStyles.miniLabel);
        if (coverage != null && selected < coverage.Length)
        {
            EditorGUILayout.LabelField($"Hedef alan ≈ %{coverage[selected] * 100f:F1} · Çevre ≈ %{coverage[draft.TaskCount] * 100f:F1}", EditorStyles.miniLabel);
            if (task.action != WonderTaskAction.Shine && coverage[selected] < .001f)
                EditorGUILayout.HelpBox("Bu görev yeni fiziksel alan açmıyor. Bölgesiz Işık görevi çevreyi aydınlatabilir; onarım için hedef bölge çizin.", MessageType.Warning);
            if (coverage[selected] > .5f)
                EditorGUILayout.HelpBox("Bu adım resmin yarısından fazlasını tek seferde açıyor. Görev tanımına ve önceki aşamaya bakın.", MessageType.Warning);
        }
        DrawBudgets();
        if (errors.Count > 0) EditorGUILayout.HelpBox(string.Join("\n", errors), MessageType.Error);
        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Kaydet: harika verisi + TR/EN metinleri + değiştirilen katalog toplamları. Karakterler, ödüller ve kaynakçı ayarları korunur. Kod yalnız Editor içinde çalışır.", MessageType.None);
    }

    void DrawBudgets()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Oyuncu sırasına göre yıldızlar", EditorStyles.boldLabel);
        var next = (WonderCatalog)EditorGUILayout.ObjectField("Katalog", catalog, typeof(WonderCatalog), false);
        if (next != catalog)
        {
            catalog = next; CloneBudget(); Changed();
        }
        if (budgetDraft == null) return;
        EditorGUI.BeginChangeCheck();
        int first = EditorGUILayout.IntField("İlk harika toplamı", budgetDraft.firstWonderTotalStars);
        int second = EditorGUILayout.IntField("İkinci harika toplamı", budgetDraft.secondWonderTotalStars);
        int last = EditorGUILayout.IntField("Son harika toplamı", budgetDraft.lastWonderTotalStars);
        if (EditorGUI.EndChangeCheck())
        {
            Record("Yıldız toplamları");
            budgetDraft.firstWonderTotalStars = first; budgetDraft.secondWonderTotalStars = second; budgetDraft.lastWonderTotalStars = last;
            Changed();
        }
        slot = EditorGUILayout.IntSlider("Önizlenen sıra", slot + 1, 1, Mathf.Max(1, budgetDraft.Count)) - 1;
        for (int i = 0; i < costs.Length; i++)
            EditorGUILayout.LabelField($"{i + 1}. sıra: {string.Join(" + ", costs[i])} = {costs[i].Sum()} ★", EditorStyles.miniLabel);
        EditorGUILayout.LabelField("Toplamlar tüm harikaları etkiler; ülkeye göre sıra değişebilir.", EditorStyles.wordWrappedMiniLabel);
        if (source != null && !(catalog.wonders ?? Array.Empty<WonderDefinition>()).Contains(source))
        {
            EditorGUILayout.HelpBox("Bu harika katalogda değil. Eklemek katalogdaki harika sayısını ve ara sıraların toplamlarını değiştirir.", MessageType.Info);
            if (GUILayout.Button("Kaydet ve bu harikayı kataloğa ekle")) AddToCatalog();
        }
    }

    void AddToCatalog()
    {
        var future = Instantiate(budgetDraft);
        try
        {
            future.wonders = (future.wonders ?? Array.Empty<WonderDefinition>()).Concat(new[] { draft }).ToArray();
            var futureErrors = WonderMissionAuthoring.Errors(draft, englishTitle, englishTasks, future);
            if (futureErrors.Count > 0) { status = "Kataloğa eklenmedi: " + string.Join(" · ", futureErrors); return; }
        }
        finally { DestroyImmediate(future); }
        if (!Save(false)) return;
        Undo.RecordObject(catalog, "Harikayı kataloğa ekle");
        catalog.wonders = (catalog.wonders ?? Array.Empty<WonderDefinition>()).Concat(new[] { source }).ToArray();
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssetIfDirty(catalog);
        CloneBudget(); cacheDirty = true;
        status = "Harika kataloğa eklendi. Sıralara göre yeni toplamlar solda gösteriliyor.";
    }

    void AddTask()
    {
        Record("Görev ekle");
        // Materialize existing fallback weights before changing stage order/count.
        MaterializeWeights();
        var list = draft.tasks.ToList(); list.Add(new WonderTask { name = "Yeni Görev", regions = Array.Empty<Rect>() }); draft.tasks = list.ToArray();
        englishTasks = englishTasks.Concat(new[] { "" }).ToArray();
        selected = draft.tasks.Length - 1; completed = selected; selectedRect = -1;
        Stop(); Changed();
    }
    void MaterializeWeights()
    {
        for (int i = 0; i < draft.tasks.Length; i++) draft.tasks[i].starCost = Mathf.Max(1, draft.GetStarCost(i));
    }
    void MoveTask(int direction)
    {
        Record("Görev sırası"); MaterializeWeights();
        int next = selected + direction;
        var task = draft.tasks[selected]; draft.tasks[selected] = draft.tasks[next]; draft.tasks[next] = task;
        string en = englishTasks[selected]; englishTasks[selected] = englishTasks[next]; englishTasks[next] = en;
        selected = next; selectedRect = -1; completed = next; Stop(); Changed();
    }
    void RemoveTask()
    {
        Record("Görev sil"); MaterializeWeights();
        draft.tasks = draft.tasks.Where((_, i) => i != selected).ToArray();
        englishTasks = englishTasks.Where((_, i) => i != selected).ToArray();
        selected = Mathf.Min(selected, draft.tasks.Length - 1); selectedRect = -1; completed = selected;
        Stop(); Changed();
    }

    void DrawCanvas()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            drawRegion = GUILayout.Toggle(drawRegion, "Bölge çiz", EditorStyles.toolbarButton);
            showRegions = GUILayout.Toggle(showRegions, "Sınırlar", EditorStyles.toolbarButton);
            original = GUILayout.Toggle(original, "Orijinal", EditorStyles.toolbarButton);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(playing ? "Durdur" : "Görev efektini izle", EditorStyles.toolbarButton))
            {
                if (playing) Stop();
                else { original = false; completed = selected; effectProgress = 0; playbackStart = EditorApplication.timeSinceStartup; playing = true; }
            }
        }
        EditorGUI.BeginChangeCheck();
        int step = EditorGUILayout.IntSlider("Tamamlanan adım", completed, 0, draft.tasks.Length);
        if (EditorGUI.EndChangeCheck()) { completed = step; Stop(); }
        EditorGUILayout.LabelField("Çiz: sürükle. Seç/taşı: bölgeye tıkla. Boyutlandır: sağ-alt tutamaç. Sil: Delete. Geri al: Ctrl/Cmd+Z.", EditorStyles.wordWrappedMiniLabel);
        Rect viewport = GUILayoutUtility.GetRect(100, 100, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        EditorGUI.DrawRect(viewport, new Color(.10f, .11f, .13f));
        var sprite = draft.backgroundSprite;
        if (sprite == null) { GUI.Label(viewport, "Soldan bir resim sprite'ı seçin.", EditorStyles.centeredGreyMiniLabel); return; }
        float scale = Mathf.Min((viewport.width - 28) / sprite.rect.width, (viewport.height - 28) / sprite.rect.height);
        Vector2 size = sprite.rect.size * Mathf.Max(.01f, scale);
        Rect image = new Rect(viewport.center - size * .5f, size);
        if (Event.current.type == EventType.Repaint) DrawPhoto(image, sprite);
        if (showRegions) DrawRegions(image);
        HandleCanvas(image);
        EditorGUILayout.LabelField($"{completed}/{draft.TaskCount} tamamlandı · çevre rengi %{100f * completed / draft.TaskCount:F0}", EditorStyles.miniLabel);
        EditorGUILayout.LabelField("Önizleme oyunun restorasyon shader'ını kullanır. Kaynakçı, düşen tabaka ve karakter animasyonları sahnede izlenir.", EditorStyles.wordWrappedMiniLabel);
    }

    void DrawPhoto(Rect image, Sprite sprite)
    {
        Vector4 uv = DataUtility.GetOuterUV(sprite);
        Rect sourceUV = Rect.MinMaxRect(uv.x, uv.y, uv.z, uv.w);
        if (original) { GUI.DrawTextureWithTexCoords(image, sprite.texture, sourceUV, true); return; }
        if (draft.HasTaskRegions && regions == null)
        { GUI.Label(image, "Bölge sınırı aşıldı; önizleme için bölge sayısını azaltın."); return; }
        if (previewMaterial == null)
        {
            var shader = Shader.Find("UI/WonderReveal");
            if (shader == null) { GUI.Label(image, "UI/WonderReveal shader bulunamadı."); return; }
            previewMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }
        var m = previewMaterial;
        float reveal = (float)completed / draft.TaskCount;
        m.SetFloat("_Reveal", reveal); m.SetFloat("_ContextReveal", reveal);
        m.SetFloat("_Restoration", 1); m.SetFloat("_TaskRegions", regions == null ? 0 : 1);
        m.SetFloat("_RegionCount", draft.TaskCount);
        m.SetFloat("_RegionStride", WonderRestorationLayout.Stride(draft.TaskCount));
        m.SetVector("_SpriteUVRect", uv);
        m.SetVector("_RestorationRect", new Vector4(0, 0, image.width, image.height));
        m.SetFloat("_RhodesTorch", sprite.name == "RodosDevi" ? 1 : 0);
        m.SetVector("_TaskAnim", Vector4.zero); m.SetVector("_RegionCut", Vector4.zero);
        m.SetVector("_RegionFinish", Vector4.zero); m.SetVector("_Torch", Vector4.zero);
        regions?.ApplyTo(m);
        if (playing && regions != null)
        {
            float p = effectProgress;
            var action = draft.tasks[selected].action;
            m.SetFloat("_ContextReveal", (selected + p) / draft.TaskCount);
            m.SetVector("_TaskBounds", regions.BoundsVector(selected));
            if (action == WonderTaskAction.Repair)
            {
                float trace = Mathf.Clamp01(p / .65f);
                Vector2 torch = regions.TracePoint(selected, trace, out int rect, out float local);
                m.SetVector("_RegionCut", new Vector4(selected, local, 1, rect + 1));
                m.SetVector("_Torch", new Vector4(torch.x, torch.y, p < .65f ? 1 : 0, 0));
                if (p >= .65f) m.SetVector("_RegionFinish", new Vector4(selected, (p - .65f) / .35f, 1, 0));
            }
            else m.SetVector("_TaskAnim", new Vector4(selected, p, (float)action,
                !regions.HasOwnedSurface(selected) && action != WonderTaskAction.Shine ? 2 : 1));
        }
        Graphics.DrawTexture(image, sprite.texture, sourceUV, 0, 0, 0, 0, Color.white, m);
    }

    static Color TaskColor(int i) => Color.HSVToRGB((i * .137f) % 1f, .65f, 1f);
    static Rect ScreenRect(Rect image, Rect uv) => new Rect(image.x + uv.x * image.width, image.y + (1 - uv.yMax) * image.height, uv.width * image.width, uv.height * image.height);
    static Vector2 UVPoint(Rect image, Vector2 point) => new Vector2(Mathf.Clamp01((point.x - image.x) / image.width), Mathf.Clamp01(1 - (point.y - image.y) / image.height));

    void DrawRegions(Rect image)
    {
        for (int t = 0; t < draft.tasks.Length; t++)
        {
            var list = draft.tasks[t].regions;
            if (list == null) continue;
            for (int r = 0; r < list.Length; r++)
            {
                Rect box = ScreenRect(image, list[r]);
                if (float.IsNaN(box.x + box.y + box.width + box.height) || float.IsInfinity(box.x + box.y + box.width + box.height)
                    || box.width <= 0 || box.height <= 0) continue;
                Color c = TaskColor(t);
                Handles.BeginGUI();
                Handles.DrawSolidRectangleWithOutline(box, new Color(c.r, c.g, c.b, t == selected ? .09f : .025f), c);
                Handles.EndGUI();
                GUI.Label(new Rect(box.x + 3, box.y + 2, 62, 20), $"{t + 1}.{r + 1}", EditorStyles.whiteMiniLabel);
                if (t == selected && r == selectedRect) EditorGUI.DrawRect(new Rect(box.xMax - 7, box.yMax - 7, 9, 9), Color.white);
            }
        }
    }

    void HandleCanvas(Rect image)
    {
        Event e = Event.current;
        mouseControl = GUIUtility.GetControlID("WonderMissionCanvas".GetHashCode(), FocusType.Passive);
        var task = draft.tasks[selected];
        if (e.type == EventType.MouseDown && e.button == 0 && image.Contains(e.mousePosition))
        {
            GUI.FocusControl(null);
            Stop();
            if (drawRegion)
            {
                BeginDragUndo("Resimde bölge çiz");
                task.regions = (task.regions ?? Array.Empty<Rect>()).Concat(new[] { new Rect() }).ToArray();
                selectedRect = task.regions.Length - 1;
                creating = true; resizing = false;
                dragStart = UVPoint(image, e.mousePosition);
            }
            else
            {
                bool found = false;
                if (showRegions && task.regions != null && selectedRect >= 0 && selectedRect < task.regions.Length)
                {
                    Rect box = ScreenRect(image, task.regions[selectedRect]);
                    found = new Rect(box.xMax - 10, box.yMax - 10, 16, 16).Contains(e.mousePosition);
                    resizing = found;
                }
                if (!found)
                {
                    // Same smallest-region-wins rule as the runtime, including Shine editing overlays.
                    float area = float.MaxValue;
                    Vector2 p = UVPoint(image, e.mousePosition);
                    for (int t = 0; t < draft.tasks.Length; t++)
                        for (int r = 0; r < (draft.tasks[t].regions?.Length ?? 0); r++)
                        {
                            Rect box = draft.tasks[t].regions[r];
                            if (!box.Contains(p) || box.width * box.height >= area) continue;
                            area = box.width * box.height; selected = t; selectedRect = r; found = true;
                        }
                    resizing = false;
                }
                if (!found) return;
                task = draft.tasks[selected];
                BeginDragUndo("Resim bölgesini taşı / boyutlandır");
                creating = false;
                dragRect = task.regions[selectedRect];
                dragStart = UVPoint(image, e.mousePosition);
            }
            showRegions = true;
            dragging = true;
            GUIUtility.hotControl = mouseControl;
            e.Use();
        }
        if (e.type == EventType.MouseDrag && dragging && GUIUtility.hotControl == mouseControl)
        {
            Record("Resim bölgesini düzenle");
            Vector2 p = UVPoint(image, e.mousePosition);
            Rect next;
            if (creating) next = Rect.MinMaxRect(Mathf.Min(p.x, dragStart.x), Mathf.Min(p.y, dragStart.y), Mathf.Max(p.x, dragStart.x), Mathf.Max(p.y, dragStart.y));
            else if (resizing) next = Rect.MinMaxRect(dragRect.xMin, Mathf.Min(p.y, dragRect.yMax - .001f), Mathf.Max(p.x, dragRect.xMin + .001f), dragRect.yMax);
            else next = new Rect(Mathf.Clamp(dragRect.x + p.x - dragStart.x, 0, 1 - dragRect.width), Mathf.Clamp(dragRect.y + p.y - dragStart.y, 0, 1 - dragRect.height), dragRect.width, dragRect.height);
            task.regions[selectedRect] = next;
            Changed(); e.Use();
        }
        if (e.type == EventType.MouseUp && dragging && GUIUtility.hotControl == mouseControl)
        {
            Record("Resim bölgesini düzenle");
            if (creating && (task.regions[selectedRect].width < .003f || task.regions[selectedRect].height < .003f))
            { task.regions = task.regions.Where((_, i) => i != selectedRect).ToArray(); selectedRect = -1; }
            dragging = false; GUIUtility.hotControl = 0; FinishDragUndo(); Changed(); e.Use();
        }
        if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace)
            && !EditorGUIUtility.editingTextField && selectedRect >= 0 && selectedRect < (task.regions?.Length ?? 0))
        {
            Record("Bölgeyi sil"); task.regions = task.regions.Where((_, i) => i != selectedRect).ToArray(); selectedRect = -1; Changed(); e.Use();
        }
    }

    void ImportImage()
    {
        string input = EditorUtility.OpenFilePanel("Harika resmi", "", "png,jpg,jpeg");
        if (string.IsNullOrEmpty(input)) return;
        string target = EditorUtility.SaveFilePanelInProject("Resmi projeye kopyala", Path.GetFileName(input), Path.GetExtension(input).TrimStart('.'), "Yeni bir resim yolu seçin.", "Assets/_Project/Art/UI/Missions");
        if (string.IsNullOrEmpty(target)) return;
        try
        {
            if (File.Exists(target)) throw new IOException("Mevcut resmi değiştirmemek için yeni bir dosya adı seçin.");
            File.Copy(input, target, false);
            AssetDatabase.ImportAsset(target);
            var importer = (TextureImporter)AssetImporter.GetAtPath(target);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            Record("Harika resmi ata"); draft.backgroundSprite = AssetDatabase.LoadAssetAtPath<Sprite>(target); Changed();
        }
        catch (Exception ex) { status = "Resim alınamadı: " + ex.Message; }
    }

    public override void SaveChanges() { if (Save(false)) base.SaveChanges(); }
    public override void DiscardChanges() { base.DiscardChanges(); }

    bool Save(bool saveAs)
    {
        Stop(); cacheDirty = true; RefreshCache();
        if (draft == null) return false;
        if (errors.Count > 0) { status = "Kaydedilmedi: " + string.Join(" · ", errors); return false; }
        string path = source == null || saveAs
            ? EditorUtility.SaveFilePanelInProject("Harika taslağını kaydet", "Wonder_" + draft.wonderId, "asset", "WonderDefinition dosyası", Folder)
            : AssetDatabase.GetAssetPath(source);
        if (string.IsNullOrEmpty(path)) return false;
        bool isNew = source == null || saveAs;
        string saveId = draft.wonderId;
        if (saveAs && source != null)
        {
            // Save As is an independent wonder with independent progress/localization keys.
            saveId = Path.GetFileNameWithoutExtension(path);
            if (saveId.StartsWith("Wonder_", StringComparison.Ordinal)) saveId = saveId.Substring(7);
            if (saveId == source.wonderId || saveId.Any(c => !char.IsLetterOrDigit(c) && c != '_'))
            { status = "Farklı Kaydet için dosyada yeni bir kimlik kullanın (ör. Wonder_mis9.asset)."; return false; }
        }
        if (isNew && File.Exists(path)) { status = "Bu dosya zaten var; farklı bir ad seçin."; return false; }
        foreach (string guid in AssetDatabase.FindAssets("t:WonderDefinition"))
        {
            var existing = AssetDatabase.LoadAssetAtPath<WonderDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (existing != null && existing.wonderId == saveId && (isNew || existing != source))
            { status = "Aynı kimlik başka bir harikada kullanılıyor: " + saveId; return false; }
        }
        // Validate JSON and prepare the complete merge before changing any asset.
        WonderDefinition prepared = Instantiate(draft);
        WonderDefinition backup = source != null && !isNew ? Instantiate(source) : null;
        string originalName = source != null ? source.name : "";
        HideFlags originalFlags = source != null ? source.hideFlags : HideFlags.None;
        string oldJson = null;
        int oldFirst = catalog.firstWonderTotalStars, oldSecond = catalog.secondWonderTotalStars, oldLast = catalog.lastWonderTotalStars;
        bool wroteJson = false, created = false;
        Object saved = null;
        try
        {
            prepared.wonderId = saveId;
            prepared.starCosts = Enumerable.Range(0, prepared.tasks.Length).Select(i => Mathf.Max(1, prepared.GetStarCost(i))).ToArray();
            foreach (var task in prepared.tasks) task.starCost = 0;
            prepared.totalStages = prepared.tasks.Length;
            oldJson = File.ReadAllText(WonderMissionAuthoring.LocalizationPath);
            string merged = WonderMissionAuthoring.MergeLocalization(oldJson, prepared, englishTitle, englishTasks);
            if (isNew)
            {
                prepared.hideFlags = HideFlags.None;
                prepared.name = Path.GetFileNameWithoutExtension(path);
                AssetDatabase.CreateAsset(prepared, path); created = true; saved = prepared;
                Undo.RegisterCreatedObjectUndo(prepared, "Harika oluştur");
            }
            else
            {
                Undo.RecordObject(source, "Harika görevlerini kaydet");
                var flags = source.hideFlags; string name = source.name;
                EditorUtility.CopySerialized(prepared, source); source.hideFlags = flags; source.name = name;
                EditorUtility.SetDirty(source); saved = source;
            }
            bool budgetChanged = catalog.firstWonderTotalStars != budgetDraft.firstWonderTotalStars
                || catalog.secondWonderTotalStars != budgetDraft.secondWonderTotalStars || catalog.lastWonderTotalStars != budgetDraft.lastWonderTotalStars;
            if (budgetChanged)
            {
                Undo.RecordObject(catalog, "Harika yıldız toplamları");
                catalog.firstWonderTotalStars = budgetDraft.firstWonderTotalStars;
                catalog.secondWonderTotalStars = budgetDraft.secondWonderTotalStars;
                catalog.lastWonderTotalStars = budgetDraft.lastWonderTotalStars;
                EditorUtility.SetDirty(catalog);
            }
            if (merged != oldJson) { WonderMissionAuthoring.WriteLocalization(merged); wroteJson = true; }
            AssetDatabase.SaveAssetIfDirty(saved);
            if (budgetChanged) AssetDatabase.SaveAssetIfDirty(catalog);
            if (wroteJson) AssetDatabase.ImportAsset(WonderMissionAuthoring.LocalizationPath);
            source = (WonderDefinition)saved;
            draft.wonderId = saveId;
            hasUnsavedChanges = false;
            status = "Kaydedildi: " + path + (isNew ? " · Kataloğa ekleme düğmesi sol panelde." : "");
            EditorGUIUtility.PingObject(source);
            return true;
        }
        catch (Exception ex)
        {
            // Restore the saved files when a write fails; keep the editable draft open.
            if (backup != null) { EditorUtility.CopySerialized(backup, source); source.hideFlags = originalFlags; source.name = originalName; EditorUtility.SetDirty(source); AssetDatabase.SaveAssetIfDirty(source); }
            if (created) AssetDatabase.DeleteAsset(path);
            catalog.firstWonderTotalStars = oldFirst; catalog.secondWonderTotalStars = oldSecond; catalog.lastWonderTotalStars = oldLast;
            EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssetIfDirty(catalog);
            if (wroteJson) { WonderMissionAuthoring.WriteLocalization(oldJson); AssetDatabase.ImportAsset(WonderMissionAuthoring.LocalizationPath); }
            status = "Kaydedilemedi; taslak korundu: " + ex.Message;
            return false;
        }
        finally
        {
            if (!created && prepared != null) DestroyImmediate(prepared);
            if (backup != null) DestroyImmediate(backup);
        }
    }
}
#endif
