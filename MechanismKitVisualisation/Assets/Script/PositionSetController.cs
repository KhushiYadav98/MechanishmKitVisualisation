using UnityEngine;

public class PositionSetController : MonoBehaviour
{
    [SerializeField] private Transform cylindricalBaseTransform;
    [SerializeField] private GameObject startupModule;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
       cylindricalBaseTransform.gameObject.SetActive(true);
    }

    // Update is called once per frame
    void Update()
    {
        
    } 

    public void OnCylindricalBaseReleased()
    {
        if (cylindricalBaseTransform == null) return;

        // Keep whatever Z spin the user left it at, reset X/Y tilt back to upright.
        // A free 3-axis grab rotation can't be reduced to "just read Euler Z" -
        // that gives unpredictable results once X/Y are also non-zero. Instead,
        // properly extract the twist component around the local Z axis.
        Quaternion twist = ExtractTwistAroundZ(cylindricalBaseTransform.localRotation);
        cylindricalBaseTransform.localRotation = Quaternion.Euler(90f, 0f, 0f) * twist;
    }

    private static Quaternion ExtractTwistAroundZ(Quaternion rotation)
    {
        Vector3 imaginary = new Vector3(rotation.x, rotation.y, rotation.z);
        Vector3 projected = Vector3.Project(imaginary, Vector3.forward);
        Quaternion twist = new Quaternion(projected.x, projected.y, projected.z, rotation.w);

        float magnitude = Mathf.Sqrt(twist.x * twist.x + twist.y * twist.y + twist.z * twist.z + twist.w * twist.w);
        if (magnitude < 1e-6f) return Quaternion.identity;

        return new Quaternion(twist.x / magnitude, twist.y / magnitude, twist.z / magnitude, twist.w / magnitude);
    }

    /// <summary>Hook this up to the Back button's OnClick.</summary>
    public void GoBackToStartup()
    {
       // gameObject.SetActive(false);
       cylindricalBaseTransform.gameObject.SetActive(false);
        if (startupModule != null) startupModule.SetActive(true);
    }
}
