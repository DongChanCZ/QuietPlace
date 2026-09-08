using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;

/// 잔류전하 방전. 데바데식 스킬체크지만 R -> S -> T 순서로 상(相)마다 다른 키를 눌러야 하고,
/// 성공할수록 바늘이 빨라진다. 아무 키나 연타로 넘길 수 없게 하려고 Space 하나가 아니라
/// 지정된 키만 받는다 - 틀린 키를 누르면 실패로 처리하고 구간을 다시 뽑는다.
public class SkillCheckMinigame : MonoBehaviour
{
    [Header("회전 바늘")]
    public RectTransform needle;
    public RectTransform hitZoneVisual;
    public float rotationSpeed = 330f;
    public float hitZoneWidth = 26f;

    [Header("성공할 때마다 이만큼 빨라진다")]
    public float speedRampPerHit = 70f;

    [Header("눌러야 하는 키 순서 (R -> S -> T)")]
    public Key[] keySequence = { Key.R, Key.S, Key.T };

    [Header("지금 눌러야 하는 키를 보여주는 표시")]
    public TMP_Text currentKeyLabel;

    [Header("2단계 안내 문구 (난이도에 따라 바뀐다)")]
    public TMP_Text hintLabel;

    [Header("사운드")]
    public AudioSource audioSource;
    public AudioClip hitSound;
    public AudioClip missSound; // 실패 시 2단계 경보음

    public UnityEvent onAllHits;

    private float currentAngle;
    private float hitZoneCenter;
    private int hitCount;
    private bool completed;
    private float baseSpeed;

    void OnEnable()
    {
        // 보통은 스페이스바 2번, 어려움은 R/S/T 3번.
        keySequence = MinigameTuning.DischargeKeys;
        rotationSpeed = MinigameTuning.DischargeSpeed;
        hitZoneWidth = MinigameTuning.DischargeZoneWidth;

        hitCount = 0;
        completed = false;
        currentAngle = 0f;
        baseSpeed = rotationSpeed;
        RandomizeHitZone();
        RefreshKeyLabel();
        RefreshHint();
    }

    void OnDisable()
    {
        // 램프로 올려둔 속도를 원래대로 되돌린다. 재시도할 때마다 누적되면 안 된다.
        if (baseSpeed > 0f) rotationSpeed = baseSpeed;
    }

    void Update()
    {
        if (completed) return;

        currentAngle = (currentAngle + rotationSpeed * Time.deltaTime) % 360f;
        if (needle != null) needle.localRotation = Quaternion.Euler(0f, 0f, -currentAngle);
        if (hitZoneVisual != null) hitZoneVisual.localRotation = Quaternion.Euler(0f, 0f, -hitZoneCenter);

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        Key expected = ExpectedKey();

        // 지정된 키를 눌렀으면 판정, 그 외 R/S/T 중 다른 키를 눌렀으면 바로 실패.
        if (keyboard[expected].wasPressedThisFrame)
        {
            TryHit();
            return;
        }

        // 보통 난이도는 같은 키(Space)가 두 번이라 "다른 키" 검사가 자기 자신을 잡는다.
        // 순서에 서로 다른 키가 섞여 있을 때만 오타 판정을 한다.
        for (int i = 0; i < keySequence.Length; i++)
        {
            if (keySequence[i] == expected) continue;
            if (keyboard[keySequence[i]].wasPressedThisFrame) { Miss(); return; }
        }
    }

    /// 2단계 안내 문구를 난이도에 맞춘다.
    void RefreshHint()
    {
        if (hintLabel == null) return;

        bool multiKey = false;
        for (int i = 1; i < keySequence.Length; i++)
            if (keySequence[i] != keySequence[0]) { multiKey = true; break; }

        hintLabel.text = multiKey
            ? "바늘이 밝은 구간에 올 때 해당 상(相) 키 · R → S → T"
            : "바늘이 밝은 구간에 올 때 Space · " + RequiredHits() + "회";
    }

    Key ExpectedKey()
    {
        if (keySequence == null || keySequence.Length == 0) return Key.Space;
        return keySequence[Mathf.Clamp(hitCount, 0, keySequence.Length - 1)];
    }

    int RequiredHits()
    {
        return keySequence != null && keySequence.Length > 0 ? keySequence.Length : 3;
    }

    void TryHit()
    {
        float diff = Mathf.Abs(Mathf.DeltaAngle(currentAngle, hitZoneCenter));

        if (diff > hitZoneWidth * 0.5f) { Miss(); return; }

        hitCount++;
        PlaySound(hitSound);

        if (hitCount >= RequiredHits())
        {
            completed = true;
            if (currentKeyLabel != null) currentKeyLabel.text = "방전 완료";
            onAllHits?.Invoke();
            return;
        }

        rotationSpeed += speedRampPerHit;
        RandomizeHitZone();
        RefreshKeyLabel();
    }

    // 실패해도 처음으로 되돌리지는 않는다. 구간만 다시 뽑아서 다시 찾게 만든다.
    void Miss()
    {
        PlaySound(missSound);
        RandomizeHitZone();
    }

    void RandomizeHitZone()
    {
        hitZoneCenter = Random.Range(0f, 360f);
    }

    void RefreshKeyLabel()
    {
        if (currentKeyLabel == null) return;

        // R/S/T 는 3상을 뜻하지만 Space 는 그냥 키다. 문구를 나눈다.
        UnityEngine.InputSystem.Key k = ExpectedKey();
        string what = k == UnityEngine.InputSystem.Key.Space
            ? "Space 로 방전"
            : k.ToString() + " 상 방전";

        currentKeyLabel.text = what + "  (" + hitCount + "/" + RequiredHits() + ")";
    }

    void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
    }
}
