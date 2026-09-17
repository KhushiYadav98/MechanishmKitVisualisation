using UnityEngine;

/// <summary>
/// Attach this to the same GameObject as the Animator playing the process
/// animation - Animation Events call methods only on components on that exact
/// GameObject. Add events in the FBX's Animation import Events list calling
/// EnableLaserLight / DisableLaserLight at the desired frames.
/// </summary>
public class ProcessController : MonoBehaviour
{
    [SerializeField] private GameObject laserLight;

    public void EnableLaserLight()
    {
        if (laserLight != null) laserLight.SetActive(true);
    }

    public void DisableLaserLight()
    {
        if (laserLight != null) laserLight.SetActive(false);
    }
}
