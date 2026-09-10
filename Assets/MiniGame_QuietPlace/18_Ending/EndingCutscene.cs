using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 엔딩 컷신. 송출이 끝난 순간부터 암전까지 7개 컷을 순서대로 돌린다.
///
///   1구간 - 송출 성공과 괴물 무력화
///     컷1  송출 게이지 99 -> 100 [COMPLETE] 를 콘솔 모니터에 타이트하게
///     컷2  확성기 익스트림 클로즈업 + 고주파
///     컷3  방을 부수던 괴물이 고주파에 비명(Shouting) -> 카메라 흔들림 -> 도망
///   2구간 - USB 다운로드와 진실
///     컷4  숄더샷. 모니터에 [프로젝트: 데스 엔젤] 문서가 한 줄씩 뜨고 얼굴에 파란 반사광
///   3구간 - 탈출과 반전
///     컷5  외부. 완강기로 벽을 내려와 착지, 건물을 돌아본다
///     컷6  롱테이크. 플레이어를 떠나 외벽을 타고 내려가 지하 1층 안으로 스며든다
///     컷7  깨진 알과 벽에 드리우는 박쥐 그림자 -> 암전
///
/// 카메라는 플레이어 카메라를 쓰지 않는다. 6번 컷이 플레이어를 떠나 벽을 통과해
/// 지하로 내려가야 하므로 전용 카메라가 필요하다. 플레이어 카메라는 컷신 동안 꺼둔다.
///
/// 오디오 리스너는 옮기지 않는다. 리스너가 둘이면 경고가 나고, 이 컷신의 소리는
/// 전부 2D 라 위치와 무관하다.
/// </summary>
[DefaultExecutionOrder(-900)]
public class EndingCutscene : MonoBehaviour
{
    public static EndingCutscene Instance { get; private set; }

    [Header("플레이어")]
    [SerializeField] private PlayerController player;
    [SerializeField] private PlayerInteractor interactor;
    [Tooltip("플레이어 모델을 감출지. 연기 애니메이션이 없을 때만 켠다.")]
    [SerializeField] private bool hidePlayerModel = false;

    [Header("플레이어가 연기할 자리")]
    [Tooltip("컷4 - 콘솔 앞에서 타이핑")]
    [SerializeField] private Transform poseTyping;
    [Tooltip("컷5 - 창문 밖 로프 시작 지점")]
    [SerializeField] private Transform poseRopeTop;
    [Tooltip("컷5 - 착지 지점 (건물을 마주본다)")]
    [SerializeField] private Transform poseLanded;
    [Tooltip("컷5 - 돌아서 걸어간 끝 지점")]
    [SerializeField] private Transform poseWalkEnd;

    [Header("연기 구간 길이(초)")]
    [SerializeField] private float landSeconds = 1.4f;
    [SerializeField] private float turnSeconds = 1.5f;
    [SerializeField] private float walkSeconds = 2.0f;

    [Header("컷신 동안 가릴 게임 HUD")]
    [Tooltip("공포 수치, 산소, 조준점 같은 인게임 표시. 컷신에 남아 있으면 연출이 깨진다.")]
    [SerializeField] private GameObject[] hudToHide;

    [Header("컷신 전용 카메라")]
    [SerializeField] private Camera cutsceneCamera;

    [Header("암전용 검은 화면")]
    [SerializeField] private CanvasGroup fadeGroup;
    [SerializeField] private TMP_Text endTitle;

    [Header("컷1 - 콘솔 모니터")]
    [SerializeField] private Transform shot1;
    [SerializeField] private float shot1Seconds = 2.6f;
    [Tooltip("송출 HUD - 100% [COMPLETE] 를 보여주려고 잠깐 다시 켠다")]
    [SerializeField] private GameObject broadcastHud;
    [SerializeField] private UnityEngine.UI.Image broadcastFill;
    [SerializeField] private TMP_Text broadcastLabel;

    [Header("컷2 - 확성기")]
    [SerializeField] private Transform shot2;
    [SerializeField] private float shot2Seconds = 2.2f;
    [Tooltip("고주파가 퍼지는 파동 이펙트 (없으면 생략)")]
    [SerializeField] private ParticleSystem waveEffect;

