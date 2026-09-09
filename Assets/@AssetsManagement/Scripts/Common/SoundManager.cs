using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-1000)] // 다른 스크립트가 Start에서 재생을 시도하기 전에 Instance와 조회표가 준비되어 있어야 함
[RequireComponent(typeof(AudioSource))]
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("효과음 - 여기에 넣은 클립의 파일 이름이 그대로 PlaySFX 호출 키가 된다")]
    [SerializeField] private AudioClip[] sfxClips;

    [Header("효과음 볼륨 (전체 볼륨은 GameSettings/AudioListener가 담당)")]
    [Range(0f, 1f)]
    [SerializeField] private float sfxVolume = 1f;

    // 이름 -> 클립. Awake에서 한 번만 만들어, 재생할 때마다 배열을 문자열 비교로 순회하지 않는다
    private readonly Dictionary<string, AudioClip> _sfxByName = new Dictionary<string, AudioClip>();
    private AudioSource _source;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject); // 타이틀 -> 인트로 -> 인게임 씬 전환 중에도 유지되어야 함

        _source = GetComponent<AudioSource>();
        _source.playOnAwake = false;

        BuildLookup();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void BuildLookup()
    {
        if (sfxClips == null) return;

        int emptySlots = 0;

        for (int i = 0; i < sfxClips.Length; i++)
        {
            AudioClip clip = sfxClips[i];

            // 배열 칸을 늘려두고 아직 안 채운 건 흔한 작업 중간 상태다.
            // 칸마다 경고를 찍으면 콘솔이 도배되므로 개수만 세서 한 번에 알린다.
            if (clip == null)
            {
                emptySlots++;
                continue;
            }

            if (_sfxByName.ContainsKey(clip.name))
            {
                Debug.LogWarning($"[{nameof(SoundManager)}] 이름이 겹치는 클립 '{clip.name}' 은 등록하지 않는다.", this);
                continue;
            }

            _sfxByName.Add(clip.name, clip);
        }

        if (emptySlots > 0)
            Debug.LogWarning($"[{nameof(SoundManager)}] sfxClips 에 빈 칸이 {emptySlots}개 있다. 등록된 효과음은 {_sfxByName.Count}개.", this);
    }

    /// <summary>
    /// 등록된 효과음을 이름으로 재생한다. 이름은 인스펙터에 넣은 오디오 파일 이름과 같다.
    /// PlayOneShot 이라 이전 소리를 끊지 않고 겹쳐서 난다.
    /// </summary>
    public void PlaySFX(string soundName)
    {
        AudioClip clip;
        if (!_sfxByName.TryGetValue(soundName, out clip))
        {
            // 인스펙터 목록에서 빠진 것은 사운드 목록(AudioLibrary)에서 이름으로 찾는다.
            // 병합할 때마다 인스펙터 참조가 사라져도 소리가 안 나는 일은 없어야 한다.
            clip = AudioLibrary.Get(soundName);

            if (clip == null)
            {
                Debug.LogWarning($"[{nameof(SoundManager)}] '{soundName}' 효과음을 인스펙터에서도, 사운드 목록에서도 못 찾았다.", this);
                return;
            }

            // 한 번 찾았으면 캐시한다 - 매번 목록을 다시 뒤지지 않는다.
            _sfxByName[soundName] = clip;
            Debug.Log($"[{nameof(SoundManager)}] '{soundName}' 은 인스펙터에 없어 사운드 목록에서 가져왔다.", this);
        }

        _source.PlayOneShot(clip, sfxVolume);
    }
}
