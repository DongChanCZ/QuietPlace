using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Audio 폴더의 모든 사운드를 이름으로 찾을 수 있게 모아둔 목록.
///
/// 왜 필요한가. 인스펙터에 손으로 꽂은 참조는 씬/프리팹 파일 안에 파일ID로 저장되는데,
/// 여러 사람이 같은 씬을 고치고 병합하면 그 줄이 그대로 날아간다. 그러면 소리가
/// 조용히 사라지고, 왜 안 나는지 알기도 어렵다.
///
/// 그래서 클립을 코드에서 "이름" 으로 찾게 한다. 이름은 .cs 파일 안의 문자열이라
/// 병합할 때 텍스트로 합쳐지고 사라지지 않는다. 이 목록 자체도 Resources 폴더에 두어
/// 어떤 씬 참조도 거치지 않고 Resources.Load 로 불러온다 - 병합으로 잃을 참조가 없다.
///
/// 목록을 채우는 것은 에디터 메뉴(Tools/QuietPlace/사운드 목록 갱신)가 한다.
/// </summary>
[CreateAssetMenu(fileName = "AudioLibrary", menuName = "QuietPlace/Audio Library")]
public class AudioLibrary : ScriptableObject
{
    /// Resources 안에서의 경로(확장자 없이). 에디터 스크립트도 이 값을 쓴다.
    public const string ResourcePath = "AudioLibrary";

    [Header("Audio 폴더에서 모아온 클립들 - 에디터 메뉴로 갱신한다")]
    [SerializeField] private AudioClip[] clips;

    private static AudioLibrary _instance;
    private Dictionary<string, AudioClip> _byName;

    /// <summary>목록을 불러온다. 없으면 null - 부르는 쪽에서 조용히 넘어가야 한다.</summary>
    public static AudioLibrary Instance
    {
        get
        {
            if (_instance != null) return _instance;

            _instance = Resources.Load<AudioLibrary>(ResourcePath);
            if (_instance == null)
                Debug.LogWarning($"[{nameof(AudioLibrary)}] Resources/{ResourcePath} 를 못 찾았다. " +
                                 "Tools/QuietPlace/사운드 목록 갱신 을 한 번 실행할 것.");

            return _instance;
        }
    }

    /// <summary>이름으로 클립을 찾는다. 없으면 null.</summary>
    public static AudioClip Get(string clipName)
    {
        if (string.IsNullOrEmpty(clipName)) return null;

        AudioLibrary lib = Instance;
        if (lib == null) return null;

        return lib.Find(clipName);
    }

    /// <summary>
    /// 인스펙터에 꽂힌 것이 있으면 그것을, 없으면 이름으로 찾은 것을 쓴다.
    /// 병합으로 참조가 빠져도 소리가 계속 나게 하는 통로다.
    /// </summary>
    public static AudioClip Resolve(AudioClip assigned, string fallbackName)
    {
        if (assigned != null) return assigned;
        return Get(fallbackName);
    }

    /// <summary>
    /// 인스펙터에 꽂힌 AudioSource 가 있으면 그것을, 없으면 새로 만들어 돌려준다.
    ///
    /// 클립과 마찬가지로 AudioSource 참조도 씬 파일에 저장되므로 병합 때 빠질 수 있다.
    /// 빠졌으면 조용해지는 대신 여기서 같은 설정으로 만들어 쓴다.
    /// spatialBlend 0 이면 2D(화면 연출), 1에 가까우면 3D(그 자리에서 나는 소리).
    /// </summary>
    public static AudioSource EnsureSource(AudioSource assigned, Transform parent, string name,
        float spatialBlend, float minDistance, float maxDistance, float volume)
    {
        if (assigned != null) return assigned;

        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        AudioSource src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = false;
        src.volume = volume;
        src.spatialBlend = spatialBlend;

        if (spatialBlend > 0f)
        {
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = minDistance;
            src.maxDistance = maxDistance;
        }

        return src;
    }

    private AudioClip Find(string clipName)
    {
        if (_byName == null)
        {
            _byName = new Dictionary<string, AudioClip>(clips != null ? clips.Length : 0);

            if (clips != null)
            {
                for (int i = 0; i < clips.Length; i++)
                {
                    AudioClip c = clips[i];
                    if (c == null || _byName.ContainsKey(c.name)) continue;
                    _byName.Add(c.name, c);
                }
            }
        }

        AudioClip found;
        if (_byName.TryGetValue(clipName, out found)) return found;

        Debug.LogWarning($"[{nameof(AudioLibrary)}] '{clipName}' 클립이 목록에 없다. " +
                         "Audio 폴더에 파일을 넣고 Tools/QuietPlace/사운드 목록 갱신 을 실행할 것.");
        return null;
    }

#if UNITY_EDITOR
    /// 에디터 스크립트가 목록을 채울 때만 쓴다.
    public void EditorSetClips(AudioClip[] value)
    {
        clips = value;
        _byName = null;
    }

    public int EditorCount => clips != null ? clips.Length : 0;
#endif
}
