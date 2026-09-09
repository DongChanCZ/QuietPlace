using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Audio 폴더의 모든 사운드를 훑어 AudioLibrary 자산에 담는다.
///
/// 자산은 Resources 폴더에 만든다. 그래야 런타임에 Resources.Load 로 불러올 수 있고,
/// 씬이나 프리팹에 참조를 남기지 않는다 - 병합으로 잃을 것이 없다.
/// 새 사운드를 추가한 뒤 이 메뉴를 한 번 실행하면 된다.
/// </summary>
public static class AudioLibraryBuilder
{
    const string AudioDir = "Assets/MiniGame_QuietPlace/Audio";
    const string ResourcesDir = "Assets/Resources";
    const string AssetPath = ResourcesDir + "/" + AudioLibrary.ResourcePath + ".asset";

    [MenuItem("Tools/QuietPlace/사운드 목록 갱신")]
    public static void Rebuild()
    {
        if (!AssetDatabase.IsValidFolder(ResourcesDir))
        {
            // "Resources" 라는 이름이어야 Resources.Load 가 찾는다. @Resources 는 안 된다.
            AssetDatabase.CreateFolder("Assets", "Resources");
            Debug.Log("[AudioLibraryBuilder] Assets/Resources 폴더를 만들었다.");
        }

        // 하위 폴더(Keystrokes 등)까지 훑는다.
        var clips = new List<AudioClip>();
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioDir }))
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
            if (clip != null) clips.Add(clip);
        }

        // 이름 순으로 정렬해 둔다. 순서가 흔들리면 자산 파일이 매번 달라져 병합이 지저분해진다.
        clips.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

        // 이름이 겹치면 이름 조회가 어느 쪽인지 알 수 없다. 미리 알려준다.
        var seen = new HashSet<string>();
        var dupes = new List<string>();
        foreach (AudioClip c in clips)
            if (!seen.Add(c.name)) dupes.Add(c.name);

        AudioLibrary lib = AssetDatabase.LoadAssetAtPath<AudioLibrary>(AssetPath);
        bool created = false;
        if (lib == null)
        {
            lib = ScriptableObject.CreateInstance<AudioLibrary>();
            AssetDatabase.CreateAsset(lib, AssetPath);
            created = true;
        }

        lib.EditorSetClips(clips.ToArray());
        EditorUtility.SetDirty(lib);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var names = new List<string>(clips.Count);
        foreach (AudioClip c in clips) names.Add(c.name);

        Debug.Log($"[AudioLibraryBuilder] 사운드 {clips.Count}개 등록 " +
                  (created ? "(자산 새로 만듦) " : "") + "-> " + AssetPath + "\n" +
                  "  " + string.Join(", ", names.ToArray()) +
                  (dupes.Count > 0 ? "\n  ★ 이름이 겹치는 클립: " + string.Join(", ", dupes.ToArray()) : ""),
                  lib);
    }

    [MenuItem("Tools/QuietPlace/사운드 목록 확인")]
    public static void Report()
    {
        AudioLibrary lib = AssetDatabase.LoadAssetAtPath<AudioLibrary>(AssetPath);
        if (lib == null)
        {
            Debug.LogWarning("[AudioLibraryBuilder] 목록 자산이 없다. '사운드 목록 갱신' 을 먼저 실행할 것.");
            return;
        }

        // 폴더에는 있는데 목록에 없는 것 = 갱신을 안 한 것
        var missing = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = Path.GetFileNameWithoutExtension(path);
            if (AudioLibrary.Get(name) == null) missing.Add(name);
        }

        Debug.Log($"[AudioLibraryBuilder] 목록에 {lib.EditorCount}개 등록됨" +
                  (missing.Count > 0
                      ? "\n  ★ 폴더에 있는데 목록에 없는 것: " + string.Join(", ", missing.ToArray()) + " -> 갱신 필요"
                      : "\n  폴더의 모든 사운드가 등록되어 있다"), lib);
    }
}
