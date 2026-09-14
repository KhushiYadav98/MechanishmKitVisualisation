using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drives the mechanism kit exploration module: each grabbable component wires
/// its grab/select event to OnComponentGrabbed(id), passing its own id as a
/// static argument in the Inspector. This looks up that id in the Components
/// list and shows the matching info UI (hiding whichever one was shown
/// before). More per-component behavior (audio, etc.) can be added to
/// ExplorationComponentData later.
/// </summary>
public class ExplorationController : MonoBehaviour
{
    [System.Serializable]
    public class ExplorationComponentData
    {
        public string id;
        public GameObject infoUI;
    }

    [SerializeField] private List<ExplorationComponentData> components;

    private Dictionary<string, ExplorationComponentData> _lookup;
    private GameObject _activeUI;

    private void Awake()
    {
        _lookup = new Dictionary<string, ExplorationComponentData>();

        foreach (ExplorationComponentData data in components)
        {
            if (data == null || string.IsNullOrEmpty(data.id)) continue;

            if (!_lookup.ContainsKey(data.id))
            {
                _lookup.Add(data.id, data);
            }

            if (data.infoUI != null) data.infoUI.SetActive(false);
        }
    }

    /// <summary>Hook this up to each component's grab/select event, with that component's id as the static argument.</summary>
    public void OnComponentGrabbed(string componentId)
    {
        if (!_lookup.TryGetValue(componentId, out ExplorationComponentData data))
        {
            Debug.LogWarning($"ExplorationController: no data found for component id '{componentId}'.");
            return;
        }

        if (_activeUI != null && _activeUI != data.infoUI)
        {
            _activeUI.SetActive(false);
        }

        if (data.infoUI != null)
        {
            data.infoUI.SetActive(true);
            _activeUI = data.infoUI;
        }
    }
}
