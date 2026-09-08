using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 배경음(BGM) 전담 매니저. 효과음은 SoundManager 가 맡는다.
///
/// SoundManager 와 나눈 이유는 하는 일이 다르기 때문이다. 효과음은 PlayOneShot 으로
/// 겹쳐 터뜨리는 일회성 소리고, 배경음은 한 곡이 계속 루프하면서 다른 곡으로 갈아타는
/// 소리다. 한 AudioSource 로 둘을 같이 하면 효과음이 배경음을 끊는다.
///
/// SoundManager 와 달리 DontDestroyOnLoad 를 걸지 않는다. 인게임에 들어가면 시작되고
/// 마지막 컷신 씬으로 넘어가면 씬과 함께 사라져야 하기 때문이다. 씬을 넘겨서까지
/// 이어져야 하는 음악이 생기면 그때 그 씬에도 이 프리팹을 놓으면 된다.
///
/// 전체 볼륨은 옵션의 사운드 슬라이더가 AudioListener.volume 으로 이미 잡고 있으므로
/// 여기서는 배경음끼리의 상대 볼륨만 다룬다.
/// </summary>
[DefaultExecutionOrder(-1000)]
public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    [Header("배경음 - 넣은 클립의 파일 이름이 그대로 PlayBGM 호출 키가 된다")]
    [SerializeField] private AudioClip[] tracks;

    [Header("씬이 시작될 때 재생할 곡 (비우면 재생하지 않는다)")]
    [SerializeField] private string playOnStart = "heart_beat_normal";

    [Header("배경음 볼륨 (전체 볼륨은 옵션 슬라이더가 담당)")]
    [Range(0f, 1f)]
    [SerializeField] private float volume = 0.55f;

    [Header("곡을 갈아탈 때 겹쳐 넘기는 시간(초)")]
    [SerializeField] private float crossfadeSeconds = 1.5f;

    /// 지금 재생 중인 곡 이름. 없으면 빈 문자열.
    public string CurrentTrack { get; private set; } = "";

    // 두 개를 번갈아 쓴다. 하나가 줄어드는 동안 다른 하나가 커져야 소리가 끊기지 않는다.
    private AudioSource _a;
    private AudioSource _b;
    private AudioSource _active;

    private readonly Dictionary<string, AudioClip> _byName = new Dictionary<string, AudioClip>();
    private Coroutine _fade;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _a = CreateSource("BGM_A");
        _b = CreateSource("BGM_B");
        _active = _a;

        BuildLookup();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        if (!string.IsNullOrEmpty(playOnStart)) PlayBGM(playOnStart);
    }

    private AudioSource CreateSource(string label)
    {
        // 프리팹에 AudioSource 두 개를 손으로 맞춰 넣는 것보다 여기서 만드는 게 안전하다.
        // loop / playOnAwake / 2D 설정을 빠뜨릴 여지가 없다. Awake 에 한 번만 돈다.
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;   // 2D - 플레이어 위치와 무관하게 같은 크기로 들린다
        source.volume = 0f;
        return source;
    }

    void BuildLookup()
    {
        if (tracks == null) return;

        int emptySlots = 0;

        for (int i = 0; i < tracks.Length; i++)
        {
            AudioClip clip = tracks[i];
            if (clip == null) { emptySlots++; continue; }

            if (_byName.ContainsKey(clip.name))
            {
                Debug.LogWarning($"[{nameof(MusicManager)}] 이름이 겹치는 곡 '{clip.name}' 은 등록하지 않는다.", this);
                continue;
            }

            _byName.Add(clip.name, clip);
        }

        if (emptySlots > 0)
            Debug.LogWarning($"[{nameof(MusicManager)}] tracks 에 빈 칸이 {emptySlots}개 있다. 등록된 곡은 {_byName.Count}개.", this);
    }

    /// <summary>
    /// 등록된 배경음을 이름으로 재생한다. 이름은 인스펙터에 넣은 오디오 파일 이름과 같다.
    /// 이미 같은 곡이 흐르고 있으면 아무 것도 하지 않는다 - 매 프레임 불러도 안전하다.
    /// </summary>
    public void PlayBGM(string trackName)
    {
        if (string.IsNullOrEmpty(trackName)) return;
        if (CurrentTrack == trackName && _active.isPlaying) return;

        if (!_byName.TryGetValue(trackName, out AudioClip clip))
        {
            Debug.LogWarning($"[{nameof(MusicManager)}] '{trackName}' 배경음이 등록되어 있지 않다.", this);
            return;
        }

        AudioSource next = _active == _a ? _b : _a;
        next.clip = clip;
        next.time = 0f;
        next.Play();

        CurrentTrack = trackName;
        StartFade(next, _active);
        _active = next;
    }

    /// <summary>배경음을 서서히 끈다. 마지막 컷신으로 넘어가기 전 등.</summary>
    public void StopBGM()
    {
        CurrentTrack = "";
        StartFade(null, _active);
    }

    /// <summary>배경음끼리의 상대 볼륨. 전체 볼륨은 옵션 슬라이더가 따로 잡는다.</summary>
    public void SetVolume(float value)
    {
        volume = Mathf.Clamp01(value);
        if (_active != null && _active.isPlaying) _active.volume = volume;
    }

    private void StartFade(AudioSource fadeIn, AudioSource fadeOut)
    {
        if (_fade != null) StopCoroutine(_fade);
        _fade = StartCoroutine(Crossfade(fadeIn, fadeOut));
    }

    private IEnumerator Crossfade(AudioSource fadeIn, AudioSource fadeOut)
    {
        float fromIn = fadeIn != null ? fadeIn.volume : 0f;
        float fromOut = fadeOut != null ? fadeOut.volume : 0f;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.01f, crossfadeSeconds);
            float e = Mathf.Clamp01(t);

            if (fadeIn != null) fadeIn.volume = Mathf.Lerp(fromIn, volume, e);
            if (fadeOut != null) fadeOut.volume = Mathf.Lerp(fromOut, 0f, e);

            yield return null;
        }

        if (fadeIn != null) fadeIn.volume = volume;
        if (fadeOut == null) { _fade = null; yield break; }

        fadeOut.volume = 0f;
        fadeOut.Stop();
        fadeOut.clip = null;
        _fade = null;
    }
}