    [Header("컷3 - 괴물")]
    [SerializeField] private Transform shot3Wide;
    [SerializeField] private Transform shot3Close;
    [SerializeField] private MonsterController[] monsters;
    [SerializeField] private float shot3WideSeconds = 2.0f;
    [SerializeField] private float shot3CloseSeconds = 2.4f;
    [Tooltip("괴물이 도망가는 시간(초)과 거리(m)")]
    [SerializeField] private float fleeSeconds = 1.6f;
    [SerializeField] private float fleeDistance = 9f;
    [Tooltip("카메라 흔들림 세기(m)")]
    [SerializeField] private float shakeAmount = 0.06f;

    [Header("컷4 - USB / 문서")]
    [SerializeField] private Transform shot4;
    [SerializeField] private EndingDocumentScreen documentScreen;
    [SerializeField] private float documentSeconds = 7.5f;

    [Header("컷5 - 완강기 하강")]
    [SerializeField] private Transform shot5Top;
    [SerializeField] private Transform shot5Bottom;
    [SerializeField] private float rappelSeconds = 4.2f;
    [Tooltip("착지 후 건물을 돌아보며 숨을 고르는 시간(초)")]
    [SerializeField] private float shot5HoldSeconds = 2.2f;
    [Tooltip("내려가는 동안 보여줄 더미(로프에 매달린 플레이어). 없으면 생략")]
    [SerializeField] private GameObject rappelDummy;

    [Header("컷6 - 벽을 타고 지하로")]
    [SerializeField] private Transform shot6Start;
    [SerializeField] private Transform shot6Mid;
    [SerializeField] private Transform shot6End;
    [SerializeField] private float shot6Seconds = 5.5f;

    [Header("컷7 - 알과 그림자")]
    [SerializeField] private Transform shot7;
    [SerializeField] private BatShadowReveal reveal;
    [SerializeField] private float shot7HoldSeconds = 3.4f;
    [SerializeField] private float blackoutSeconds = 2.2f;
    [SerializeField] private string endTitleText = "TO BE CONTINUED";

    [Header("고주파 소리")]
    [Tooltip("비우면 코드에서 만들어 쓴다(사인 스윕). 파일을 넣으면 그것을 쓴다.")]
    [SerializeField] private AudioClip highFreqClip;
    [SerializeField] private AudioSource highFreqSource;
    [SerializeField] private float highFreqVolume = 0.5f;

    public bool IsPlaying { get; private set; }

    private Camera _playerCamera;
    private Renderer[] _playerRenderers;
    private float _shake;

    // 클립 이름은 코드에 둔다 - 병합으로 사라지지 않는다.
    // high_freq 파일을 Audio 폴더에 넣고 사운드 목록을 갱신하면 만든 소리 대신 그것을 쓴다.
    const string HighFreqName = "high_freq";

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        highFreqClip = AudioLibrary.Resolve(highFreqClip, HighFreqName);
        if (highFreqClip == null) highFreqClip = MakeHighFreqTone();

        highFreqSource = AudioLibrary.EnsureSource(highFreqSource, transform, "HighFreqSource", 0f, 0f, 0f, highFreqVolume);

        if (cutsceneCamera != null) cutsceneCamera.enabled = false;
        if (rappelDummy != null) rappelDummy.SetActive(false);
        if (fadeGroup != null)
        {
            fadeGroup.alpha = 0f;
            fadeGroup.gameObject.SetActive(false);
        }
        if (endTitle != null) endTitle.text = "";
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>송출이 끝난 직후 FrequencyBroadcast 가 부른다.</summary>
    public void Begin(IInteractor who)
    {
        if (IsPlaying) return;
        if (player == null || cutsceneCamera == null)
        {
            Debug.LogError($"[{nameof(EndingCutscene)}] 참조가 비어 있다. " +
                           $"player={player != null} cutsceneCamera={cutsceneCamera != null} - " +
                           "Tools/QuietPlace/엔딩 컷신 만들기 를 실행할 것.", this);
            return;
        }

        IsPlaying = true;
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        TakeControl();

        yield return Section1();
        yield return Section2();
        yield return Section3();

        Debug.Log("[EndingCutscene] 엔딩 컷신 끝");
    }

    // ------------------------------------------------ 1구간: 송출 성공과 무력화

