using UnityEngine;
public class StoryNote : MonoBehaviour, IInteractable
{
    public void Use() => LabEntranceGame.Instance.OpenStory();
}
