using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Drives the mechanism kit exploration module: at startup every listed
/// component gets the highlight shader applied (its original materials are
/// cached first, including any child renderers). Each component's grab/select
/// event should call OnComponentGrabbed(id), passing its own id as a static
/// argument in the Inspector. That removes the highlight from that component
/// (restoring its original materials) and shows its info text in the shared
/// TextMeshProUGUI panel.
/// </summary>
public class ExplorationController : MonoBehaviour
{
    [System.Serializable]
    public class ExplorationComponentData
    {
        public string id;
        public GameObject[] targetObjects;
        public string subheading;
        [TextArea] public string infoText;
        public AudioClip audioClip;
    }

    private class RendererCache
    {
        public Renderer renderer;
        public Material[] originalMaterials;
    }

    [SerializeField] private List<ExplorationComponentData> components;
    [SerializeField] private Material highlightMaterial;

    [Header("Main UI")]
    [SerializeField] private GameObject mainUIPanel;

    [Header("Shared Info UI")]
    [SerializeField] private GameObject infoUIPanel;
    [SerializeField] private TextMeshProUGUI subheadingText;
    [SerializeField] private TextMeshProUGUI infoText;

    [Header("Back / Reset")]
    [SerializeField] private GameObject startupModule;
    [SerializeField] private float resetDuration = 1f;
    [SerializeField] private AnimationCurve resetCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private Transform cylindricalBaseTransform;
    [SerializeField] private Vector3 cylindricalBaseStartRotation = new Vector3(90f, 0f, 0f);

    [Header("Idle Float (after release)")]
    [SerializeField] private float floatAmplitude = 0.02f;
    [SerializeField] private float floatSpeed = 1f;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip startupAudioClip;

    private Dictionary<string, ExplorationComponentData> _lookup;
    private Dictionary<string, List<RendererCache>> _rendererCaches;
    private readonly List<(Transform transform, Vector3 originalLocalPosition, Quaternion originalLocalRotation, Vector3 originalLocalScale)> _originalTransforms =
        new List<(Transform, Vector3, Quaternion, Vector3)>();
    private readonly Dictionary<Transform, Coroutine> _floatRoutines = new Dictionary<Transform, Coroutine>();

    private Coroutine _resetRoutine;

    private void Awake()
    {
        _lookup = new Dictionary<string, ExplorationComponentData>();
        _rendererCaches = new Dictionary<string, List<RendererCache>>();

        foreach (ExplorationComponentData data in components)
        {
            if (data == null || string.IsNullOrEmpty(data.id) || data.targetObjects == null) continue;

            if (!_lookup.ContainsKey(data.id))
            {
                _lookup.Add(data.id, data);
            }

            if (!_rendererCaches.TryGetValue(data.id, out List<RendererCache> caches))
            {
                caches = new List<RendererCache>();
                _rendererCaches.Add(data.id, caches);
            }

            foreach (GameObject targetObject in data.targetObjects)
            {
                if (targetObject == null) continue;
                _originalTransforms.Add((targetObject.transform, targetObject.transform.localPosition, targetObject.transform.localRotation, targetObject.transform.localScale));
                caches.AddRange(CacheAndApplyHighlight(targetObject));
            }
        }

        if (infoUIPanel != null) infoUIPanel.SetActive(false);
        if (mainUIPanel != null) mainUIPanel.SetActive(true);
    }

    /// <summary>Fires every time this module is (re)activated, e.g. each time the user enters exploration.</summary>
    private void OnEnable()
    {
        PlayAudio(startupAudioClip);
    }

    /// <summary>Stops whatever is currently playing and plays clip from the start - even if it's the same clip already playing.</summary>
    private void PlayAudio(AudioClip clip)
    {
        if (audioSource == null || clip == null) return;

        audioSource.Stop();
        audioSource.clip = clip;
        audioSource.Play();
    }


    /// <summary>Hook this up to each component's grab/select event, with that component's id as the static argument.</summary>
    public void OnComponentGrabbed(string componentId)
    {
        if (!_lookup.TryGetValue(componentId, out ExplorationComponentData data))
        {
            Debug.LogWarning($"ExplorationController: no data found for component id '{componentId}'.");
            return;
        }

        // In case one of this id's objects is being re-grabbed while still floating from a previous release.
        foreach (GameObject targetObject in data.targetObjects)
        {
            if (targetObject != null) StopFloatingFor(targetObject.transform);
        }

        if (_rendererCaches.TryGetValue(componentId, out List<RendererCache> caches))
        {
            RestoreOriginalMaterials(caches);
        }

        PlayAudio(data.audioClip);

        mainUIPanel.SetActive(false);
        if (infoUIPanel != null) infoUIPanel.SetActive(true);
        if (subheadingText != null) subheadingText.text = data.subheading;
        if (infoText != null) infoText.text = data.infoText;
    }

    /// <summary>
    /// Hook this up to each individual component's release/unselect event,
    /// with that specific GameObject (itself) as the static argument. Only
    /// the released object floats - not the other objects sharing its id
    /// (e.g. the other 3 Proximity Sensors).
    /// </summary>
    public void OnComponentReleased(GameObject target)
    {
        if (target == null) return;

        StopFloatingFor(target.transform);
        _floatRoutines[target.transform] = StartCoroutine(FloatRoutine(target.transform));
    }

