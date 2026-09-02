using UnityEngine;
using UnityEngine.InputSystem;

public class VaultWindow : MonoBehaviour
{
    public Vector3 insidePoint = new Vector3(-12f, 1.1f, 3f);
    public Vector3 outsidePoint = new Vector3(-12f, 1.1f, -3f);
    bool playerInside;
    CharacterController player;
    float nextVaultTime;

    void OnTriggerEnter(Collider c)
    {
        if (!c.CompareTag("Player")) return;
        player = c.GetComponent<CharacterController>() ?? c.GetComponentInParent<CharacterController>();
        playerInside = player != null;
    }

    void OnTriggerStay(Collider c)
    {
        if (!c.CompareTag("Player")) return;
        player = c.GetComponent<CharacterController>() ?? c.GetComponentInParent<CharacterController>();
        playerInside = player != null;
    }

    void OnTriggerExit(Collider c)
    {
        if (c.CompareTag("Player")) playerInside = false;
    }

    void Update()
    {
        if (player == null)
        {
            var playerObject = GameObject.FindWithTag("Player");
            if (playerObject != null) player = playerObject.GetComponent<CharacterController>();
        }
        if (player == null) return;
        playerInside = Vector3.Distance(player.transform.position, transform.position) <= 3.25f;
        if (!playerInside) return;
        var keyboard = Keyboard.current;
        if (keyboard == null) return;
        if (keyboard.spaceKey.isPressed && Time.time >= nextVaultTime)
            Vault();
    }

    public void Vault()
    {
        if (player == null) return;
        bool comingFromOutside = Vector3.Distance(player.transform.position, outsidePoint) < Vector3.Distance(player.transform.position, insidePoint);
        Vector3 target = comingFromOutside ? insidePoint : outsidePoint;
        player.enabled = false;
        player.transform.position = target;
        player.enabled = true;
        playerInside = false;
        nextVaultTime = Time.time + .75f;
    }

    void OnGUI()
    {
        if (!playerInside) return;
        var style = new GUIStyle(GUI.skin.box) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
        GUI.Box(new Rect(Screen.width / 2f - 170f, Screen.height - 125f, 340f, 48f), "Space  경비실 창문 넘기", style);
    }
}
