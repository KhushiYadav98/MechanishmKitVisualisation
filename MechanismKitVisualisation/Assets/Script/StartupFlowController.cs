using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Drives the app's starting flow: the unfinished placeholder model is active
/// so the user can grab/place it. Call TriggerPlacementDone() (wire it to a UI
/// Button's OnClick, or use the context menu below to test it in Play mode) to
/// swap in the real model at the same spot. Both models are shaded with
/// Custom/RevealLit, which clips based on world-space height and draws a
/// glowing boundary line at the current cutoff: the real model reveals from
/// the bottom up while the unfinished model vanishes from the bottom up.
/// </summary>
public class StartupFlowController : MonoBehaviour
{
    private static readonly int RevealHeightId = Shader.PropertyToID("_RevealHeight");
    private static readonly int InvertClipId = Shader.PropertyToID("_InvertClip");
    private static readonly int BoundaryIntensityId = Shader.PropertyToID("_BoundaryIntensity");

    [Header("Models")]
    [SerializeField] private GameObject unfinishedModel;
    [SerializeField] private GameObject realModel;

    [Header("UI")]
    [SerializeField] private GameObject placementButtonCanvas;

    [Header("Modules")]
    [SerializeField] private GameObject explorationModule;
    [SerializeField] private GameObject PositionSetModule;

    [SerializeField] private GameObject ProcessModule;

    [Header("Grab Interaction")]
    [SerializeField] private Behaviour grabbableScript;
    [SerializeField] private GameObject handGrabInteractionObject;
    [SerializeField] private Transform cylindricalBaseTransform;

    [Header("Reveal Settings")]
    [SerializeField] private float revealDuration = 2f;
    [SerializeField] private AnimationCurve revealCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private Shader revealShader;

    [Header("Pop-Up Objects (shown once reveal completes)")]
    [SerializeField] private GameObject[] popUpObjects;
    [SerializeField] private float popUpDuration = 0.35f;
    [SerializeField] private AnimationCurve popUpCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private Vector3[] _popUpInitialScales;

    [Header("Events")]
   // public UnityEvent onPlacementDone;
   // public UnityEvent onRevealComplete;

    private bool _isRevealing;
    private bool _placementDone;

    private readonly List<Material> _unfinishedMaterials = new List<Material>();
    private readonly List<Material> _realMaterials = new List<Material>();

    private void Awake()
    {
        if (revealShader == null) revealShader = Shader.Find("Custom/RevealLit");

        if (unfinishedModel != null)
        {
            unfinishedModel.SetActive(true);
            ConvertToRevealMaterials(unfinishedModel, invertClip: true, _unfinishedMaterials);
        }

        if (realModel != null)
        {
            ConvertToRevealMaterials(realModel, invertClip: false, _realMaterials);
            realModel.SetActive(false);
        }

        if (popUpObjects != null && popUpObjects.Length > 0)
        {
            _popUpInitialScales = new Vector3[popUpObjects.Length];
            for (int i = 0; i < popUpObjects.Length; i++)
            {
                if (popUpObjects[i] == null) continue;
                _popUpInitialScales[i] = popUpObjects[i].transform.localScale;
                popUpObjects[i].SetActive(false);
            }
        }
    }
   
   private void Start()
    {
        if (placementButtonCanvas != null) placementButtonCanvas.SetActive(false);
        realModel.SetActive(false);
    }
    /// <summary>Hook this up to the model's grab/select event, to show the "Placement Done" button.</summary>
    public void ShowPlacementButton()
    {
        if (placementButtonCanvas != null) placementButtonCanvas.SetActive(true);
    }

    /// <summary>Hook this up to the grab release event, to snap Cylinder Base upright.</summary>
    public void OnCylindricalBaseReleased()
    {
        if (cylindricalBaseTransform != null) cylindricalBaseTransform.localRotation = Quaternion.Euler(90f, 0f, 0f);
    }

    /// <summary>Hook this up to the "Explore" button's OnClick.</summary>
    public void StartExploration()
    {
        if (explorationModule != null)
        {
            if (cylindricalBaseTransform != null)
            {
                explorationModule.transform.position = cylindricalBaseTransform.position;
            }
            explorationModule.SetActive(true);
        }
        if (cylindricalBaseTransform != null) cylindricalBaseTransform.gameObject.SetActive(false);
    } 

     public void StartPositionSet()
    {
        if (PositionSetModule != null)
        {
            if (cylindricalBaseTransform != null)
            {
                PositionSetModule.transform.position = cylindricalBaseTransform.position;
            }
            PositionSetModule.SetActive(true);
        }
        if (cylindricalBaseTransform != null) cylindricalBaseTransform.gameObject.SetActive(false);
    } 

    /// <summary>Hook this up to the "Placement Done" button's OnClick.</summary>
    public void TriggerPlacementDone()
    {
        if (_placementDone || _isRevealing) return;
        if (unfinishedModel == null || realModel == null)
        {
            Debug.LogWarning("StartupFlowController: assign both Unfinished Model and Real Model.");
            return;
        }

        if (placementButtonCanvas != null) placementButtonCanvas.SetActive(false);
        if (grabbableScript != null) grabbableScript.enabled = false;
        if (handGrabInteractionObject != null) handGrabInteractionObject.SetActive(false);

        _placementDone = true;
     //   onPlacementDone?.Invoke();
        StartCoroutine(RevealRoutine());
    }

