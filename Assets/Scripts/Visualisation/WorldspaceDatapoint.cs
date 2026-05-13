using Unity.Mathematics;
using UnityEngine;

public class WorldspaceDatapoint : MonoBehaviour
{
    public DatapointVisualizer _parent;

    [Header("Data")]
    public double3 _location;
    public int _time_ms;
    private double _evaluatedData;

    private void Awake()
    {
    }

    public void RefreshData()
    {
        if (_parent == null) return;
        _parent._data.EvaluateMeasurement(_time_ms, _parent._visualization._activeMeasureCategory, out _evaluatedData);
    }

    public void PushData()
    {
        if (_parent == null) return;
        VisualConfiguration.SetHighlightedNode(_parent._visualization, _location, _time_ms + _parent._data._startTimestamp, _time_ms, _evaluatedData);
    }
}
