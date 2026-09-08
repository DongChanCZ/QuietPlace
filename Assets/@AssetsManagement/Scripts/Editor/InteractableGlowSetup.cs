using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 반짝이 시스템 세팅 도구.
///
///  1. 반짝이 텍스처/머티리얼을 만든다 (외부 애셋 없이 코드로 그린다).
///  2. InteractableGlowManager 프리팹을 만들고 씬에 놓는다.
///  3. 하이어라키에서 고른 오브젝트에 InteractableGlow 를 붙인다.
/// </summary>
public static class InteractableGlowSetup
{
    const string TexPath = "Assets/@Resources/Textures/T_InteractableSparkle.png";
    const string MatPath = "Assets/@Resources/Materials/M_InteractableSparkle.mat";
    const string PrefabPath = "Assets/@Resources/Prefabs/Manager/P_InteractableGlowManager.prefab";

    [MenuItem("Tools/QuietPlace/반짝이 - 1. 텍스처/머티리얼 만들기")]
    public static void CreateAssets()
    {
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(TexPath));
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(MatPath));

        // ---- 반짝이 그림: 가운데가 밝은 원 + 십자로 뻗는 빛살 ----
        const int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] px = new Color[size * size];
        float half = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - half) / half;
                float dy = (y + 0.5f - half) / half;
                float r = Mathf.Sqrt(dx * dx + dy * dy);

                // 중심 광점
                float core = Mathf.Clamp01(1f - r / 0.35f);
                core = core * core;

                // 십자 빛살 - 축에 가까울수록 길게 뻗는다
                float ax = Mathf.Clamp01(1f - Mathf.Abs(dy) / 0.07f) * Mathf.Clamp01(1f - Mathf.Abs(dx));
                float ay = Mathf.Clamp01(1f - Mathf.Abs(dx) / 0.07f) * Mathf.Clamp01(1f - Mathf.Abs(dy));
                float spike = Mathf.Max(ax, ay) * 0.55f;

                float a = Mathf.Clamp01(core + spike);
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }

        tex.SetPixels(px);
        tex.Apply();
        System.IO.File.WriteAllBytes(TexPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(TexPath, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(TexPath);
        ti.textureType = TextureImporterType.Default;
        ti.alphaSource = TextureImporterAlphaSource.FromInput;
        ti.alphaIsTransparency = true;
        ti.mipmapEnabled = true;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.SaveAndReimport();

        // ---- 가산 합성 Unlit 머티리얼 ----
        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
        if (unlit == null) { Debug.LogError("[InteractableGlowSetup] URP Unlit 셰이더를 못 찾았다."); return; }

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null) { mat = new Material(unlit); AssetDatabase.CreateAsset(mat, MatPath); }
        mat.shader = unlit;

        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TexPath));
        mat.SetColor("_BaseColor", Color.white);

        // 어두운 방에서 빛처럼 보이도록 가산 합성(Additive)으로 맞춘다
        mat.SetFloat("_Surface", 1f);                                   // Transparent
        mat.SetFloat("_Blend", 2f);                                     // Additive
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        mat.SetFloat("_AlphaClip", 0f);
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.DisableKeyword("_ALPHATEST_ON");

        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[InteractableGlowSetup] 만들었다\n  {TexPath} ({size}x{size})\n  {MatPath} (URP Unlit, 가산 합성)");
    }

    [MenuItem("Tools/QuietPlace/반짝이 - 2. 매니저 만들어 씬에 놓기")]
    public static void PlaceManager()
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null) { Debug.LogError("[InteractableGlowSetup] 먼저 1번(텍스처/머티리얼 만들기)을 실행할 것."); return; }

        foreach (InteractableGlowManager m in Object.FindObjectsByType<InteractableGlowManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Undo.DestroyObjectImmediate(m.gameObject);

        GameObject go = new GameObject("P_InteractableGlowManager");
        InteractableGlowManager mgr = go.AddComponent<InteractableGlowManager>();

        // 플레이어/카메라를 찾아 넣는다. 런타임 Find 가 아니라 에디터에서 한 번 굽는 것이라 괜찮다.
        PlayerController pc = Object.FindFirstObjectByType<PlayerController>();
        Transform playerT = pc != null ? pc.transform : null;
        Transform eyesT = pc != null && pc.playerCamera != null ? pc.playerCamera.transform : null;

        SerializedObject so = new SerializedObject(mgr);
        so.FindProperty("player").objectReferenceValue = playerT;
        so.FindProperty("eyes").objectReferenceValue = eyesT;
        so.FindProperty("sparkleMaterial").objectReferenceValue = mat;
        so.ApplyModifiedPropertiesWithoutUndo();

        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PrefabPath));
        PrefabUtility.SaveAsPrefabAssetAndConnect(go, PrefabPath, InteractionMode.UserAction);
        Undo.RegisterCreatedObjectUndo(go, "Place InteractableGlowManager");

        Debug.Log($"[InteractableGlowSetup] 매니저 배치\n  player = {(playerT == null ? "<못 찾음 - 직접 넣을 것>" : playerT.name)}" +
                  $"\n  eyes = {(eyesT == null ? "<없음>" : eyesT.name)}\n  프리팹 = {PrefabPath}");
    }

    [MenuItem("Tools/QuietPlace/반짝이 - 3. 고른 오브젝트에 반짝이 넣기")]
    public static void AddToSelection()
    {
        GameObject[] selection = Selection.gameObjects;
        if (selection == null || selection.Length == 0)
        {
            Debug.LogWarning("[InteractableGlowSetup] 하이어라키에서 오브젝트를 먼저 고를 것.");
            return;
        }

        int added = 0, already = 0, noRenderer = 0;
        var report = new List<string>();

        foreach (GameObject go in selection)
        {
            if (go.GetComponent<InteractableGlow>() != null) { already++; continue; }

            if (go.GetComponentInChildren<Renderer>(true) == null)
            {
                noRenderer++;
                report.Add("  렌더러 없음(건너뜀): " + go.name);
                continue;
            }

            Undo.AddComponent<InteractableGlow>(go);
            added++;
            report.Add("  추가: " + go.name);
        }

        Debug.Log($"[InteractableGlowSetup] 반짝이 {added}개 추가 / 이미 있음 {already}개 / 렌더러 없어 건너뜀 {noRenderer}개\n"
                  + string.Join("\n", report.ToArray()));
    }

    [MenuItem("Tools/QuietPlace/반짝이 - 4. 고른 오브젝트에서 반짝이 빼기")]
    public static void RemoveFromSelection()
    {
        int removed = 0;
        foreach (GameObject go in Selection.gameObjects)
        {
            InteractableGlow g = go.GetComponent<InteractableGlow>();
            if (g == null) continue;
            Undo.DestroyObjectImmediate(g);
            removed++;
        }
        Debug.Log($"[InteractableGlowSetup] 반짝이 {removed}개 제거");
    }
}
