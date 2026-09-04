using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// T_SliderHandle.png 한 장(금속 레일 + 원형 핸들이 함께 그려져 있음)을
/// 트랙/핸들 두 스프라이트로 잘라내고, 옵션 패널의 볼륨/밝기 슬라이더에 입힌다.
/// 좌표를 손으로 넣지 않고 알파 채널을 분석해 원형 핸들의 위치와 크기를 스스로 찾는다.
/// 메뉴에서 실행하며, 여러 번 실행해도 결과가 같다.
///
/// 이 파일은 에디터 전용이라 Transform.Find 를 쓴다. CLAUDE.md 3번의 Find 금지는
/// 런타임 비용 때문이고, 여기는 편집 시점에 한 번만 도는 코드다.
/// </summary>
public static class SliderSkinSetup
{
    const string SourcePath = "Assets/@AssetsManagement/UI/Sprites/T_SliderHandle.png";
    const string KnobPath = "Assets/@AssetsManagement/UI/Sprites/T_SliderKnob.png";
    const string TrackPath = "Assets/@AssetsManagement/UI/Sprites/T_SliderTrack.png";
    const string ScenePath = "Assets/@Scenes/MainMenu.unity";

    const byte AlphaThreshold = 25;

    // 핸들은 막대보다 커야 안쪽 "쉿" 아이콘이 보인다. 막대 높이의 배수로 잡고 최소치를 둔다.
    const float KnobSizeMultiplier = 2.4f;
    const float MinKnobSize = 34f;

    static readonly string[] TargetSliderNames = { "VolumeSlider", "BrightnessSlider" };

    // ---------------------------------------------------------------- 1단계

