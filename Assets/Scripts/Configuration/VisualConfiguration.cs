using System;
using Unity.Mathematics;
using UnityEngine;

[CreateAssetMenu(fileName = "New Visualization Configuration", menuName = "Cansat/Visualization Configuration")]
public class VisualConfiguration : ScriptableObject
{
    [Header("Configuration")]
    public MeasureMappings _activeMeasureCategory;

    [Header("Runtime")]
    public double _evaluatedActiveMeasurement;
    public DataKeyframe _evaluatedDataKey;

    [Space(10)]
    public WorldspaceDatapoint _highlightedNode = null;
    public bool _highlightedNodeLock = false;
    public Vector2 _highlightInfoPosition;

    [Multiline] public string _highlightedNodeLocation;

    public string _highlightedNodeTime;
    public string _highlightedNodeNormalizedTime;

    public string _highlightedNodeEvaluated;

    public static void SetHighlightedNode(VisualConfiguration instance, double3 location, int time, int normalizedTime, double evaluated)
    {
        instance._highlightedNodeLocation = $"Latitude: {location.x}°\nLongitude: {location.y}°\n{location.z}m above sea level";

        instance._highlightedNodeTime = $"(system time {TimeSpan.FromMilliseconds(time).ToString(@"h\:mm\:ss\.ff")})";
        instance._highlightedNodeNormalizedTime = $"At {TimeSpan.FromMilliseconds(normalizedTime).ToString(@"h\:mm\:ss\.ff")}";

        instance._highlightedNodeEvaluated = $"{instance._activeMeasureCategory}: {Math.Round(evaluated, 4)} {CansatDataHelpers.GetMeasureSuffix[instance._activeMeasureCategory]}";
    }
}
