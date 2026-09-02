using UnityEngine;
public class MapPanel : MonoBehaviour, IInteractable
{
    public void Use() => LabEntranceGame.Instance.OpenMap();
}
