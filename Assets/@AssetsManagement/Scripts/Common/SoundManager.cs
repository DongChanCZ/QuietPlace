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

        for (int i = 0; i < sfxClips.Length; i++)
        {
            AudioClip clip = sfxClips[i];

            if (clip == null)
            {
                Debug.LogWarning($"[{nameof(SoundManager)}] sfxClips[{i}] 칸이 비어 있다.", this);
                continue;
            }

            if (_sfxByName.ContainsKey(clip.name))
            {
                Debug.LogWarning($"[{nameof(SoundManager)}] 이름이 겹치는 클립 '{clip.name}' 은 등록하지 않는다.", this);
                continue;
            }

            _sfxByName.Add(clip.name, clip);
        }
    }

    /// <summary>
    /// 등록된 효과음을 이름으로 재생한다. 이름은 인스펙터에 넣은 오디오 파일 이름과 같다.
    /// PlayOneShot 이라 이전 소리를 끊지 않고 겹쳐서 난다.
    /// </summary>
    public void PlaySFX(string soundName)
    {
        if (!_sfxByName.TryGetValue(soundName, out AudioClip clip))
        {
            Debug.LogWarning($"[{nameof(SoundManager)}] '{soundName}' 효과음이 등록되어 있지 않다.", this);
            return;
        }

        _source.PlayOneShot(clip, sfxVolume);
    }
}
