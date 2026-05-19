using System;
using Unity.Mathematics;
using UnityEngine;

public class WorldspaceDatapoint : MonoBehaviour
{
    public WorldspaceDataPlotter _parent;

    [Header("Appearance")]
    [SerializeField] private Vector3 _normalScale;
    [SerializeField] private Vector3 _hoveredScale;

    [SerializeField] private Vector2 _alphaStates;

    [Header("Data")]
    public double3 _location;
    public int _time_ms;
    private double _evaluatedData;

    // Internal
    private Material _material;
    private bool _hovered = false;

    private void Awake()
    {
        _material = gameObject.GetComponent<MeshRenderer>().material;

        SetHighlighted(false);
    }

    public void RefreshData()
    {
        if (_parent == null) return;
        _parent._data.EvaluateMeasurement(_time_ms, _parent._visualization._activeMeasureCategory, out _evaluatedData);
        _material.color = _parent._visualization.EvaluateDataColor(
            CansatDataHelpers.InverseDynamicDataMappings()[_parent._visualization._activeMeasureCategory], 
            _evaluatedData);

        ApplyHighlighted();
    }

    public void PushData()
    {
        if (_parent == null) return;
        _parent._visualization.SetHighlightedNodeInfo(_location, _time_ms + _parent._data._startTimestamp, _time_ms, _evaluatedData);
    }

    public void SetHighlighted(bool highlighted)
    {
        _hovered = highlighted;
        ApplyHighlighted();
    }

    private void ApplyHighlighted()
    {
        if (_hovered)
        {
            transform.localScale = _hoveredScale;
            _material.color = new Color(_material.color.r, _material.color.g, _material.color.b, _alphaStates.y);
        }
        else
        {
            transform.localScale = _normalScale;
            _material.color = new Color(_material.color.r, _material.color.g, _material.color.b, _alphaStates.x);
        }
    }
}
