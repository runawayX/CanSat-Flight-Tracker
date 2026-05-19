using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using Unity.Mathematics;
using UnityEngine;

[CreateAssetMenu(fileName = "New Visualization Configuration", menuName = "Cansat/Visualization Configuration")]
public class VisualConfiguration : ScriptableObject
{
    [Header("Visualization")]
    public TextAsset _visualizationColors;
    private IReadOnlyDictionary<string, Gradient> _visualizationColorMapping;

    [Header("Configuration")]
    public List<string> _measureCategories;
    public int _activeMeasureCategory;

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

    private static readonly GradientAlphaKey[] _internalAlphaMultiplier = new GradientAlphaKey[2] { new(1, 0), new(1, 1) };

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
            Dictionary<string, JsonColor[]> deserializedColorMap = JsonConvert.DeserializeObject<Dictionary<string, JsonColor[]>>(_visualizationColors.text, CansatDataHelpers.SerializeConfig);
            Dictionary<string, Gradient> unityColorMap = new Dictionary<string, Gradient>();

            foreach (var mapping in deserializedColorMap)
            {
                Gradient g = new Gradient();
                GradientColorKey[] colorPoints = new GradientColorKey[mapping.Value.Length];
                GradientAlphaKey[] alphaPoints = new GradientAlphaKey[mapping.Value.Length];

                float timeIncrement = 1f / (mapping.Value.Length - 1);

                for (int i = 0; i < mapping.Value.Length; i++)
                {
                    colorPoints[i] = new(mapping.Value[i].ToColor(), timeIncrement * i);
                    alphaPoints[i] = new(mapping.Value[i].a, timeIncrement * i);
                }

                g.SetKeys(colorPoints, _internalAlphaMultiplier);
                unityColorMap[mapping.Key] = g;
            }

            _visualizationColorMapping = unityColorMap;
            Debug.Log($"Successfully loaded data visualization customization.");
        }
        catch
        {
            Debug.LogError("Failed to load data visualization customization.");
        }
    }

    public void SetHighlightedNodeInfo(double3 location, int time, int normalizedTime, double evaluated)
    {
        _measureCategories = CansatDataHelpers.InverseDynamicDataMappings();

        _highlightedNodeLocation = $"Longitude: {location.x}°\nLatitude: {location.y}°\n{location.z}m above sea level";

        _highlightedNodeTime = $"(system time {TimeSpan.FromMilliseconds(time).ToString(@"h\:mm\:ss\.ff")})";
        _highlightedNodeNormalizedTime = $"At {TimeSpan.FromMilliseconds(normalizedTime).ToString(@"h\:mm\:ss\.ff")}";

        if (CansatDataHelpers.MeasurePropertyMap.TryGetValue(CansatDataHelpers.InverseDynamicDataMappings()[_activeMeasureCategory], out MeasureProperties p)) 
            _highlightedNodeEvaluated = $"{p._name ?? CansatDataHelpers.InverseDynamicDataMappings()[_activeMeasureCategory]}: {Math.Round(evaluated, 4)} {p._suffix}";
        else
            _highlightedNodeEvaluated = $"Undefined Parameter: {Math.Round(evaluated, 4)}";
    }

    public Color EvaluateDataColor(string category, double value)
    {
        if (!CansatDataHelpers.MeasurePropertyMap.ContainsKey(category))
        {
            Debug.LogError($"Trying to access undefined data category \"{category}\".");
            return Color.magenta;
        }

        float lerpRatio = CansatDataHelpers.GetMeasureInRange(value, category);

        if (_visualizationColorMapping.TryGetValue(category, out Gradient g)) return g.Evaluate(lerpRatio);

        //Debug.LogWarning($"Fallback to default for \"{category}\" in color eval.");
        return _visualizationColorMapping["Default"].Evaluate(lerpRatio);
    }
}
