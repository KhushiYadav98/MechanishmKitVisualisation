using UnityEngine;

/// <summary>
/// Attach this to the same GameObject as the Animator playing the process
/// animation - Animation Events call methods only on components on that exact
/// GameObject. Add an event in the FBX's Animation import Events list calling
/// PlayLaserSound at the desired frame.
/// </summary>
public class ProcessController : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
  
    [SerializeField] private AudioClip actuatorSound;

    [Header("Laser Object")]
    [SerializeField] private GameObject laserObject;


    private void OnEnable()
    {
        if (laserObject != null)
        {
            laserObject.SetActive(false);
        }
    }


    /// <summary>Animation Event target - plays the laser sound once.</summary>
  

    /// <summary>Animation Event target - plays the actuator sound once.</summary>
    /// 
    

 public void EnableLaserObject()
    {
        if (laserObject != null)
        {
            laserObject.SetActive(true);
        }
    } 

    public void DisableLaserObject()
    {
        if (laserObject != null)
        {
            laserObject.SetActive(false);
        }
    }

    public void PlayActuatorSound()
    {
        if (audioSource != null && actuatorSound != null)
        {
            audioSource.PlayOneShot(actuatorSound);
        }
    }
}