    [MenuItem("Tools/QuietPlace/슬라이더 스킨 - 1. 스프라이트 잘라내기")]
    public static void SliceSprites()
    {
        Texture2D src = LoadReadable(SourcePath);
        if (src == null) return;

        int w = src.width;
        int h = src.height;
        Color32[] px = src.GetPixels32();

        // 배경 제거 도구가 투명 대신 체커무늬를 픽셀에 구워넣은 경우를 되살린다.
        // 체커는 r=g=b 로 완전히 균일한 회색이고, 금속은 사진이라 채널값이 미세하게 다르다.
        // 테두리에서 번지게(flood fill) 해서 피사체 안쪽 회색은 건드리지 않는다.
        int keyed = KeyOutBakedCheckerboard(px, w, h);
        if (keyed > 0)
            Debug.Log($"[SliderSkinSetup] 알파가 전부 불투명해 구워진 체커무늬로 판단하고 배경 {keyed}픽셀을 투명 처리했다.");

        // 열마다 불투명 픽셀의 세로 범위를 잰다. 원형 핸들은 레일보다 훨씬 높으므로
        // 세로 범위가 가장 큰 열이 곧 핸들의 중심이고, 그 범위가 핸들의 지름이 된다.
        int[] extents = new int[w];
        for (int x = 0; x < w; x++) extents[x] = ColumnExtent(px, w, h, x);

        // 배경 제거 후에도 1~2픽셀 폭의 잔재가 세로줄로 남을 수 있다. 그런 줄이 최댓값을
        // 훔쳐가지 않도록, 각 열의 값을 좌우 이웃과의 최소값으로 눌러서 판단한다.
        int[] robust = new int[w];
        for (int x = 0; x < w; x++)
        {
            int m = extents[x];
            for (int d = -3; d <= 3; d++)
                m = Mathf.Min(m, extents[Mathf.Clamp(x + d, 0, w - 1)]);
            robust[x] = m;
        }

        int centerX = -1;
        int centerY = 0;
        int maxExtent = 0;
        int firstX = -1;

        for (int x = 0; x < w; x++)
        {
            if (robust[x] <= 0) continue;
            if (firstX < 0) firstX = x;

            if (robust[x] > maxExtent)
            {
                maxExtent = robust[x];
                centerX = x;
            }
        }

        if (centerX >= 0)
        {
            int top = -1, bottom = -1;
            for (int y = 0; y < h; y++)
            {
                if (px[y * w + centerX].a <= AlphaThreshold) continue;
                if (top < 0) top = y;
                bottom = y;
            }
            centerY = (top + bottom) / 2;
        }

        if (centerX < 0)
        {
            Debug.LogError($"[SliderSkinSetup] {SourcePath} 에 불투명 픽셀이 없다. 배경이 제거된 PNG 인지 확인할 것.");
            return;
        }

        // 진단: 알파 분포가 예상과 다를 때 원인을 눈으로 확인할 수 있게 남긴다
        System.Text.StringBuilder profile = new System.Text.StringBuilder();
        int step = Mathf.Max(1, w / 28);
        for (int x = 0; x < w; x += step)
            profile.Append(x).Append(':').Append(ColumnExtent(px, w, h, x)).Append("  ");

        int solidCenterX = -1;
        int solidMax = 0;
        for (int x = 0; x < w; x++)
        {
            int e = ColumnExtent(px, w, h, x, 200);
            if (e > solidMax) { solidMax = e; solidCenterX = x; }
        }

        // 배경이 정말 투명한지 확인하려고 실제 픽셀을 몇 개 찍어본다.
        // 배경 제거 도구가 체커무늬를 픽셀에 구워버리면 알파는 255 인데 눈에는 투명해 보인다.
        System.Text.StringBuilder samples = new System.Text.StringBuilder();
        int[,] pts = { { 0, 0 }, { 8, 8 }, { 24, 24 }, { 40, h - 40 }, { w / 2, h / 2 }, { 100, h / 2 } };
        for (int i = 0; i < pts.GetLength(0); i++)
        {
            int sx = Mathf.Clamp(pts[i, 0], 0, w - 1);
            int sy = Mathf.Clamp(pts[i, 1], 0, h - 1);
            Color32 c = px[sy * w + sx];
            samples.Append($"({sx},{sy})=RGBA({c.r},{c.g},{c.b},{c.a})  ");
        }

        Debug.Log($"[SliderSkinSetup] 진단 - 크기 {w}x{h}, firstX={firstX}, " +
                  $"centerX={centerX}, centerY={centerY}, maxExtent={maxExtent} (임계 {AlphaThreshold})\n" +
                  $"불투명(알파>200) 기준: centerX={solidCenterX}, maxExtent={solidMax}\n" +
                  $"픽셀 표본: {samples}\n" +
                  $"열별 세로범위: {profile}");

        // 핸들은 원형이므로 최대 세로 범위를 지름으로 보고 정사각형으로 잘라낸다.
        int radius = maxExtent / 2;
        int knobX = Mathf.Max(0, centerX - radius);
        int knobY = Mathf.Max(0, centerY - radius);
        int knobW = Mathf.Min(w - knobX, radius * 2);
        int knobH = Mathf.Min(h - knobY, radius * 2);

        // 트랙은 핸들 왼쪽의 순수 레일 구간을 쓴다. 핸들이 섞여 들어가면 안 된다.
        int trackRight = knobX - 4;
        if (trackRight - firstX < 8)
        {
            Debug.LogError("[SliderSkinSetup] 핸들 왼쪽에 레일 구간이 부족해 트랙을 잘라낼 수 없다.");
            return;
        }

        int trackTop = h;
        int trackBottom = -1;
        for (int x = firstX; x <= trackRight; x++)
        {
            for (int y = 0; y < h; y++)
            {
                if (px[y * w + x].a <= AlphaThreshold) continue;
                if (y < trackTop) trackTop = y;
                if (y > trackBottom) trackBottom = y;
            }
        }

        int trackW = trackRight - firstX + 1;
        int trackH = trackBottom - trackTop + 1;

        WritePng(px, w, h, knobX, knobY, knobW, knobH, KnobPath);
        WritePng(px, w, h, firstX, trackTop, trackW, trackH, TrackPath);

        AssetDatabase.Refresh();
        ConfigureSprite(KnobPath);
        ConfigureSprite(TrackPath);

        Debug.Log($"[SliderSkinSetup] 원본 {w}x{h} -> 핸들 {knobW}x{knobH}, 트랙 {trackW}x{trackH} 생성 완료.\n" +
                  $"  {KnobPath}\n  {TrackPath}\n이어서 메뉴 2번을 실행할 것.");
    }

    // ---------------------------------------------------------------- 2단계

