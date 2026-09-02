using UnityEngine;
public class SecretWall : MonoBehaviour, IInteractable
{
    public void Use() => LabEntranceGame.Instance.TrySecretWall(gameObject);
}
