using UnityEngine;

public class AutoSlideDoor : MonoBehaviour
{
    public Vector3 openOffset = new Vector3(0, 3.2f, 0);
    public float speed = 2f;
    Vector3 closedPos, targetPos;
    bool triggered;

    void Awake() { closedPos = transform.position; targetPos = closedPos; }

    void OnTriggerEnter(Collider c)
    {
        if (!triggered && c.CompareTag("Player")) { triggered = true; targetPos = closedPos + openOffset; }
    }

    void Update()
    {
        if (transform.position != targetPos)
            transform.position = Vector3.MoveTowards(transform.position, targetPos, speed * Time.deltaTime);
    }
}