    private IEnumerator Section1()
    {
        // 컷1 - 게이지가 100% 로 넘어가는 순간
        Place(shot1);
        ShowBroadcastComplete();
        yield return new WaitForSeconds(shot1Seconds);
        if (broadcastHud != null) broadcastHud.SetActive(false);

        // 컷2 - 확성기. 여기서 고주파가 터진다
        Place(shot2);
        PlayHighFreq();
        if (waveEffect != null) waveEffect.Play();
        yield return new WaitForSeconds(shot2Seconds);

        // 컷3 - 괴물이 고주파를 맞는다
        Place(shot3Wide);
        NeutralizeMonsters();
        _shake = shakeAmount;
        yield return new WaitForSeconds(shot3WideSeconds);

        Place(shot3Close);
        yield return new WaitForSeconds(shot3CloseSeconds);

        _shake = 0f;
        yield return FleeMonsters();
    }

    private void ShowBroadcastComplete()
    {
        if (broadcastHud != null) broadcastHud.SetActive(true);
        if (broadcastFill != null) broadcastFill.fillAmount = 1f;
        if (broadcastLabel != null) broadcastLabel.text = "송출  100%   [COMPLETE]";
    }

    private void NeutralizeMonsters()
    {
        if (monsters == null) return;

        foreach (MonsterController m in monsters)
        {
            if (m == null || !m.gameObject.activeInHierarchy) continue;
            m.Neutralize();   // Agent 를 끄고 Shouting(비명) 자세로 들어간다
        }
    }

    /// 비명이 끝나면 도망친다. NavMeshAgent 는 무력화 상태에서 꺼져 있으므로 직접 밀어낸다.
    private IEnumerator FleeMonsters()
    {
        if (monsters == null || fleeSeconds <= 0f) yield break;

        var from = new Vector3[monsters.Length];
        var to = new Vector3[monsters.Length];

        for (int i = 0; i < monsters.Length; i++)
        {
            if (monsters[i] == null) continue;
            monsters[i].SetAnimState("Chasing");   // 달리는 자세

            Transform t = monsters[i].transform;
            from[i] = t.position;

            // 카메라를 등지고 달아나게 한다. 화면 밖으로 나가는 방향이라야 도망으로 읽힌다.
            Vector3 away = t.position - cutsceneCamera.transform.position;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = t.forward;
            away.Normalize();

            to[i] = from[i] + away * fleeDistance;
            t.rotation = Quaternion.LookRotation(away, Vector3.up);
        }

        float e = 0f;
        while (e < fleeSeconds)
        {
            e += Time.deltaTime;
            float k = Smooth(e / fleeSeconds);
            for (int i = 0; i < monsters.Length; i++)
                if (monsters[i] != null) monsters[i].transform.position = Vector3.Lerp(from[i], to[i], k);
            yield return null;
        }

        // 프리팹은 지우지 않는다 - 끄기만 한다.
        for (int i = 0; i < monsters.Length; i++)
            if (monsters[i] != null) monsters[i].gameObject.SetActive(false);
    }

    // ------------------------------------------------ 2구간: USB 와 진실

    private IEnumerator Section2()
    {
        Place(shot4);
        PlacePlayer(poseTyping, "EndTyping");

        if (documentScreen != null)
        {
            yield return documentScreen.Play(documentSeconds);
            yield return new WaitForSeconds(0.8f);
            yield return documentScreen.Close();
        }
        else
        {
            yield return new WaitForSeconds(documentSeconds);
        }
    }

    // ------------------------------------------------ 3구간: 탈출과 반전

