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

    [SerializeField] private GameObject startupModule;
    [SerializeField] private GameObject explorationModule;
    [SerializeField] private GameObject PositionSetModule;

    [SerializeField] private GameObject ProcessModule;

    [Header("Grab Interaction")]
    [SerializeField] private Behaviour grabbableScript;
    [SerializeField] private GameObject handGrabInteractionObject;
    [SerializeField] private Transform cylindricalBaseTransform;

    [Header("Spawn In Front Of User")]
    [SerializeField] private Transform xrCamera;
    [SerializeField] private float spawnDistance = 1.2f;

    [Header("Reveal Settings")]
    [SerializeField] private float revealDuration = 2f;
    [SerializeField] private AnimationCurve revealCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private Shader revealShader;
    [SerializeField] private AudioSource revealAudioSource;
    [SerializeField] private AudioClip revealSound;
    [SerializeField] private AudioClip placeKitInstructionSound;
    [SerializeField] private float placeKitInstructionDelay = 3f;
    [SerializeField] private AudioClip processModuleStartSound;

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
    private bool _positionSetStarted;

    private readonly List<Material> _unfinishedMaterials = new List<Material>();
    private readonly List<Material> _realMaterials = new List<Material>();

    private void Awake()
    {
        if (revealShader == null) revealShader = Shader.Find("Custom/RevealLit");

        if (unfinishedModel != null)
        {
            unfinishedModel.SetActive(true);
            ConvertToRevealMaterials(unfinishedModel, invertClip: true, _unfinishedMaterials);

            // _RevealHeight defaults to 0 until the reveal sequence sets it.
            // With InvertClip on (show above threshold), carrying the model
            // below world Y 0 during placement would clip the whole mesh away,
            // exposing the OutlineBlack backfaces as a solid silhouette. Force
            // it fully visible until TriggerPlacementDone takes over.
            SetRevealHeight(_unfinishedMaterials, -10000f);
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

        PositionStartupModuleInFrontOfUser();
        ActivateOnlyModule(startupModule);

        StartCoroutine(PlayPlaceKitInstructionRoutine());
    }

    /// <summary>
    /// The headset's starting position/facing direction depends on wherever
    /// the user happens to be standing and looking when tracking begins - it
    /// is not fixed by the Eye Level/Floor Level tracking origin setting, which
    /// only calibrates the vertical (Y) reference point. So instead of relying
    /// on a fixed world position, move the starting module in front of wherever
    /// the camera actually is at launch.
    /// </summary>
    private void PositionStartupModuleInFrontOfUser()
    {
        if (startupModule == null || xrCamera == null) return;

        Vector3 forward = xrCamera.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = xrCamera.up;
        forward.Normalize();

        Vector3 targetPosition = xrCamera.position + forward * spawnDistance;
        targetPosition.y = startupModule.transform.position.y;

        // Position only - leave rotation as originally authored. This project's
        // models don't follow a plain "local +Z is forward" convention, so a
        // fresh LookRotation here turns the object to face the wrong way. The
        // hand-authored rotation already looks correct; it just needs moving.
        startupModule.transform.position = targetPosition;
    }

    private IEnumerator PlayPlaceKitInstructionRoutine()
    {
        yield return new WaitForSeconds(placeKitInstructionDelay);

        if (revealAudioSource != null && placeKitInstructionSound != null)
        {
            revealAudioSource.PlayOneShot(placeKitInstructionSound);
        }
    }

    /// <summary>Activates exactly one of the four modules and deactivates the rest.</summary>
    private void ActivateOnlyModule(GameObject moduleToActivate)
    {
        if (startupModule != null) startupModule.SetActive(startupModule == moduleToActivate);
        if (explorationModule != null) explorationModule.SetActive(explorationModule == moduleToActivate);
        if (PositionSetModule != null) PositionSetModule.SetActive(PositionSetModule == moduleToActivate);
        if (ProcessModule != null) ProcessModule.SetActive(ProcessModule == moduleToActivate);
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
        if (explorationModule != null && cylindricalBaseTransform != null)
        {
            explorationModule.transform.position = cylindricalBaseTransform.position;
        }
        ActivateOnlyModule(explorationModule);
    }

     public void StartPositionSet()
    {
        // Only sync to the cylinder base's position the first time - after
        // that, keep whatever position the user last left it at inside the
        // Position Set module instead of re-snapping it every re-entry.
        if (PositionSetModule != null && !_positionSetStarted && cylindricalBaseTransform != null)
        {
            PositionSetModule.transform.position = cylindricalBaseTransform.position;
        }
        _positionSetStarted = true;
        ActivateOnlyModule(PositionSetModule);
    }

         public void StartProcessAni()
    {
        // Only sync to the cylinder base's position the first time - after
        // that, keep whatever position the user last left it at inside the
        // Position Set module instead of re-snapping it every re-entry.
        if (ProcessModule != null &&  cylindricalBaseTransform != null)
        {
            ProcessModule.transform.position = cylindricalBaseTransform.position;
        }
        _positionSetStarted = true;
        ActivateOnlyModule(ProcessModule);

        if (revealAudioSource != null && processModuleStartSound != null)
        {
            revealAudioSource.Stop();
            revealAudioSource.clip = processModuleStartSound;
            revealAudioSource.Play();
        }
    }

    /// <summary>Hook this up to the Process module's Back button's OnClick.</summary>
    public void GoBackFromProcess()
    {
        ActivateOnlyModule(startupModule);
        revealAudioSource.Stop();
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

     startupModule.GetComponent<MeshRenderer>().enabled = false;
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

        if (revealAudioSource != null && revealSound != null)
        {
            revealAudioSource.PlayOneShot(revealSound);
        }

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
