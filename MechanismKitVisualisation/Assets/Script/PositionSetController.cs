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
        if (cylindricalBaseTransform != null) cylindricalBaseTransform.localRotation = Quaternion.Euler(90f, 0f, 0f);
    }

    /// <summary>Hook this up to the Back button's OnClick.</summary>
    public void GoBackToStartup()
    {
       // gameObject.SetActive(false);
       cylindricalBaseTransform.gameObject.SetActive(false);
        if (startupModule != null) startupModule.SetActive(true);
    }
}
