using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f;

    void Update()
    {
        Vector3 moveDir = Vector3.zero;

        if (Keyboard.current.wKey.isPressed) moveDir += transform.forward;
        if (Keyboard.current.sKey.isPressed) moveDir -= transform.forward;
        if (Keyboard.current.aKey.isPressed) moveDir -= transform.right;
        if (Keyboard.current.dKey.isPressed) moveDir += transform.right;

        moveDir.Normalize();

        transform.position += moveDir * moveSpeed * Time.deltaTime;
    }
}
