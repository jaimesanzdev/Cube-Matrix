using UnityEngine;

public class PortalTile : MonoBehaviour
{
    [SerializeField] private PortalTile linkedPortal; // drag the exit portal here in Inspector

    private bool isOnCooldown = false; // stops infinite teleport loop

    public void TryTeleport(Transform cube)
    {
        if (isOnCooldown || linkedPortal == null) return;

        // move cube to exit portal position, keep same Y
        Vector3 exitPos = linkedPortal.transform.position;
        exitPos.y = cube.position.y;
        cube.position = exitPos;

        // put exit portal on cooldown so cube doesn't instantly teleport back
        linkedPortal.SetCooldown();
    }

    public void SetCooldown()
    {
        isOnCooldown = true;
        Invoke(nameof(ResetCooldown), 0.5f);
    }

    private void ResetCooldown()
    {
        isOnCooldown = false;
    }
    public Vector3 GetExitPosition(float cubeY)
    {
        Vector3 exitPos = linkedPortal.transform.position;
        exitPos.y = cubeY;
        return exitPos;
    }
}