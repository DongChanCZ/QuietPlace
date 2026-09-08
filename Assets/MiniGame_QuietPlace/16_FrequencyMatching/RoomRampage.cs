using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 송출을 기다리는 동안 괴물이 방 안의 물건을 부수는 연출.
///
/// 등록된 소품을 하나씩 골라 물리를 켜고 밀어 넘긴다. 넘어질 때마다 소음을 내므로
/// 플레이어는 "지금 어디가 부서졌다"를 소리로 알게 되고, 숨어 있는 동안 방 안이
/// 점점 망가진다.
///
/// 새 오브젝트를 만들지 않는다. 이미 방에 있는 소품에 Rigidbody 를 붙여 넘기기만 한다.
/// 맵 프리팹 구조를 바꾸지 않으려고 부모도 옮기지 않는다.
/// </summary>
public class RoomRampage : MonoBehaviour
{
    [Header("부술 소품들 (에디터 도구로 채운다)")]
    [SerializeField] private Transform[] props;

    [Header("이 간격마다 하나씩 부순다(초)")]
    [SerializeField] private float interval = 3.5f;
    [SerializeField] private float intervalJitter = 1.5f;

    [Header("밀어내는 힘")]
    [SerializeField] private float impulse = 4.5f;
    [SerializeField] private float upwardImpulse = 1.6f;
    [SerializeField] private float torque = 3f;

    [Header("넘어질 때 나는 소음")]
    [SerializeField] private float noiseLoudness = 6f;
    [SerializeField] private float noiseRange = 22f;

    [Header("소리 - SoundManager 에 등록된 이름")]
    [SerializeField] private string breakSoundName = "";

    private readonly List<Transform> _remaining = new List<Transform>();
    private bool _running;
    private float _timer;

    public bool Running => _running;
    public int RemainingCount => _remaining.Count;

    public void Begin()
    {
        _remaining.Clear();
        if (props != null)
            foreach (Transform t in props)
                if (t != null) _remaining.Add(t);

        _running = _remaining.Count > 0;
        _timer = NextDelay() * 0.5f;   // 시작하고 조금 있다 첫 소리가 난다
    }

    public void Stop()
    {
        _running = false;
    }

    void Update()
    {
        if (!_running) return;

        _timer -= Time.deltaTime;
        if (_timer > 0f) return;

        _timer = NextDelay();
        SmashOne();

        if (_remaining.Count == 0) _running = false;
    }

    private float NextDelay()
    {
        return Mathf.Max(0.4f, interval + Random.Range(-intervalJitter, intervalJitter));
    }

    private void SmashOne()
    {
        while (_remaining.Count > 0)
        {
            int i = Random.Range(0, _remaining.Count);
            Transform t = _remaining[i];
            _remaining.RemoveAt(i);
            if (t == null) continue;

            Rigidbody body = t.GetComponent<Rigidbody>();
            if (body == null) body = t.gameObject.AddComponent<Rigidbody>();
            body.isKinematic = false;
            body.useGravity = true;

            // 소품에 콜라이더가 없으면 바닥을 뚫고 떨어진다
            if (t.GetComponent<Collider>() == null)
            {
                BoxCollider box = t.gameObject.AddComponent<BoxCollider>();
                box.isTrigger = false;
            }

            Vector3 dir = Random.insideUnitSphere;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
            dir.Normalize();

            body.AddForce(dir * impulse + Vector3.up * upwardImpulse, ForceMode.Impulse);
            body.AddTorque(Random.insideUnitSphere * torque, ForceMode.Impulse);

            if (NoiseManager.Instance != null)
                NoiseManager.Instance.Emit(new NoiseEvent(t.position, noiseLoudness, noiseRange, NoiseType.ObjectImpact));

            if (!string.IsNullOrEmpty(breakSoundName) && SoundManager.Instance != null)
                SoundManager.Instance.PlaySFX(breakSoundName);

            return;
        }
    }
}
