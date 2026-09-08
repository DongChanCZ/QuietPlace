using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// P_TransformerRepair 안의 3단계(모듈 안착)를 화면 UI 방식으로 다시 조립한다.
///
/// 3D 큐브를 끄는 옛 구성(Module + SlotTarget + GaugeUI)은 지우지 않고 끄기만 한다.
/// 프리뷰 씬이나 나중에 되돌릴 일이 있을 수 있으니 남겨두는 게 안전하다.
/// </summary>
public static class ModuleBalanceUIBuilder
{
    const string PrefabPath = "Assets/@Resources/Prefabs/Manager/P_TransformerRepair.prefab";
    const string StageName = "Stage_ModuleBalance";
    const string UIName = "BalanceUI";
    const string KoreanFontPath = "Assets/Fonts/MalgunGothic SDF.asset";

    static TMP_FontAsset koreanFont;

    [MenuItem("Tools/QuietPlace/변압기 모듈 안착 UI 만들기")]
    public static void Build()
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (contents == null) { Debug.LogError($"[ModuleBalanceUIBuilder] {PrefabPath} 를 못 열었다."); return; }

        try
        {
            Transform stage = null;
            foreach (Transform t in contents.GetComponentsInChildren<Transform>(true))
                if (t.name == StageName) stage = t;

            if (stage == null) { Debug.LogError($"[ModuleBalanceUIBuilder] {StageName} 이 없다."); return; }

            // 옛 3D 구성은 끄기만 한다
            int turnedOff = 0;
            foreach (Transform child in stage)
            {
                if (child.name != "Module" && child.name != "GaugeUI") continue;
                if (!child.gameObject.activeSelf) continue;
                child.gameObject.SetActive(false);
                turnedOff++;
            }

            // 이미 만든 UI 가 있으면 지우고 새로 만든다
            Transform old = stage.Find(UIName);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            GameObject uiRoot = new GameObject(UIName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            uiRoot.transform.SetParent(stage, false);

            Canvas canvas = uiRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = uiRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            Image dim = Img(uiRoot.transform, "Dim", new Color(0.02f, 0.02f, 0.03f, 0.88f), Vector2.zero, Vector2.one * 100f);
            Stretch(dim.rectTransform);

            Label(uiRoot.transform, "Title", "변압기 모듈 안착", 48f, new Vector2(0f, 400f), new Color(0.6f, 0.85f, 1f));
            TMP_Text hint = Label(uiRoot.transform, "Hint", "모듈을 왼쪽 버튼으로 잡아라", 28f,
                new Vector2(0f, 348f), new Color(0.78f, 0.82f, 0.86f));

            // ---------- 움직일 수 있는 영역 ----------
            GameObject areaGo = Empty(uiRoot.transform, "Area");
            RectTransform area = (RectTransform)areaGo.transform;
            SetBox(area, new Vector2(0f, 20f), new Vector2(1100f, 620f));
            Image areaBg = Img(area, "AreaBG", new Color(1f, 1f, 1f, 0.05f), Vector2.zero, new Vector2(1100f, 620f));

            // ---------- 슬롯 (목표) ----------
            // 원래는 렌더러 없는 빈 Transform 이라 어디로 넣어야 할지 안 보였다.
            GameObject slotGo = Empty(area, "Slot");
            RectTransform slot = (RectTransform)slotGo.transform;
            SetBox(slot, new Vector2(300f, -140f), new Vector2(190f, 190f));

            Img(slot, "SlotHole", new Color(0.05f, 0.06f, 0.07f, 0.95f), Vector2.zero, new Vector2(190f, 190f));
            Image slotFrame = Img(slot, "SlotFrame", new Color(0.95f, 0.4f, 0.35f), Vector2.zero, new Vector2(210f, 210f));
            slotFrame.type = Image.Type.Sliced;
            slotFrame.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd");
            slotFrame.transform.SetAsFirstSibling();   // 테두리가 구멍 뒤에 깔린다
            Label(slot, "SlotLabel", "여기", 26f, new Vector2(0f, -130f), new Color(0.8f, 0.84f, 0.88f));

            // ---------- 모듈 (끌 대상) ----------
            GameObject moduleGo = Empty(area, "ModuleIcon");
            RectTransform module = (RectTransform)moduleGo.transform;
            SetBox(module, new Vector2(-330f, 170f), new Vector2(160f, 160f));

            Img(module, "Body", new Color(0.42f, 0.45f, 0.5f), Vector2.zero, new Vector2(160f, 160f));
            Img(module, "CoilL", new Color(0.72f, 0.45f, 0.2f), new Vector2(-40f, 0f), new Vector2(44f, 110f));
            Img(module, "CoilR", new Color(0.72f, 0.45f, 0.2f), new Vector2(40f, 0f), new Vector2(44f, 110f));
            Img(module, "Plate", new Color(0.58f, 0.61f, 0.66f), new Vector2(0f, 64f), new Vector2(160f, 26f));
            Label(module, "Weight", "25kg", 22f, new Vector2(0f, -58f), new Color(0.9f, 0.92f, 0.95f));

            // ---------- 유지 게이지 ----------
            Label(uiRoot.transform, "GaugeLabel", "안착 유지", 22f, new Vector2(-420f, -390f), new Color(0.7f, 0.72f, 0.75f));
            Img(uiRoot.transform, "GaugeBG", new Color(1f, 1f, 1f, 0.14f), new Vector2(0f, -390f), new Vector2(700f, 26f));
            Image gauge = Img(uiRoot.transform, "GaugeFill", new Color(0.45f, 0.9f, 0.6f, 0.95f),
                new Vector2(0f, -390f), new Vector2(700f, 26f));
            gauge.type = Image.Type.Filled;
            gauge.fillMethod = Image.FillMethod.Horizontal;
            gauge.fillOrigin = 0;
            gauge.fillAmount = 0f;

            Label(uiRoot.transform, "Escape", "X  그만두기", 22f, new Vector2(0f, -440f), new Color(0.55f, 0.58f, 0.62f));

            // ---------- 컴포넌트 ----------
            ModuleBalanceUI balance = stage.GetComponent<ModuleBalanceUI>();
            if (balance == null) balance = stage.gameObject.AddComponent<ModuleBalanceUI>();

            balance.module = module;
            balance.slot = slot;
            balance.bounds = area;
            balance.grabRadius = 170f;
            balance.pullStrength = 20f;
            balance.damping = 4.2f;
            balance.gravity = 900f;
            balance.releaseGravityScale = 2.5f;
            balance.swayStrength = 900f;
            balance.swaySpeed = 0.85f;
            balance.tremorStrength = 620f;
            balance.tremorSpeed = 11f;
            balance.toleranceRadius = 40f;
            balance.requiredHoldTime = 3f;
            balance.gaugeFill = gauge;
            balance.slotFrame = slotFrame;
            balance.hintLabel = hint;

            // 시퀀서에 UI 방식을 물린다
            TransformerRepairSequence seq = contents.GetComponent<TransformerRepairSequence>();
            if (seq != null)
            {
                seq.moduleBalanceUI = balance;
                seq.moduleBalanceStage = null;   // 3D 방식은 더 이상 듣지 않는다
            }

            PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
            Debug.Log($"[ModuleBalanceUIBuilder] 조립 완료 -> {PrefabPath}\n" +
                      $"  옛 3D 구성 {turnedOff}개 비활성 (지우지 않음)\n" +
                      $"  슬롯 허용 {balance.toleranceRadius}px, 유지 {balance.requiredHoldTime}초\n" +
                      $"  당김 {balance.pullStrength}, 중력 {balance.gravity}px/s^2 " +
                      $"(커서를 슬롯보다 약 {(balance.gravity / balance.pullStrength):0}px 위에 둬야 균형), " +
                      $"흔들림 {balance.swayStrength}+{balance.tremorStrength}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }

        AssetDatabase.Refresh();
    }

    // ---------------------------------------------------------------- 유틸

    static GameObject Empty(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    static Image Img(Transform parent, string name, Color color, Vector2 pos, Vector2 size)
    {
        GameObject go = Empty(parent, name);
        Image img = go.AddComponent<Image>();
        img.color = color;
        img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        img.type = Image.Type.Sliced;
        img.raycastTarget = false;
        SetBox(img.rectTransform, pos, size);
        return img;
    }

    static TMP_Text Label(Transform parent, string name, string text, float size, Vector2 pos, Color color)
    {
        GameObject go = Empty(parent, name);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();

        if (koreanFont == null) koreanFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontPath);
        if (koreanFont != null) tmp.font = koreanFont;

        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        SetBox(tmp.rectTransform, pos, new Vector2(1000f, size * 1.7f));
        return tmp;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void SetBox(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }
}
