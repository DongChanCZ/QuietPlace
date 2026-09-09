using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 낡은 아날로그 전압계를 UI Image 만으로 조립한다.
///
/// 텍스처를 새로 만들지 않는다 - 눈금은 중심에서 회전시킨 얇은 막대, 표면의 얼룩과
/// 유리 반사는 반투명 사각형을 겹쳐 흉내낸다. 텍스처가 없으니 어떤 크기로 키워도
/// 흐려지지 않고, 색만 바꿔 다른 계기(전압/토크/주파수)로 재활용할 수 있다.
///
/// 다른 빌더에서 MakeGauge 를 불러 쓴다.
/// </summary>
public static class AnalogGaugeBuilder
{
    const string KoreanFontPath = "Assets/Fonts/MalgunGothic SDF.asset";
    static TMP_FontAsset koreanFont;

    // 낡은 계기의 색. 크림색 눈금판 + 놋쇠 테 + 검은 바늘.
    public static readonly Color DialFace = new Color(0.85f, 0.81f, 0.70f);
    public static readonly Color DialFaceEdge = new Color(0.72f, 0.67f, 0.55f);
    public static readonly Color Bezel = new Color(0.30f, 0.27f, 0.22f);
    public static readonly Color BezelRim = new Color(0.55f, 0.47f, 0.32f);   // 놋쇠
    public static readonly Color Ink = new Color(0.16f, 0.15f, 0.13f);
    public static readonly Color InkFaint = new Color(0.16f, 0.15f, 0.13f, 0.45f);
    public static readonly Color Danger = new Color(0.72f, 0.18f, 0.13f);
    public static readonly Color Glass = new Color(1f, 1f, 1f, 0.05f);

