using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 주파수를 맞춘 뒤의 송출 대기.
///
/// 주파수를 맞추는 것까지가 플레이어의 조작이고, 그 다음은 기다리는 시간이다. 컴퓨터 앞에
/// 붙잡아두면 그냥 화면만 보는 지루한 5초가 되므로, 맞추는 순간 미니게임 UI 를 닫고
/// 조작을 돌려준다. 게이지는 월드에서 혼자 차오르고 플레이어는 그동안 숨어서 버틴다.
///
/// 차오르는 동안 소음이 계속 나가므로 괴물이 방으로 찾아온다 - 그게 이 구간의 내용이다.
/// </summary>
[DefaultExecutionOrder(-500)]
public class FrequencyBroadcast : MonoBehaviour
{
    public static FrequencyBroadcast Instance { get; private set; }

    [Header("송출에 걸리는 시간(초)")]
    public float duration = 45f;

    [Header("HUD - 송출 진행 표시 (평소에는 꺼둔다)")]
    [SerializeField] private GameObject hudRoot;
    [SerializeField] private Image progressFill;
    [SerializeField] private TMP_Text label;

    [Header("송출 위치 - 소음이 나가는 지점 (안테나/확성기)")]
    [SerializeField] private Transform emitPoint;

    [Header("소음 - 이 간격마다 한 번씩 낸다(초)")]
    [SerializeField] private float noiseInterval = 1.5f;
    [SerializeField] private float noiseLoudness = 9f;
    [SerializeField] private float noiseRange = 30f;

    [Header("송출 중 방을 부수는 연출")]
    [SerializeField] private RoomRampage rampage;

    [Header("송출이 끝났을 때")]
    public UnityEvent onBroadcastComplete;

    public bool Running { get; private set; }
    public bool Completed { get; private set; }
    public float Progress01 => duration <= 0f ? 1f : Mathf.Clamp01(_elapsed / duration);

    private float _elapsed;
    private float _noiseTimer;

    // 끝났을 때 완료를 돌려줄 대상. 프리팹이 아니라 시작할 때 넘겨받는다.
    private ComputerTerminal _computer;
    private IInteractor _player;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (hudRoot != null) hudRoot.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>주파수를 맞췄을 때 시작한다. 끝나면 computer 에 완료를 알린다.</summary>
    public void Begin(ComputerTerminal computer, IInteractor player)
    {
        if (Running || Completed) return;

        duration = MinigameTuning.BroadcastSeconds;

        _computer = computer;
        _player = player;
        Running = true;
        _elapsed = 0f;
        _noiseTimer = 0f;

        if (hudRoot != null) hudRoot.SetActive(true);
        Refresh();

        if (rampage != null) rampage.Begin();
    }

    void Update()
    {
        if (!Running) return;

        _elapsed += Time.deltaTime;

        // 송출음이 계속 나간다. 괴물은 NoiseManager 를 통해 이 소리를 듣는다.
        _noiseTimer -= Time.deltaTime;
        if (_noiseTimer <= 0f)
        {
            _noiseTimer = Mathf.Max(0.1f, noiseInterval);
            EmitNoise();
        }

        Refresh();

        if (_elapsed < duration) return;

        Running = false;
        Completed = true;
        if (hudRoot != null) hudRoot.SetActive(false);
        if (rampage != null) rampage.Stop();

        // 이게 없으면 송출이 끝나도 컴퓨터가 완료 처리되지 않는다.
        if (_computer != null) _computer.OnSolved(_player);

        GameCheckpoint.Save(GameStep.Broadcast, _player);

        onBroadcastComplete?.Invoke();

        // 여기서 엔딩으로 넘어간다. 컷신이 없는 씬(프리뷰 등)에서는 그냥 끝난다.
        if (EndingCutscene.Instance != null) EndingCutscene.Instance.Begin(_player);
    }

    private void EmitNoise()
    {
        if (NoiseManager.Instance == null) return;

        Vector3 at = emitPoint != null ? emitPoint.position : transform.position;
        NoiseManager.Instance.Emit(new NoiseEvent(at, noiseLoudness, noiseRange, NoiseType.Custom));
    }

    private void Refresh()
    {
        if (progressFill != null) progressFill.fillAmount = Progress01;

        if (label == null) return;

        float remain = Mathf.Max(0f, duration - _elapsed);
        label.text = Completed ? "송출 완료" : "주파수 송출 중 · " + remain.ToString("0") + "초";
    }
}