    private IEnumerator Section3()
    {
        // 컷5 - 완강기로 내려온다. 카메라는 외부에서 벽을 함께 훑어 내리고,
        // 플레이어는 로프를 타는 동작으로 같은 속도로 내려온다.
        if (rappelDummy != null) rappelDummy.SetActive(true);

        PlacePlayer(poseRopeTop, "EndRope");
        StartCoroutine(MovePlayer(poseRopeTop, poseLanded, rappelSeconds));
        yield return MoveCamera(shot5Top, shot5Bottom, rappelSeconds);

        if (rappelDummy != null) rappelDummy.SetActive(false);

        // 착지 -> 숨을 고르고 -> 돌아서 걸어간 뒤 -> 건물을 다시 돌아본다
        PlacePlayer(poseLanded, "EndLand");
        yield return new WaitForSeconds(landSeconds);

        SetPlayerAnim("EndTurn");
        yield return new WaitForSeconds(turnSeconds);
        FacePlayer(poseWalkEnd);

        StartCoroutine(MovePlayer(poseLanded, poseWalkEnd, walkSeconds));
        SetPlayerAnim("EndWalk");
        yield return new WaitForSeconds(walkSeconds);

        SetPlayerAnim("EndTurn");
        yield return new WaitForSeconds(turnSeconds);
        FacePlayer(poseLanded);          // 건물 쪽으로 다시 돈다
        SetPlayerAnim("Idle");

        if (shot5HoldSeconds > 0f) yield return new WaitForSeconds(shot5HoldSeconds);

        // 컷6 - 플레이어를 떠나 외벽을 타고 지하로. 한 번도 끊지 않는다.
        yield return MoveCamera(shot6Start, shot6Mid, shot6Seconds * 0.55f);
        yield return MoveCamera(shot6Mid, shot6End, shot6Seconds * 0.45f);

        // 컷7 - 알과 그림자
        Place(shot7);
        if (reveal != null) yield return reveal.Play();
        if (shot7HoldSeconds > 0f) yield return new WaitForSeconds(shot7HoldSeconds);

        yield return Blackout();
    }

    private IEnumerator Blackout()
    {
        if (fadeGroup == null) yield break;

        fadeGroup.gameObject.SetActive(true);

        float t = 0f;
        while (t < blackoutSeconds)
        {
            t += Time.deltaTime;
            fadeGroup.alpha = Mathf.Clamp01(t / blackoutSeconds);
            yield return null;
        }
        fadeGroup.alpha = 1f;

        if (reveal != null) reveal.StopSounds();

        if (endTitle != null)
        {
            endTitle.text = endTitleText;
            yield return new WaitForSeconds(2.4f);
        }
    }

    // ------------------------------------------------ 플레이어 연기

    /// 플레이어를 자리에 놓고 동작을 시작한다. CharacterController 는 꺼져 있어
    /// transform 을 직접 옮겨도 되밀리지 않는다.
    private void PlacePlayer(Transform at, string animState)
    {
        if (at != null) player.transform.SetPositionAndRotation(at.position, at.rotation);
        SetPlayerAnim(animState);
    }

    private void SetPlayerAnim(string animState)
    {
        if (!string.IsNullOrEmpty(animState)) player.SetAnimState(animState);
    }

    /// 바라보는 방향만 목표 쪽으로 돌린다(수평만).
    private void FacePlayer(Transform target)
    {
        if (target == null) return;

        Vector3 flat = target.position - player.transform.position;
        flat.y = 0f;
        if (flat.sqrMagnitude < 0.0001f) return;

        player.transform.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
    }