    /// <summary>
    /// 계기 하나를 만든다. 반환된 AnalogGauge 에 SetValue01 로 값을 넣으면 바늘이 움직인다.
    /// </summary>
    /// <param name="size">눈금판 지름(px)</param>
    /// <param name="label">눈금판에 인쇄할 이름 (예: "TORQUE")</param>
    /// <param name="maxReading">눈금판 오른쪽 끝 숫자</param>
    public static AnalogGauge MakeGauge(Transform parent, string name, Vector2 pos, float size,
        string label, int maxReading, string unit = "V", bool showDanger = true)
    {
        GameObject go = Empty(parent, name);
        RectTransform rt = (RectTransform)go.transform;
        SetBox(rt, pos, new Vector2(size, size));

        // ---- 테두리: 놋쇠 링 + 어두운 몸통 ----
        Circle(go.transform, "BezelRim", BezelRim, Vector2.zero, size);
        Circle(go.transform, "BezelBody", Bezel, Vector2.zero, size * 0.955f);

        // ---- 눈금판 ----
        float face = size * 0.86f;
        float R = face * 0.5f;                  // 눈금판 반지름. 모든 자리를 이 값으로 잡는다.
        Circle(go.transform, "FaceEdge", DialFaceEdge, Vector2.zero, face);
        Circle(go.transform, "Face", DialFace, Vector2.zero, face - size * 0.03f);

        // 세월 얼룩. 사각형으로 두면 눈금판 위에 네모가 얹힌 게 그대로 보이므로
        // 둥근 반점으로 흩뿌린다.
        Circle(go.transform, "Stain0", new Color(0.55f, 0.48f, 0.36f, 0.11f),
            new Vector2(-R * 0.18f, -R * 0.46f), R * 0.95f);
        Circle(go.transform, "Stain1", new Color(0.40f, 0.34f, 0.24f, 0.08f),
            new Vector2(R * 0.42f, R * 0.16f), R * 0.60f);
        Circle(go.transform, "Stain2", new Color(0.48f, 0.42f, 0.30f, 0.07f),
            new Vector2(-R * 0.50f, R * 0.30f), R * 0.44f);

        // ---- 눈금 ----
        // 바늘과 같은 각도 범위. 큰 눈금 5개(모두 숫자), 그 사이에 작은 눈금 3개씩.
        const float MinAngle = 62f, MaxAngle = -62f;
        const int Majors = 5;
        const int MinorsPerMajor = 3;

        float tickOut = R * 0.94f;                        // 눈금 바깥 끝
        float majorIn = R * 0.76f;                        // 큰 눈금 안쪽 끝
        float minorIn = R * 0.85f;
        float numR = R * 0.555f;                           // 숫자 자리

        for (int i = 0; i < Majors; i++)
        {
            float t = (float)i / (Majors - 1);
            float angle = Mathf.Lerp(MinAngle, MaxAngle, t);
            bool inDanger = showDanger && t >= 0.75f;

            // 눈금 자체는 늘 검게 둔다. 위험 구간은 아래 붉은 띠가 알려준다 -
            // 눈금까지 붉게 물들이면 띠와 겹쳐 붉은 빗살무늬처럼 지저분해진다.
            Tick(go.transform, "TickMajor" + i, angle, majorIn, tickOut, size * 0.016f, Ink);

            float rad = (90f + angle) * Mathf.Deg2Rad;
            Label(go.transform, "Num" + i, Mathf.RoundToInt(maxReading * t).ToString(),
                size * 0.060f, new Vector2(Mathf.Cos(rad) * numR, Mathf.Sin(rad) * numR),
                inDanger ? Danger : Ink);

            if (i >= Majors - 1) continue;

            for (int m = 1; m <= MinorsPerMajor; m++)
            {
                float mt = (i + m / (float)(MinorsPerMajor + 1)) / (Majors - 1);
                float ma = Mathf.Lerp(MinAngle, MaxAngle, mt);
                Tick(go.transform, $"TickMinor{i}_{m}", ma, minorIn, tickOut, size * 0.008f, InkFaint);
            }
        }

        // ---- 위험 구간 띠 ----
        // 눈금 바로 안쪽에 붉은 띠. 부채꼴 스프라이트가 없으니 짧은 막대를 촘촘히 잇는다.
        Graphic dangerArc = null;
        if (showDanger)
        {
            GameObject arc = Empty(go.transform, "DangerArc");
            SetBox((RectTransform)arc.transform, Vector2.zero, Vector2.one * face);

            // 조각의 긴 쪽이 원주를 따라 눕도록 놓는다. 반지름 방향으로 세우면
            // 띠가 아니라 붉은 선이 뻗친 모양이 된다.
            const int Segs = 22;
            float bandR = R * 0.700f;
            float bandThickness = R * 0.085f;
            float arcSpan = Mathf.Abs(Mathf.Lerp(MinAngle, MaxAngle, 1f) - Mathf.Lerp(MinAngle, MaxAngle, 0.75f));
            float segWidth = bandR * (arcSpan / Segs) * Mathf.Deg2Rad * 1.35f;   // 조금 겹쳐 이음새를 지운다

            for (int i = 0; i <= Segs; i++)
            {
                float t = Mathf.Lerp(0.75f, 1f, i / (float)Segs);
                float a = Mathf.Lerp(MinAngle, MaxAngle, t);
                float rad2 = (90f + a) * Mathf.Deg2Rad;
                Image seg = Img(arc.transform, "Seg" + i, Danger,
                    new Vector2(Mathf.Cos(rad2) * bandR, Mathf.Sin(rad2) * bandR),
                    new Vector2(segWidth, bandThickness));
                seg.rectTransform.localRotation = Quaternion.Euler(0f, 0f, a);
            }
            dangerArc = arc.GetComponentInChildren<Image>();
        }

        // ---- 눈금판 인쇄 ----
        Label(go.transform, "DialLabel", label, size * 0.080f, new Vector2(0f, -R * 0.30f), Ink);
        Label(go.transform, "DialUnit", unit, size * 0.062f, new Vector2(0f, -R * 0.46f), InkFaint);

        // 숫자 창 - 눈금판 아래쪽에 작은 창을 파고 그 안에 값을 적는다
        Img(go.transform, "ReadoutWindow", new Color(0.20f, 0.19f, 0.16f, 0.85f),
            new Vector2(0f, -R * 0.66f), new Vector2(R * 0.86f, R * 0.24f));
        TMP_Text readout = Label(go.transform, "Readout", "0 " + unit, size * 0.062f,
            new Vector2(0f, -R * 0.66f), new Color(0.86f, 0.82f, 0.68f));

        // ---- 바늘 ----
        // pivot 을 아래쪽 끝에 두어 그 점을 축으로 돈다. 축보다 살짝 아래로 내려
        // 균형추가 반대편에 보이게 한다 - 실제 계기 바늘이 그렇게 생겼다.
        GameObject needleGo = Empty(go.transform, "Needle");
        RectTransform needle = (RectTransform)needleGo.transform;
        needle.anchorMin = needle.anchorMax = new Vector2(0.5f, 0.5f);
        needle.pivot = new Vector2(0.5f, 0.12f);
        needle.sizeDelta = new Vector2(size * 0.018f, R * 1.02f);
        needle.anchoredPosition = Vector2.zero;

        Image needleImg = needleGo.AddComponent<Image>();
        needleImg.color = Ink;
        needleImg.sprite = null;
        needleImg.raycastTarget = false;

        // 축 뚜껑 - 바늘 밑동을 덮는다
        Circle(go.transform, "Hub", new Color(0.20f, 0.18f, 0.15f), Vector2.zero, size * 0.095f);
        Circle(go.transform, "HubTop", BezelRim, Vector2.zero, size * 0.050f);

        // ---- 유리 ----
        // 위에서 들어온 빛이 유리에 맺힌 자리. 둥글게 두 겹으로 겹쳐 부드럽게 만든다.
        Circle(go.transform, "GlassSheen", new Color(1f, 1f, 1f, 0.045f),
            new Vector2(-R * 0.30f, R * 0.36f), R * 0.86f);
        Circle(go.transform, "GlassSheen2", new Color(1f, 1f, 1f, 0.035f),
            new Vector2(-R * 0.40f, R * 0.46f), R * 0.48f);
        Circle(go.transform, "GlassEdge", new Color(1f, 1f, 1f, 0.025f), Vector2.zero, face - size * 0.03f);

        // ---- 나사 4개 (테두리 위) ----
        float sc = size * 0.335f;
        foreach (var p in new[] { new Vector2(-sc, sc), new Vector2(sc, sc), new Vector2(-sc, -sc), new Vector2(sc, -sc) })
        {
            Circle(go.transform, "Screw", new Color(0.42f, 0.38f, 0.30f), p, size * 0.048f);
            Img(go.transform, "ScrewSlot", new Color(0.18f, 0.16f, 0.13f), p, new Vector2(size * 0.034f, size * 0.007f));
        }

        Graphic glowLamp = Circle(go.transform, "OverloadLamp", new Color(1f, 0.35f, 0.25f, 0.95f),
            new Vector2(0f, R * 0.60f), size * 0.048f);
        glowLamp.gameObject.SetActive(false);

        // ---- 컴포넌트 ----
        AnalogGauge gauge = go.AddComponent<AnalogGauge>();
        SerializedObject so = new SerializedObject(gauge);
        so.FindProperty("needle").objectReferenceValue = needle;
        so.FindProperty("minAngle").floatValue = MinAngle;
        so.FindProperty("maxAngle").floatValue = MaxAngle;
        so.FindProperty("needleGraphic").objectReferenceValue = needleImg;
        so.FindProperty("needleNormal").colorValue = Ink;
        so.FindProperty("needleDanger").colorValue = Danger;
        so.FindProperty("dangerArc").objectReferenceValue = dangerArc;
        so.FindProperty("readout").objectReferenceValue = readout;
        so.FindProperty("readoutSuffix").stringValue = " " + unit;
        so.FindProperty("readoutScale").floatValue = maxReading;
        so.FindProperty("glowLamp").objectReferenceValue = glowLamp;
        so.ApplyModifiedPropertiesWithoutUndo();

        return gauge;
    }