    [MenuItem("Tools/QuietPlace/슬라이더 스킨 - 2. 옵션 슬라이더에 적용")]
    public static void ApplyToSliders()
    {
        Sprite knob = AssetDatabase.LoadAssetAtPath<Sprite>(KnobPath);
        Sprite track = AssetDatabase.LoadAssetAtPath<Sprite>(TrackPath);

        if (knob == null || track == null)
        {
            Debug.LogError("[SliderSkinSetup] 잘라낸 스프라이트가 없다. 메뉴 1번을 먼저 실행할 것.");
            return;
        }

        if (SceneManager.GetActiveScene().path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        Slider[] all = Object.FindObjectsByType<Slider>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int done = 0;

        for (int i = 0; i < all.Length; i++)
        {
            if (System.Array.IndexOf(TargetSliderNames, all[i].name) < 0) continue;
            if (Skin(all[i], knob, track)) done++;
        }

        if (done == 0)
        {
            Debug.LogWarning($"[SliderSkinSetup] {string.Join(", ", TargetSliderNames)} 을 찾지 못했다. 이름이 바뀌었는지 확인할 것.");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[SliderSkinSetup] 슬라이더 {done}개에 적용하고 씬을 저장했다.");
    }

    // ---------------------------------------------------------------- 3단계

    // 옵션 패널은 레이아웃 그룹 없이 절대좌표로 배치돼 있다. 이름과 y 를 짝지어 한 번에 지정하면
    // 여러 번 실행해도 요소가 계속 아래로 밀리지 않는다.
    static readonly string[] OptionsOrder =
    {
        "VolumeLabel", "VolumeSlider",
        "MonsterVolumeLabel", "MonsterVolumeSlider",
        "BrightnessLabel", "BrightnessSlider",
        "OptionsCloseButton",
    };

    static readonly float[] OptionsY = { 130f, 90f, 30f, -10f, -70f, -110f, -200f };

    [MenuItem("Tools/QuietPlace/슬라이더 스킨 - 3. 괴물소리 슬라이더 추가")]
    public static void AddMonsterVolumeSlider()
    {
        if (SceneManager.GetActiveScene().path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        Slider volume = null;
        Slider[] all = Object.FindObjectsByType<Slider>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
            if (all[i].name == "VolumeSlider") { volume = all[i]; break; }

        if (volume == null)
        {
            Debug.LogError("[SliderSkinSetup] VolumeSlider 를 찾지 못해 복제 기준이 없다.");
            return;
        }

        Transform panel = volume.transform.parent;
        Transform volumeLabel = panel.Find("VolumeLabel");

        if (volumeLabel == null)
        {
            Debug.LogError("[SliderSkinSetup] VolumeLabel 을 찾지 못했다.");
            return;
        }

        // 처음부터 만들지 않고 사운드 라벨/슬라이더를 복제한다.
        // 폰트, 색, 크기, 방금 입힌 스킨까지 그대로 물려받는 게 목적이다.
        Transform label = panel.Find("MonsterVolumeLabel");
        if (label == null)
        {
            GameObject clone = Object.Instantiate(volumeLabel.gameObject, panel);
            clone.name = "MonsterVolumeLabel";
            Undo.RegisterCreatedObjectUndo(clone, "Add MonsterVolumeLabel");
            label = clone.transform;
        }

        TMPro.TextMeshProUGUI text = label.GetComponent<TMPro.TextMeshProUGUI>();
        if (text != null)
        {
            Undo.RecordObject(text, "Set Label Text");
            text.text = "괴물소리";
            EditorUtility.SetDirty(text);
        }
        else
        {
            Debug.LogWarning("[SliderSkinSetup] MonsterVolumeLabel 에 TextMeshProUGUI 가 없어 문구를 바꾸지 못했다.");
        }

        Transform slider = panel.Find("MonsterVolumeSlider");
        if (slider == null)
        {
            GameObject clone = Object.Instantiate(volume.gameObject, panel);
            clone.name = "MonsterVolumeSlider";
            Undo.RegisterCreatedObjectUndo(clone, "Add MonsterVolumeSlider");
            slider = clone.transform;

            // 복제본이 실제 볼륨을 건드리면 안 된다. 기능 연결은 나중 작업이다.
            Slider copied = clone.GetComponent<Slider>();
            if (copied != null)
            {
                copied.onValueChanged = new Slider.SliderEvent();
                copied.SetValueWithoutNotify(1f);
                EditorUtility.SetDirty(copied);
            }
        }

        // 전체 배치를 절대좌표로 다시 지정한다
        int placed = 0;
        for (int i = 0; i < OptionsOrder.Length; i++)
        {
            Transform t = panel.Find(OptionsOrder[i]);
            if (t == null) continue;

            RectTransform rt = t as RectTransform;
            if (rt == null) continue;

            Undo.RecordObject(rt, "Layout Options");
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, OptionsY[i]);
            EditorUtility.SetDirty(rt);
            placed++;
        }

        // 계층 순서도 화면 순서와 맞춘다
        for (int i = 0; i < OptionsOrder.Length; i++)
        {
            Transform t = panel.Find(OptionsOrder[i]);
            if (t != null) t.SetSiblingIndex(i);
        }

        Scene scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[SliderSkinSetup] 괴물소리 슬라이더를 추가하고 옵션 요소 {placed}개의 위치를 다시 잡았다. 기능(onValueChanged)은 비어 있다.");
    }

    static bool Skin(Slider slider, Sprite knob, Sprite track)
    {
        RectTransform root = slider.transform as RectTransform;
        if (root == null)
        {
            Debug.LogWarning($"[SliderSkinSetup] {slider.name} 이 RectTransform 이 아니다. 건너뛴다.");
            return false;
        }

        float height = root.rect.height;
        if (height <= 1f) height = 20f;

        float knobSize = Mathf.Max(MinKnobSize, height * KnobSizeMultiplier);

        // 트랙: 이 프로젝트의 슬라이더는 루트에 직접 Image 가 붙어 있고 그것이 지금 흰 막대로 보인다.
        // 별도 Background 자식을 만들면 흰 막대가 그대로 남으므로 루트 Image 를 트랙으로 바꾼다.
        Image rootImage = EnsureImage(root, track, false);
        rootImage.raycastTarget = true; // 막대 아무 곳이나 눌러 값을 바꿀 수 있어야 한다

        // 핸들이 움직이는 영역. 핸들 반지름만큼 좌우를 안쪽으로 당겨 끝에서 튀어나오지 않게 한다.
        RectTransform area = EnsureChild(root, "Handle Slide Area");
        if (area == null) return false;
        area.anchorMin = Vector2.zero;
        area.anchorMax = Vector2.one;
        area.pivot = new Vector2(0.5f, 0.5f);
        area.offsetMin = new Vector2(knobSize * 0.5f, 0f);
        area.offsetMax = new Vector2(-knobSize * 0.5f, 0f);
        area.SetAsLastSibling();

        // 핸들. 원이 찌그러지지 않도록 정사각형으로 고정한다.
        RectTransform handle = EnsureChild(area, "Handle");
        if (handle == null) return false;
        handle.anchorMin = new Vector2(0f, 0.5f);
        handle.anchorMax = new Vector2(0f, 0.5f);
        handle.pivot = new Vector2(0.5f, 0.5f);
        handle.sizeDelta = new Vector2(knobSize, knobSize);
        handle.anchoredPosition = Vector2.zero;
        Image handleImage = EnsureImage(handle, knob, true);

        Undo.RecordObject(slider, "Skin Slider");
        slider.direction = Slider.Direction.LeftToRight;
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;
        EditorUtility.SetDirty(slider);
        return true;
    }

    // ---------------------------------------------------------------- 유틸

    static RectTransform EnsureChild(Transform parent, string name)
    {
        Transform found = parent.Find(name);
        if (found != null)
        {
            RectTransform rt = found as RectTransform;
            if (rt == null)
                Debug.LogWarning($"[SliderSkinSetup] {parent.name}/{name} 이 RectTransform 이 아니다.");
            return rt;
        }

        GameObject go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static Image EnsureImage(RectTransform rt, Sprite sprite, bool preserveAspect)
    {
        Image img = rt.GetComponent<Image>();
        if (img == null) img = Undo.AddComponent<Image>(rt.gameObject);

        Undo.RecordObject(img, "Set Slider Sprite");
        img.sprite = sprite;
        img.type = Image.Type.Simple;
        img.color = Color.white;
        img.preserveAspect = preserveAspect;
        EditorUtility.SetDirty(img);
        return img;
    }

    /// <summary>
    /// 알파가 전부 불투명하면 배경이 체커무늬로 구워진 것으로 보고, 테두리에서 번져 들어가며
    /// 균일 회색 배경만 알파 0 으로 만든다. 실제로 투명한 PNG 라면 아무것도 하지 않는다.
    /// </summary>
    static int KeyOutBakedCheckerboard(Color32[] px, int w, int h)
    {
        // 이미 투명한 픽셀이 있으면 정상적인 알파 PNG 다
        for (int i = 0; i < px.Length; i++)
            if (px[i].a <= AlphaThreshold) return 0;

        bool[] visited = new bool[px.Length];
        System.Collections.Generic.Queue<int> queue = new System.Collections.Generic.Queue<int>();

        // 테두리 픽셀 중 배경으로 보이는 것을 시작점으로 넣는다
        for (int x = 0; x < w; x++)
        {
            Enqueue(px, visited, queue, w, x, 0);
            Enqueue(px, visited, queue, w, x, h - 1);
        }
        for (int y = 0; y < h; y++)
        {
            Enqueue(px, visited, queue, w, 0, y);
            Enqueue(px, visited, queue, w, w - 1, y);
        }

        int cleared = 0;
        while (queue.Count > 0)
        {
            int index = queue.Dequeue();
            int x = index % w;
            int y = index / w;

            px[index].a = 0;
            cleared++;

            if (x > 0) Enqueue(px, visited, queue, w, x - 1, y);
            if (x < w - 1) Enqueue(px, visited, queue, w, x + 1, y);
            if (y > 0) Enqueue(px, visited, queue, w, x, y - 1);
            if (y < h - 1) Enqueue(px, visited, queue, w, x, y + 1);
        }

        return cleared;
    }

    static void Enqueue(Color32[] px, bool[] visited, System.Collections.Generic.Queue<int> queue, int w, int x, int y)
    {
        int index = y * w + x;
        if (visited[index]) return;
        if (!IsFlatGrayBackground(px[index])) return;

        visited[index] = true;
        queue.Enqueue(index);
    }

    static bool IsFlatGrayBackground(Color32 c)
    {
        // 체커는 완전한 무채색이다. 사진인 금속은 채널이 미세하게 어긋나므로(173,172,168 / 78,78,76)
        // 채널 완전 일치를 요구하면 배경만 정확히 골라낼 수 있다.
        if (c.r != c.g || c.g != c.b) return false;

        // 관찰된 체커 두 칸은 113 과 143 이고, 칸 경계는 그 사이 값(128 등)으로 흐려져 있다.
        // 사이 구간을 통째로 덮어야 격자선이 세로 줄무늬로 남지 않는다.
        // 레일은 78, 핸들 밝은 부분은 173 이라 이 구간과 겹치지 않는다.
        return c.r >= 100 && c.r <= 156;
    }

    static int ColumnExtent(Color32[] px, int w, int h, int x, byte threshold = AlphaThreshold)
    {
        int top = -1;
        int bottom = -1;

        for (int y = 0; y < h; y++)
        {
            if (px[y * w + x].a <= threshold) continue;
            if (top < 0) top = y;
            bottom = y;
        }

        return top < 0 ? 0 : bottom - top + 1;
    }

    static Texture2D LoadReadable(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError($"[SliderSkinSetup] {path} 를 찾을 수 없다. Unity 가 임포트했는지 확인할 것.");
            return null;
        }

        // 픽셀을 원본 그대로 읽어야 한다. 기본 임포트 설정으로는 두 가지가 망가진다.
        //  - npotScale: 888x330 같은 비-2의거듭제곱 이미지가 1024x256 으로 리샘플된다
        //  - textureCompression: 압축 과정에서 알파가 뭉개져 배경과 도형 구분이 사라진다
        bool changed = false;

        if (!importer.isReadable) { importer.isReadable = true; changed = true; }
        if (importer.npotScale != TextureImporterNPOTScale.None) { importer.npotScale = TextureImporterNPOTScale.None; changed = true; }
        if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }
        if (importer.alphaSource != TextureImporterAlphaSource.FromInput) { importer.alphaSource = TextureImporterAlphaSource.FromInput; changed = true; }
        if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; changed = true; }
        if (importer.mipmapEnabled) { importer.mipmapEnabled = false; changed = true; }
        if (importer.maxTextureSize < 2048) { importer.maxTextureSize = 2048; changed = true; }

        if (changed) importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    /// <summary>투명 처리까지 끝난 픽셀 배열에서 직접 잘라내 PNG 로 저장한다.</summary>
    static void WritePng(Color32[] px, int srcW, int srcH, int x, int y, int cw, int ch, string path)
    {
        Color32[] cutPixels = new Color32[cw * ch];

        for (int j = 0; j < ch; j++)
        {
            int srcRow = (y + j) * srcW;
            int dstRow = j * cw;
            for (int i = 0; i < cw; i++)
                cutPixels[dstRow + i] = px[srcRow + x + i];
        }

        Texture2D cut = new Texture2D(cw, ch, TextureFormat.RGBA32, false);
        cut.SetPixels32(cutPixels);
        cut.Apply();
        System.IO.File.WriteAllBytes(path, cut.EncodeToPNG());
        Object.DestroyImmediate(cut);
    }

    static void ConfigureSprite(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.SaveAndReimport();
    }
}