    private IEnumerator MovePlayer(Transform a, Transform b, float seconds)
    {
        if (a == null || b == null) yield break;

        Vector3 p0 = a.position, p1 = b.position;
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            player.transform.position = Vector3.Lerp(p0, p1, Mathf.Clamp01(t / Mathf.Max(0.01f, seconds)));
            yield return null;
        }
        player.transform.position = p1;
    }

    // ------------------------------------------------ 카메라

    /// 카메라를 앵커 자리에 그대로 놓는다.
    private void Place(Transform anchor)
    {
        if (anchor == null || cutsceneCamera == null) return;
        cutsceneCamera.transform.SetPositionAndRotation(anchor.position, anchor.rotation);
    }

    /// 두 앵커 사이를 부드럽게 옮긴다. 컷6 처럼 벽을 통과해도 상관없다 - 카메라는 물리와 무관하다.
    private IEnumerator MoveCamera(Transform a, Transform b, float seconds)
    {
        if (cutsceneCamera == null) yield break;
        if (a == null || b == null) { Place(a != null ? a : b); yield break; }

        Vector3 p0 = a.position, p1 = b.position;
        Quaternion r0 = a.rotation, r1 = b.rotation;

        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            float k = Smooth(t / Mathf.Max(0.01f, seconds));
            cutsceneCamera.transform.SetPositionAndRotation(
                Vector3.Lerp(p0, p1, k), Quaternion.Slerp(r0, r1, k));
            yield return null;
        }
        cutsceneCamera.transform.SetPositionAndRotation(p1, r1);
    }

    // 흔들림은 카메라를 옮기는 코루틴과 싸우지 않게 LateUpdate 에서 얹는다.
    void LateUpdate()
    {
        if (!IsPlaying || _shake <= 0f || cutsceneCamera == null) return;
        cutsceneCamera.transform.position += Random.insideUnitSphere * _shake;
    }

    // ------------------------------------------------ 소리

    private void PlayHighFreq()
    {
        if (highFreqSource == null || highFreqClip == null) return;
        highFreqSource.clip = highFreqClip;
        highFreqSource.volume = highFreqVolume;
        highFreqSource.time = 0f;
        highFreqSource.Play();
    }

    /// <summary>
    /// 고주파 음을 코드에서 만든다. 3kHz 에서 9kHz 로 올라가는 사인 스윕에 페이드를 넣어
    /// "귀가 먹먹한" 소리를 만든다. 전용 음원 파일이 없어도 이 컷이 성립해야 하므로 둔다.
    /// 한 번만 만들고 클립으로 들고 있는다 - 매 프레임 계산하는 것이 아니다.
    /// </summary>
    private static AudioClip MakeHighFreqTone()
    {
        const int rate = 44100;
        const float seconds = 2.6f;
        const float fromHz = 3000f;
        const float toHz = 9000f;

        int count = (int)(rate * seconds);
        var data = new float[count];
        float phase = 0f;

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            float hz = Mathf.Lerp(fromHz, toHz, t);
            phase += 2f * Mathf.PI * hz / rate;

            // 앞뒤를 부드럽게 - 갑자기 끊기면 소리가 툭 끊긴 것처럼 들린다
            float envelope = Mathf.Min(1f, Mathf.Min(t / 0.06f, (1f - t) / 0.18f));
            data[i] = Mathf.Sin(phase) * envelope * 0.7f;
        }

        AudioClip clip = AudioClip.Create("high_freq_generated", count, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // ------------------------------------------------ 조작 잠금

    private void TakeControl()
    {
        if (interactor != null) interactor.enabled = false;

        player.InputLocked = true;
        player.CameraLocked = true;
        if (player.Controller != null) player.Controller.enabled = false;

        // 인게임 HUD 를 내린다. 컷신 위에 공포 수치가 떠 있으면 연출이 깨진다.
        HideGameplayHud();

        // 컷신 카메라로 넘긴다. 플레이어 카메라를 켜둔 채로는 어느 쪽이 그려질지 알 수 없다.
        _playerCamera = player.playerCamera;
        if (_playerCamera != null) _playerCamera.enabled = false;
        cutsceneCamera.enabled = true;

        if (!hidePlayerModel) return;

        // 컷5 는 로프에 매달린 전용 더미를 쓴다. 본체가 화면에 같이 나오면 둘이 겹친다.
        _playerRenderers = player.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in _playerRenderers) r.enabled = false;
    }

    /// <summary>
    /// 인게임 HUD 를 내린다.
    ///
    /// 보통은 빌더가 hudToHide 에 HUD 캔버스를 꽂아둔다. 비어 있으면(빌더를 아직 돌리지
    /// 않았거나 병합으로 빠졌으면) 여기서 한 번 찾는다. 컷신 시작 때 딱 한 번이라
    /// 런타임 비용이 없다 - BuildingPower 가 조명을 모으는 것과 같은 예외다.
    /// </summary>
    private void HideGameplayHud()
    {
        if (hudToHide != null && hudToHide.Length > 0)
        {
            foreach (GameObject go in hudToHide)
                if (go != null) go.SetActive(false);
            return;
        }

        // 공포 수치 표시가 붙어 있는 캔버스가 곧 인게임 HUD 다. 그 하나를 내리면
        // 산소/조준점/퀵슬롯/안내문까지 같이 내려간다(같은 캔버스에 있다).
        ScreamFearUI fear = FindFirstObjectByType<ScreamFearUI>(FindObjectsInactive.Include);
        if (fear == null) return;

        Canvas canvas = fear.GetComponentInParent<Canvas>();
        GameObject target = canvas != null ? canvas.gameObject : fear.gameObject;

        hudToHide = new[] { target };
        target.SetActive(false);
        Debug.Log($"[EndingCutscene] 인게임 HUD '{target.name}' 를 내렸다(자동 탐색).");
    }

    private static float Smooth(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }
}