    /// <summary>계기를 올려놓을 낡은 금속 패널. 미니게임 화면의 배경으로 쓴다.</summary>
    public static GameObject MakePanel(Transform parent, string name, Vector2 pos, Vector2 size)
    {
        GameObject go = Empty(parent, name);
        SetBox((RectTransform)go.transform, pos, size);

        Img(go.transform, "PanelEdge", new Color(0.20f, 0.19f, 0.17f), Vector2.zero, size);
        Img(go.transform, "PanelFace", new Color(0.26f, 0.25f, 0.23f), Vector2.zero, size - Vector2.one * 8f);

        // 가로로 난 긁힘과 얼룩
        Img(go.transform, "Scuff0", new Color(0f, 0f, 0f, 0.10f), new Vector2(0f, size.y * 0.18f), new Vector2(size.x - 40f, 3f));
        Img(go.transform, "Scuff1", new Color(1f, 1f, 1f, 0.04f), new Vector2(0f, -size.y * 0.26f), new Vector2(size.x - 90f, 2f));
        Img(go.transform, "Grime", new Color(0.08f, 0.07f, 0.05f, 0.18f),
            new Vector2(0f, -size.y * 0.42f), new Vector2(size.x - 16f, size.y * 0.16f));

        // 네 귀퉁이 나사
        float mx = size.x * 0.5f - 22f, my = size.y * 0.5f - 22f;
        foreach (var p in new[] { new Vector2(-mx, my), new Vector2(mx, my), new Vector2(-mx, -my), new Vector2(mx, -my) })
        {
            Img(go.transform, "Screw", new Color(0.40f, 0.37f, 0.30f), p, Vector2.one * 16f);
            Img(go.transform, "ScrewSlot", new Color(0.15f, 0.14f, 0.12f), p, new Vector2(11f, 3f));
        }

        return go;
    }