    [ContextMenu("Test: Trigger Placement Done")]
    private void TestTriggerPlacementDone()
    {
        TriggerPlacementDone();
    }

    private IEnumerator RevealRoutine()
    {
        _isRevealing = true;

        // realModel starts out parented under unfinishedModel so it inherits its
        // placement. Detach it first (keeping its current world transform) so it
        // doesn't get deactivated along with unfinishedModel at the end.
        realModel.transform.SetParent(unfinishedModel.transform.parent, true);
        realModel.SetActive(true);

        Bounds unfinishedBounds = GetWorldBounds(unfinishedModel);
        Bounds realBounds = GetWorldBounds(realModel);

        SetRevealHeight(_unfinishedMaterials, unfinishedBounds.min.y);
        SetRevealHeight(_realMaterials, realBounds.min.y);

        float t = 0f;
        while (t < revealDuration)
        {
            t += Time.deltaTime;
            float p = revealCurve.Evaluate(Mathf.Clamp01(t / revealDuration));

            SetRevealHeight(_unfinishedMaterials, Mathf.LerpUnclamped(unfinishedBounds.min.y, unfinishedBounds.max.y, p));
            SetRevealHeight(_realMaterials, Mathf.LerpUnclamped(realBounds.min.y, realBounds.max.y, p));
            yield return null;
        }

        SetRevealHeight(_unfinishedMaterials, unfinishedBounds.max.y);
        SetRevealHeight(_realMaterials, realBounds.max.y);

        // The boundary line would otherwise stay lit forever at the top edge,
        // since the topmost pixels stay within _BoundaryWidth of the final
        // _RevealHeight once the animation is done.
        SetBoundaryIntensity(_realMaterials, 0f);

        unfinishedModel.SetActive(false);

        _isRevealing = false;
     //   onRevealComplete?.Invoke();

        StartCoroutine(PopUpRoutine());
    }

    private IEnumerator PopUpRoutine()
    {
        if (popUpObjects == null || popUpObjects.Length == 0) yield break;

        for (int i = 0; i < popUpObjects.Length; i++)
        {
            if (popUpObjects[i] == null) continue;
            popUpObjects[i].transform.localScale = Vector3.zero;
            popUpObjects[i].SetActive(true);
        }

        float t = 0f;
        while (t < popUpDuration)
        {
            t += Time.deltaTime;
            float p = popUpCurve.Evaluate(Mathf.Clamp01(t / popUpDuration));
            for (int i = 0; i < popUpObjects.Length; i++)
            {
                if (popUpObjects[i] == null) continue;
                popUpObjects[i].transform.localScale = _popUpInitialScales[i] * p;
            }
            yield return null;
        }

        for (int i = 0; i < popUpObjects.Length; i++)
        {
            if (popUpObjects[i] == null) continue;
            popUpObjects[i].transform.localScale = _popUpInitialScales[i];
        }
    }

    private static void SetRevealHeight(List<Material> materials, float height)
    {
        for (int i = 0; i < materials.Count; i++)
        {
            materials[i].SetFloat(RevealHeightId, height);
        }
    }

    private static void SetBoundaryIntensity(List<Material> materials, float intensity)
    {
        for (int i = 0; i < materials.Count; i++)
        {
            materials[i].SetFloat(BoundaryIntensityId, intensity);
        }
    }

    /// <summary>
    /// Replaces each renderer's Lit-style material with a unique Custom/RevealLit
    /// instance, copying over the base texture/color so the look stays the same
    /// once the reveal finishes. Materials without a _BaseMap/_BaseColor (e.g. a
    /// separate outline material) are left untouched.
    /// </summary>
    private void ConvertToRevealMaterials(GameObject target, bool invertClip, List<Material> results)
    {
        if (revealShader == null)
        {
            Debug.LogError("StartupFlowController: Custom/RevealLit shader not found.");
            return;
        }

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            Material[] sharedMats = renderer.sharedMaterials;
            Material[] newMats = new Material[sharedMats.Length];

            for (int i = 0; i < sharedMats.Length; i++)
            {
                Material original = sharedMats[i];
                if (original == null || !original.HasProperty("_BaseColor"))
                {
                    newMats[i] = original;
                    continue;
                }

                Material revealMat = new Material(revealShader);
                if (original.HasProperty("_BaseMap"))
                {
                    Texture baseMap = original.GetTexture("_BaseMap");
                    revealMat.SetTexture("_BaseMap", baseMap);
                    revealMat.SetTextureScale("_BaseMap", original.GetTextureScale("_BaseMap"));
                    revealMat.SetTextureOffset("_BaseMap", original.GetTextureOffset("_BaseMap"));
                }
                revealMat.SetColor("_BaseColor", original.GetColor("_BaseColor"));
                revealMat.SetFloat(InvertClipId, invertClip ? 1f : 0f);

                newMats[i] = revealMat;
                results.Add(revealMat);
            }

            renderer.materials = newMats;
        }
    }

    private static Bounds GetWorldBounds(GameObject target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return new Bounds(target.transform.position, Vector3.zero);
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }
        return bounds;
    }
}