    private IEnumerator FloatRoutine(Transform target)
    {
        Vector3 basePosition = target.position;
        float phase = Random.value * Mathf.PI * 2f;

        while (true)
        {
            float offset = Mathf.Sin(Time.time * floatSpeed + phase) * floatAmplitude;
            target.position = basePosition + Vector3.up * offset;
            yield return null;
        }
    }

    private void StopFloatingFor(Transform target)
    {
        if (!_floatRoutines.TryGetValue(target, out Coroutine routine)) return;

        if (routine != null) StopCoroutine(routine);
        _floatRoutines.Remove(target);
    }

    private void StopAllFloating()
    {
        foreach (Coroutine routine in _floatRoutines.Values)
        {
            if (routine != null) StopCoroutine(routine);
        }
        _floatRoutines.Clear();
    }

    /// <summary>Hook this up to the Reset button's OnClick. Animates each target object back to its starting pose.</summary>
    public void ResetTargetPositions()
    {
        StopAllFloating();
        if (_resetRoutine != null) StopCoroutine(_resetRoutine);
        _resetRoutine = StartCoroutine(ResetTargetPositionsRoutine());
        infoUIPanel.SetActive(false);
    }

    private IEnumerator ResetTargetPositionsRoutine()
    {
        // Scale correction and the cylindrical base's rotation reset happen
        // instantly, up front - only then do the components animate back to
        // their original position/rotation.
        SnapTargetScales();
        ResetCylindricalBaseRotation();

        int count = _originalTransforms.Count;
        var startPositions = new Vector3[count];
        var startRotations = new Quaternion[count];
        for (int i = 0; i < count; i++)
        {
            Transform t = _originalTransforms[i].transform;
            if (t == null) continue;
            startPositions[i] = t.localPosition;
            startRotations[i] = t.localRotation;
        }

        float time = 0f;
        while (time < resetDuration)
        {
            time += Time.deltaTime;
            float p = resetCurve.Evaluate(Mathf.Clamp01(time / resetDuration));
            for (int i = 0; i < count; i++)
            {
                Transform t = _originalTransforms[i].transform;
                if (t == null) continue;
                t.localPosition = Vector3.LerpUnclamped(startPositions[i], _originalTransforms[i].originalLocalPosition, p);
                t.localRotation = Quaternion.SlerpUnclamped(startRotations[i], _originalTransforms[i].originalLocalRotation, p);
            }
            yield return null;
        }

        SnapTargetTransforms();
        _resetRoutine = null;
    }

    private void SnapTargetTransforms()
    {
        foreach ((Transform transform, Vector3 originalLocalPosition, Quaternion originalLocalRotation, Vector3 originalLocalScale) in _originalTransforms)
        {
            if (transform == null) continue;
            transform.localPosition = originalLocalPosition;
            transform.localRotation = originalLocalRotation;
        }
    }

    private void SnapTargetScales()
    {
        foreach ((Transform transform, Vector3 originalLocalPosition, Quaternion originalLocalRotation, Vector3 originalLocalScale) in _originalTransforms)
        {
            if (transform == null) continue;
            transform.localScale = originalLocalScale;
        }
    }

    private void ResetCylindricalBaseRotation()
    {
        if (cylindricalBaseTransform != null)
        {
            cylindricalBaseTransform.localRotation = Quaternion.Euler(cylindricalBaseStartRotation);
        }
    }

    /// <summary>Hook this up to the Back button's OnClick.</summary>
    public void GoBackToStartup()
    { 
       
        StopAllFloating();
        if (_resetRoutine != null)
        {
            StopCoroutine(_resetRoutine);
            _resetRoutine = null;
        }
        SnapTargetScales();
        ResetCylindricalBaseRotation();
        SnapTargetTransforms();

        if (_rendererCaches != null)
        {
            foreach (List<RendererCache> caches in _rendererCaches.Values)
            {
                ApplyHighlightFromCache(caches);
            }
        }

        gameObject.SetActive(false);
        if (startupModule != null) startupModule.SetActive(true);
        infoUIPanel.SetActive(false);
         mainUIPanel.SetActive(true);
    }

    /// <summary>
    /// Caches each renderer's current materials (including child renderers)
    /// and replaces any Lit-style slot (one exposing _BaseColor) with the
    /// shared highlightMaterial as-is.
    /// </summary>
    private List<RendererCache> CacheAndApplyHighlight(GameObject target)
    {
        var caches = new List<RendererCache>();

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            caches.Add(new RendererCache { renderer = renderer, originalMaterials = renderer.sharedMaterials });
        }

        ApplyHighlightFromCache(caches);
        return caches;
    }

    private void ApplyHighlightFromCache(List<RendererCache> caches)
    {
        if (highlightMaterial == null)
        {
            Debug.LogError("ExplorationController: Highlight Material is not assigned.");
            return;
        }

        foreach (RendererCache cache in caches)
        {
            if (cache.renderer == null) continue;

            Material[] highlightMats = new Material[cache.originalMaterials.Length];
            for (int i = 0; i < cache.originalMaterials.Length; i++)
            {
                Material original = cache.originalMaterials[i];
                highlightMats[i] = (original == null || !original.HasProperty("_BaseColor"))
                    ? original
                    : highlightMaterial;
            }

            cache.renderer.materials = highlightMats;
        }
    }

    private static void RestoreOriginalMaterials(List<RendererCache> caches)
    {
        foreach (RendererCache cache in caches)
        {
            if (cache.renderer != null) cache.renderer.materials = cache.originalMaterials;
        }
    }
}