    // ---------------------------------------------------------------- 유틸

    /// 중심에서 angle 도 방향으로, 반지름 inner 부터 outer 까지 걸치는 눈금을 놓는다.
    static void Tick(Transform parent, string name, float angle, float inner, float outer, float width, Color color)
    {
        float rad = (90f + angle) * Mathf.Deg2Rad;
        float mid = (inner + outer) * 0.5f;
        Image tick = Img(parent, name, color,
            new Vector2(Mathf.Cos(rad) * mid, Mathf.Sin(rad) * mid),
            new Vector2(width, outer - inner));
        tick.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    /// 동그란 판. UI Image 는 스프라이트가 없으면 사각형이라 내장 원형 스프라이트를 쓴다.
    public static Image Circle(Transform parent, string name, Color color, Vector2 pos, float diameter)
    {
        Image img = Img(parent, name, color, pos, Vector2.one * diameter);
        if (circleSprite == null)
            circleSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        img.sprite = circleSprite;
        img.type = Image.Type.Simple;
        return img;
    }

    static Sprite circleSprite;

    static GameObject Empty(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    public static Image Img(Transform parent, string name, Color color, Vector2 pos, Vector2 size)
    {
        GameObject go = Empty(parent, name);
        Image img = go.AddComponent<Image>();
        img.color = color;
        img.sprite = null;              // 단색 사각형. 스프라이트를 늘리면 모서리가 뭉개진다.
        img.raycastTarget = false;
        SetBox(img.rectTransform, pos, size);
        return img;
    }

    public static TMP_Text Label(Transform parent, string name, string text, float size, Vector2 pos, Color color)
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
        tmp.textWrappingMode = TextWrappingModes.NoWrap;   // "TORQUE WRENCH" 가 두 줄로 접히지 않게
        SetBox(tmp.rectTransform, pos, new Vector2(size * 14f, size * 1.6f));
        return tmp;
    }

    public static void SetBox(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }
}
