using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

[System.Serializable]
public struct MeasureProperties
{
    [JsonProperty("PrioritizeDefault")] public bool _prioritizeDefault { get; set; }
    [JsonProperty("DefaultBounds")] public double2 _defaultBounds { get; set; }
    [JsonIgnore] public double2 _runtimeBounds;
    [JsonIgnore] public bool _hasRuntimeBounds;

    public MeasureProperties(double2 defaultBounds, bool prioritizeDefault)
    {
        _prioritizeDefault = prioritizeDefault;
        _defaultBounds = defaultBounds;

        _runtimeBounds = _prioritizeDefault ? defaultBounds : double2.zero;
        _hasRuntimeBounds = false;
    }

    public static void CheckRuntimeBounds(ref MeasureProperties properties, double value)
    {
        if (properties._prioritizeDefault) return;

        if (properties._hasRuntimeBounds) properties._runtimeBounds = new double2(math.min(properties._runtimeBounds.x, value), math.max(properties._runtimeBounds.y, value));
        else
        {
            properties._runtimeBounds = value;
            properties._hasRuntimeBounds = true;
        }
    }
}

[CreateAssetMenu(fileName = "New Visualization Configuration", menuName = "Cansat/Visualization Configuration")]
public class VisualConfiguration : ScriptableObject
{
    [Header("Visualization")]
    public List<Gradient> _categoryColors;
    public List<double2> _categoryBounds;

    public TextAsset _visualizationProperties;
    public TextAsset _visualizationColors;
    public IReadOnlyDictionary<MeasureMappings, MeasureProperties> _visualizationPropertyMapping;
    public IReadOnlyDictionary<MeasureMappings, Gradient> _visualizationColorMapping = new Dictionary<MeasureMappings, Gradient>();

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

    private struct JsonColor
    {
        public float r;
        public float g;
        public float b;
        public float a;

        public JsonColor(Color c)
        {
            r = c.r; g = c.g; b = c.b; a = c.a;
        }

        public Color ToColor() => new Color(r, g, b, a);
    }

    public void LoadVisualCustomization()
    {
        try
        {
            _visualizationPropertyMapping = JsonConvert.DeserializeObject<Dictionary<MeasureMappings, MeasureProperties>>(_visualizationProperties.text);

            Dictionary<MeasureMappings, JsonColor[]> deserializedColorMap = JsonConvert.DeserializeObject<Dictionary<MeasureMappings, JsonColor[]>>(_visualizationColors.text);
            Dictionary<MeasureMappings, Gradient> unityColorMap = new Dictionary<MeasureMappings, Gradient>();
            foreach (var mapping in deserializedColorMap)
            {
                Gradient g = new Gradient() { colorKeys = new GradientColorKey[mapping.Value.Length], alphaKeys = new GradientAlphaKey[1] { new(1, 0) } };
                float timeIncrement = 1f / (mapping.Value.Length - 1);

                for (int i = 0; i < mapping.Value.Length; i++) g.colorKeys[i] = new(mapping.Value[i].ToColor(), timeIncrement * i);
                unityColorMap[mapping.Key] = g;
            }

            _visualizationColorMapping = unityColorMap;

            Debug.Log("Successfully loaded data visualization customization.");
        }
        catch
        {
            Debug.LogError("Failed to load data visualization customization.");
        }
    }

    public static void SetHighlightedNode(VisualConfiguration instance, double3 location, int time, int normalizedTime, double evaluated)
    {
        instance._highlightedNodeLocation = $"Latitude: {location.x}°\nLongitude: {location.y}°\n{location.z}m above sea level";

        instance._highlightedNodeTime = $"(system time {TimeSpan.FromMilliseconds(time).ToString(@"h\:mm\:ss\.ff")})";
        instance._highlightedNodeNormalizedTime = $"At {TimeSpan.FromMilliseconds(normalizedTime).ToString(@"h\:mm\:ss\.ff")}";

        instance._highlightedNodeEvaluated = $"{instance._activeMeasureCategory}: {Math.Round(evaluated, 4)} {CansatDataHelpers.GetMeasureSuffix[instance._activeMeasureCategory]}";
    }
}
