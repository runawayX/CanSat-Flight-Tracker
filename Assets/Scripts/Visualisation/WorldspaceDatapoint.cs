using System;
using Unity.Mathematics;
using UnityEngine;

public class WorldspaceDatapoint : MonoBehaviour
{
    public WorldspaceDataPlotter _parent;

    [Header("Data")]
    public double3 _location;
    public int _time_ms;
    private double _evaluatedData;

    // Internal
    private Material _material;

    private void Awake()
    {
        _material = gameObject.GetComponent<MeshRenderer>().material;
    }

    public void RefreshData()
    {
        if (_parent == null) return;
        _parent._data.EvaluateMeasurement(_time_ms, _parent._visualization._activeMeasureCategory, out _evaluatedData);
        _material.color = _parent._visualization.EvaluateDataColor(
            CansatDataHelpers.InverseDynamicDataMappings()[_parent._visualization._activeMeasureCategory], 
            _evaluatedData);
    }

    public void PushData()
    {
        if (_parent == null) return;
        _parent._visualization.SetHighlightedNode(_location, _time_ms + _parent._data._startTimestamp, _time_ms, _evaluatedData);
    }
}
