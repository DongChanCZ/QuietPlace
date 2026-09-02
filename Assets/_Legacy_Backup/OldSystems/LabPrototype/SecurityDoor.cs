using UnityEngine;
public class SecurityDoor : MonoBehaviour, IInteractable
{
    public void Use() => LabEntranceGame.Instance.TryDoor(gameObject);
}
