using UnityEngine;
public class AccessCard : MonoBehaviour, IInteractable
{
    public void Use() => LabEntranceGame.Instance.TakeCard(gameObject);
}
