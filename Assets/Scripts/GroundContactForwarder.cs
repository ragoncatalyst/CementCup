using UnityEngine;

/// <summary>
/// Forwards ground collision/trigger events from the physics root (PlayerEmpty)
/// to the PlayerMov script which may be on a child object (PlayerQuad).
/// This is automatically added by PlayerMov when needed.
/// </summary>
public class GroundContactForwarder : MonoBehaviour
{
    [HideInInspector] public PlayerMov targetPlayerMov;
    [HideInInspector] public string groundTag = "Ground";

    private void OnTriggerEnter(Collider other)
    {
        if (targetPlayerMov == null) return;
        if (!other.CompareTag(groundTag)) return;
        targetPlayerMov.HandleGroundContact(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (targetPlayerMov == null) return;
        if (!other.CompareTag(groundTag)) return;
        targetPlayerMov.HandleGroundContact(false);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (targetPlayerMov == null) return;
        if (!collision.collider.CompareTag(groundTag)) return;
        targetPlayerMov.HandleGroundContact(true);
    }

    private void OnCollisionExit(Collision collision)
    {
        if (targetPlayerMov == null) return;
        if (!collision.collider.CompareTag(groundTag)) return;
        targetPlayerMov.HandleGroundContact(false);
    }
}
