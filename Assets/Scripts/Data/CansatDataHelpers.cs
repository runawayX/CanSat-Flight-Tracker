using CesiumForUnity;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public readonly struct GeoTransform
{
    public readonly int normalizedTime;
    public readonly double3 lonLatAlt;
    public readonly Quaternion localRotation;

    public static GeoTransform identity = new GeoTransform(-1, double3.zero, Quaternion.identity);

    public GeoTransform(int normalizedTime, double3 lonLatAlt, Quaternion localRotation)
    {
        this.normalizedTime = normalizedTime;
        this.lonLatAlt = lonLatAlt;
        this.localRotation = localRotation;
    }
}

/// <summary>
/// Common Data Mappings
/// </summary>
public struct CDM
{
    /// <summary>
    /// Number of quick mapped categories
    /// </summary>
    public static readonly int Mapped = 7;

    public static int t = -1;
    public static int lat = -1, lon = -1, alt = -1;
    /// <summary>
    /// Globe Separation (WGS84 conversion)
    /// </summary>
    public static int gs = -1;
    public static int hdg = -1, pitch = -1, roll = -1;

    public static void Remap(string key, int index)
    {
        switch (key)
        {
            case "t": t = index; break;
            case "lat": lat = index; break;
            case "lon": lon = index; break;
            case "alt": alt = index; break;
            case "gs": gs = index; break;
            case "hdg": hdg = index; break;
            case "pitch": pitch = index; break;
            case "roll": roll = index; break;
        }
    }

    public static void Clear()
    {
        t = -1;
        lat = -1; lon = -1; alt = -1;
        gs = -1;
        hdg = -1; pitch = -1; roll = -1;
    }

    public static bool IsValid(bool msl = false)
    {
        return t != -1 && lat != -1 && lon != -1 && alt != -1 && (!msl || gs != -1) && hdg != -1 && pitch != -1 && roll != -1;
    }

    public static bool IsValidGPS(bool msl = false)
    {
        return t != -1 && lat != -1 && lon != -1 && alt != -1 && (!msl || gs != -1);
    }

    public static bool IsValidRotation()
    {
        return t != -1 && hdg != -1 && pitch != -1 && roll != -1;
    }
}

[System.Serializable]
public class MeasureProperties
{
    [JsonProperty("DisplayName")] public string _name { get; set; }
    [JsonProperty("DisplaySuffix")] public string _suffix { get; set; }
    [JsonProperty("PrioritizeDefault")] public bool _prioritizeDefault { get; set; }
    [JsonProperty("DefaultMin")] public double _defaultMin { get; set; } = 0;
    [JsonProperty("DefaultMax")] public double _defaultMax { get; set; } = 0;
    [JsonIgnore] public double2 _runtimeBounds = double2.zero;
    [JsonIgnore] public bool _hasRuntimeBounds;

    public MeasureProperties(double2 defaultBounds, bool prioritizeDefault)
    {
        _name = null;
        _suffix = "";

        _prioritizeDefault = prioritizeDefault;
        _defaultMin = defaultBounds.x;
        _defaultMax = defaultBounds.y;

        _runtimeBounds = _prioritizeDefault ? defaultBounds : double2.zero;
        _hasRuntimeBounds = false;
    }

    public double2 GetBounds()
    {
        if (_prioritizeDefault || !_hasRuntimeBounds) return new double2(_defaultMin, _defaultMax);
        else return _runtimeBounds;
    }

    public double GetBoundMagnitude()
    {
        if (_prioritizeDefault || !_hasRuntimeBounds) return _defaultMax - _defaultMin;
        else return _runtimeBounds.y - _runtimeBounds.x;
    }

    public void CheckRuntimeBounds(double value)
    {
        if (_prioritizeDefault || double.IsNaN(value)) return;

        if (_hasRuntimeBounds) _runtimeBounds = new double2(math.min(_runtimeBounds.x, value), math.max(_runtimeBounds.y, value));
        else
        {
            _runtimeBounds = value;
            _hasRuntimeBounds = true;
        }
    }
}

public static class CansatDataHelpers
{
    public static int LerpInt(int start, int end, float ratio)
    {
        return start + (int) Math.Round((end - start) * ratio);
    }

    public static double3 LerpDouble3(double3 start, double3 end, float ratio)
    {
        return start + (end - start) * ratio;
    }

    /// <summary>
    /// Moves the provided globe-anchored transform by a meter-based offset north/east
    /// </summary>
    /// <param name="obj">Globe-anchored Transform to move</param>
    /// <param name="v">Meter-based offset</param>
    public static void TranslateGPS(CesiumGlobeAnchor obj, Vector2 v)
    {
        double longitudeRad = math.radians(obj.longitudeLatitudeHeight.x);
        double latitudeRad = math.radians(obj.longitudeLatitudeHeight.y);

        // east
        double3 east = new double3(
            -math.sin(longitudeRad),
             math.cos(longitudeRad),
             0.0);

        // north
        double3 north = new double3(
            -math.sin(latitudeRad) * math.cos(longitudeRad),
            -math.sin(latitudeRad) * math.sin(longitudeRad),
             math.cos(latitudeRad));

        double3 offset = east * v.x + north * v.y;
        obj.positionGlobeFixed += offset;
    }

    public static Dictionary<string, int> DynamicDataMappings;

    public static bool _hasUpdatedMappings = true;
    private static List<string> _inverseMappingsCache;

    public static List<string> InverseDynamicDataMappings()
    {
        if (!_hasUpdatedMappings) return _inverseMappingsCache;

        string[] tempCategories = new string[DynamicDataMappings.Count];
        foreach (var mapping in DynamicDataMappings)
        {
            tempCategories[mapping.Value] = mapping.Key;
        }

        _inverseMappingsCache = new List<string>(tempCategories);

        _hasUpdatedMappings = false;
        return _inverseMappingsCache;
    }

    public static float GetMeasureInRange(double value, string category)
    {
        if (!double.IsNaN(value))
        {
            double2 bound = MeasurePropertyMap[category].GetBounds();
            if (bound.y != bound.x) return Mathf.Clamp01((float) ((value - bound.x) / (bound.y - bound.x)));
        }

        return 0;
    }

    public static Dictionary<string, MeasureProperties> MeasurePropertyMap;

    public static void LoadMeasureProperties(TextAsset propertiesAsset)
    {
        try
        {
            MeasurePropertyMap = JsonConvert.DeserializeObject<Dictionary<string, MeasureProperties>>(propertiesAsset.text);
        }
        catch (Exception e)
        {
            MeasurePropertyMap = new Dictionary<string, MeasureProperties>();
            Debug.LogError($"Failed to load measure properties ({e.Message})");
        }
    }

    public static void ResetMeasureBounds()
    {
        foreach (MeasureProperties measure in MeasurePropertyMap.Values) measure._hasRuntimeBounds = false;
    }

    public static readonly JsonSerializerSettings SerializeConfig = new JsonSerializerSettings()
    {
        FloatParseHandling = FloatParseHandling.Double,
        FloatFormatHandling = FloatFormatHandling.String
    };
}
