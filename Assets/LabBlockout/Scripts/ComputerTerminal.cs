using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 3층 방송실 안의 컴퓨터. E 를 누르면 비밀번호를 넣고, 통과하면 주파수 맞추기로 이어진다.
///
/// 문을 여는 키패드(KeypadTerminal)와는 별개다. 문은 회로를 뜯어 빨리치기로 열고,
/// 이 컴퓨터는 잠금이 풀린 뒤 주파수를 맞추는 데 쓴다.
/// </summary>
[DisallowMultipleComponent]
public sealed class ComputerTerminal : Interactable
{
    [SerializeField] string displayName = "방송 제어 콘솔";

    [Header("한 번 통과하면 다시 하지 않는다")]
    [SerializeField] bool oneShot = true;

    [Header("사운드")]
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip solvedSound;

    [Header("주파수 정합까지 끝났을 때 할 일")]
    public UnityEvent onSolved;

    /// 비밀번호를 이미 통과했는가. 중간에 Esc 로 나갔다 들어와도 다시 묻지 않는다.
    public bool PasswordSolved { get; private set; }

    /// 주파수를 맞춰 송출이 시작됐는가.
    public bool FrequencySet { get; private set; }

    /// 송출까지 다 끝났는가.
    public bool Solved { get; private set; }

    // 콘솔은 손이 차 있어도 만질 수 있다 - 키보드는 팔꿈치로도 눌린다
    public override bool AllowedWhileHandsFull => true;

    public override string Prompt(IInteractor player)
    {
        if (Solved && oneShot) return null;
        if (FrequencySet) return null;   // 송출 중에는 더 만질 게 없다
        return "E  " + displayName + (PasswordSolved ? " 주파수 정합" : " 접속");
    }

    public override void Interact(IInteractor player)
    {
        if (Solved && oneShot) return;
        if (FrequencySet) return;

        if (FrequencyHackManager.Instance == null)
        {
            // 매니저가 없는 씬(프리뷰 등)에서는 진행이 막히지 않게 통과 처리한다.
            Debug.LogWarning($"[{nameof(ComputerTerminal)}] FrequencyHackManager 가 없어 미니게임 없이 통과시킨다.", this);
            MarkPasswordSolved();
            OnSolved(player);
            return;
        }

        FrequencyHackManager.Instance.Begin(this, player);
    }

    /// <summary>비밀번호 단계를 통과했을 때 매니저가 부른다.</summary>
    public void MarkPasswordSolved()
    {
        PasswordSolved = true;
    }

    /// <summary>네 대역을 다 맞춰 송출이 시작됐을 때 매니저가 부른다.</summary>
    public void OnFrequencySet(IInteractor player)
    {
        FrequencySet = true;
        PasswordSolved = true;
        if (player != null) player.ShowNotice("주파수가 맞았다. 송출이 시작된다 - 숨어서 버텨라.");
    }

    /// <summary>송출까지 끝났을 때 매니저가 부른다.</summary>
    public void OnSolved(IInteractor player)
    {
        if (Solved && oneShot) return;

        Solved = true;
        PasswordSolved = true;

        if (audioSource != null && solvedSound != null) audioSource.PlayOneShot(solvedSound);
        if (player != null) player.ShowNotice("주파수가 맞춰졌습니다.");

        onSolved?.Invoke();
    }
}
