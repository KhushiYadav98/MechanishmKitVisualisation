using UnityEngine;

public class PositionSetController : MonoBehaviour
{
    [SerializeField] private Transform cylindricalBaseTransform;
    

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    } 

    public void OnCylindricalBaseReleased()
    {
        if (cylindricalBaseTransform != null) cylindricalBaseTransform.localRotation = Quaternion.Euler(90f, 0f, 0f);
    }
}
