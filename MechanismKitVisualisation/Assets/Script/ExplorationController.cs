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
    }

    private class RendererCache
    {
        public Renderer renderer;
        public Material[] originalMaterials;
    }

    [SerializeField] private List<ExplorationComponentData> components;
    [SerializeField] private Material highlightMaterial;

    [Header("Shared Info UI")]
    [SerializeField] private GameObject infoUIPanel;
    [SerializeField] private TextMeshProUGUI subheadingText;
    [SerializeField] private TextMeshProUGUI infoText;

    [Header("Back / Reset")]
    [SerializeField] private GameObject startupModule;
    [SerializeField] private float resetDuration = 1f;
    [SerializeField] private AnimationCurve resetCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Idle Float (after release)")]
    [SerializeField] private float floatAmplitude = 0.02f;
    [SerializeField] private float floatSpeed = 1f;

    private Dictionary<string, ExplorationComponentData> _lookup;
    private Dictionary<string, List<RendererCache>> _rendererCaches;
    private readonly List<(Transform transform, Vector3 originalPosition, Quaternion originalRotation)> _originalTransforms =
        new List<(Transform, Vector3, Quaternion)>();
    private readonly Dictionary<string, List<Coroutine>> _floatRoutines = new Dictionary<string, List<Coroutine>>();

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
                _originalTransforms.Add((targetObject.transform, targetObject.transform.position, targetObject.transform.rotation));
                caches.AddRange(CacheAndApplyHighlight(targetObject));
            }
        }

        if (infoUIPanel != null) infoUIPanel.SetActive(false);
    }

    /// <summary>Hook this up to each component's grab/select event, with that component's id as the static argument.</summary>
    public void OnComponentGrabbed(string componentId)
    {
        if (!_lookup.TryGetValue(componentId, out ExplorationComponentData data))
        {
            Debug.LogWarning($"ExplorationController: no data found for component id '{componentId}'.");
            return;
        }

        // In case it's being re-grabbed while still floating from a previous release.
        StopFloating(componentId);

        if (_rendererCaches.TryGetValue(componentId, out List<RendererCache> caches))
        {
            RestoreOriginalMaterials(caches);
        }

        if (infoUIPanel != null) infoUIPanel.SetActive(true);
        if (subheadingText != null) subheadingText.text = data.subheading;
        if (infoText != null) infoText.text = data.infoText;
    }

    /// <summary>Hook this up to each component's release/unselect event, with that component's id as the static argument.</summary>
    public void OnComponentReleased(string componentId)
    {
        if (!_lookup.TryGetValue(componentId, out ExplorationComponentData data))
        {
            Debug.LogWarning($"ExplorationController: no data found for component id '{componentId}'.");
            return;
        }

        StopFloating(componentId);

        var routines = new List<Coroutine>();
        foreach (GameObject targetObject in data.targetObjects)
        {
            if (targetObject == null) continue;
            routines.Add(StartCoroutine(FloatRoutine(targetObject.transform)));
        }
        _floatRoutines[componentId] = routines;
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

    private void StopFloating(string componentId)
    {
        if (!_floatRoutines.TryGetValue(componentId, out List<Coroutine> routines)) return;

        foreach (Coroutine routine in routines)
        {
            if (routine != null) StopCoroutine(routine);
        }
        _floatRoutines.Remove(componentId);
    }

    private void StopAllFloating()
    {
        foreach (List<Coroutine> routines in _floatRoutines.Values)
        {
            foreach (Coroutine routine in routines)
            {
                if (routine != null) StopCoroutine(routine);
            }
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
        int count = _originalTransforms.Count;
        var startPositions = new Vector3[count];
        var startRotations = new Quaternion[count];
        for (int i = 0; i < count; i++)
        {
            Transform t = _originalTransforms[i].transform;
            if (t == null) continue;
            startPositions[i] = t.position;
            startRotations[i] = t.rotation;
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
                t.position = Vector3.LerpUnclamped(startPositions[i], _originalTransforms[i].originalPosition, p);
                t.rotation = Quaternion.SlerpUnclamped(startRotations[i], _originalTransforms[i].originalRotation, p);
            }
            yield return null;
        }

        SnapTargetTransforms();
        _resetRoutine = null;
    }

    private void SnapTargetTransforms()
    {
        foreach ((Transform transform, Vector3 originalPosition, Quaternion originalRotation) in _originalTransforms)
        {
            if (transform == null) continue;
            transform.position = originalPosition;
            transform.rotation = originalRotation;
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
