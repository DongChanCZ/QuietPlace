using UnityEngine;

public sealed class FacilityClue : MonoBehaviour
{
    public enum ClueKind
    {
        LobbyMap,
        TornDocument
    }

    [SerializeField] private ClueKind kind;
    [SerializeField] private string displayName = "단서";

    public string DisplayName => displayName;

    public void Inspect(FacilityPlayerController player)
    {
        if (player != null)
            player.InspectLobbyClue(kind);
    }
}
