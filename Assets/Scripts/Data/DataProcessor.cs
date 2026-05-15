using C5;
using CesiumForUnity;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using TMPro;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.UIElements;

using static CansatDataHelpers;

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
    public static int t = -1;
    public static int lat = -1, lon = -1, alt = -1;
    public static int hdg = -1, pitch = -1, roll = -1;

    public static void Remap(string key, int index)
    {
        switch (key)
        {
            case "t": t = index; break;
            case "lat": lat = index; break;
            case "lon": lon = index; break;
            case "alt": alt = index; break;
            case "hdg": hdg = index; break;
            case "pitch": pitch = index; break;
            case "roll": roll = index; break;
        }
    }
}

[System.Serializable]
public struct MeasureProperties
{
    [JsonProperty("DisplayName")] public string _name { get; set; }
    [JsonProperty("DisplaySuffix")] public string _suffix { get; set; }
    [JsonProperty("PrioritizeDefault")] public bool _prioritizeDefault { get; set; }
    [JsonProperty("DefaultBounds")] public double2 _defaultBounds { get; set; }
    [JsonIgnore] public double2 _runtimeBounds;
    [JsonIgnore] public bool _hasRuntimeBounds;

    public MeasureProperties(double2 defaultBounds, bool prioritizeDefault)
    {
        _name = "Undefined";
        _suffix = "";

        _prioritizeDefault = prioritizeDefault;
        _defaultBounds = defaultBounds;

        _runtimeBounds = _prioritizeDefault ? defaultBounds : double2.zero;
        _hasRuntimeBounds = false;
    }

    public double2 GetBounds()
    {
        if (_prioritizeDefault || !_hasRuntimeBounds) return _defaultBounds;
        else return _runtimeBounds;
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

public class DataProcessor : MonoBehaviour
{
    [Header("Components")]
    public DataConfiguration _config;
    //[SerializeField] private CesiumGeoreference _map;

    [Header("Events")]
    public UnityEvent<bool> _onNewPacket;
    public UnityEvent _onBadPacket;

    public UnityEvent _onCompleteDatabase;
    public UnityEvent _onClearDatabase;

    [Header("Editor")]
    public bool _logAddPacket = false;
    public bool _logSerialRaw = false;

    // Internal
    public int _startTimestamp { get; private set; } = 0;
    public int _endTimestamp { get; private set; } = 0;
    private List<TreeDictionary<int, double>> _dataKeys;

    public bool _hasData { get; private set; } = false;

    public DataReceiver<bool> _recordedDataFeed { get; private set; }
    public DataReceiver<SerialPortHandler.StatusCode> _liveDataFeed { get; private set; }

    private void Awake()
    {
        _dataKeys = new List<TreeDictionary<int, double>>();

        DynamicDataMappings = new Dictionary<string, int>();
    }

    private void Start()
    {
    }

    private void Update()
    {
        if (_liveDataFeed != null)
        {
            _liveDataFeed.Read();
            _config._serialStatus = _liveDataFeed.Status();
        }
    }

    private void OnDisable()
    {
        StopSerial();
    }

    #region Actions
    public void ReadRecording()
    {
        ClearData();

        _recordedDataFeed = new JsonFileDataReader(this, _config._path);
        _recordedDataFeed.Begin();
    }

    public void StartSerial()
    {
        if (_liveDataFeed == null)
        {
            _liveDataFeed = new SerialPortDataReceiver(this, _logSerialRaw);
            Debug.Log("Created new Serial Port Data Receiver.");
        }

        _liveDataFeed.Begin();
        Debug.Log($"External serial port is {_liveDataFeed.Status()}");
    }

    public void StopSerial()
    {
        _liveDataFeed?.End();
        _liveDataFeed = null;

        _config._serialStatus = SerialPortHandler.StatusCode.DISABLED;
    }

    public void CompleteData()
    {
        _onCompleteDatabase.Invoke();
    }

    public void ExportData()
    {
        if (!_hasData)
        {
            Debug.Log("No data to export.");
            return;
        }

        DataExporter<DataKeyframe> export = new JsonFileDataExporter(_dataKeys, _config._exportPath);

        try
        {
            export.Export();
            Debug.Log($"Successfully exported data to {_config._exportPath}");
        } catch (Exception ex) {
            Debug.LogWarning($"Failed to export data - {ex.ToString()}");
        }
    }

    public void ClearData()
    {
        Debug.Log("Clearing data...");

        _hasData = false;
        _dataKeys.Clear();

        _onClearDatabase.Invoke();
    }
    #endregion

    #region Data Processing
    public void ProcessDatakey(Dictionary<string, double> packet, bool incremental)
    {
        //if (_logAddPacket) Debug.Log($"Processing {p}...");

        int t = (int) packet["t"];

        foreach (var sub in packet)
        {
            if (!DynamicDataMappings.ContainsKey(sub.Key))
            {
                DynamicDataMappings.Add(sub.Key, _dataKeys.Count);
                CDM.Remap(sub.Key, DynamicDataMappings[sub.Key]);

                _dataKeys.Add(new TreeDictionary<int, double>());
            }

            _dataKeys[DynamicDataMappings[sub.Key]].Add(t, sub.Value);

            _startTimestamp = Math.Min(_startTimestamp, t);
            _endTimestamp = Math.Max(_endTimestamp, t);
        }

        _hasData = true;
        _onNewPacket.Invoke(incremental);
    }

    public void WarnBadDatakey()
    {
        _onBadPacket.Invoke();
    }
    #endregion

    #region Accessing & Evaluation
    /// <summary>
    /// Gives the total time of data tracking
    /// </summary>
    /// <returns></returns>
    public int TotalTime()
    {
        if (_hasData) return _endTimestamp - _startTimestamp;
        else return 0;
    }

    public bool IsListeningSerial()
    {
        if (_liveDataFeed != null) return (_liveDataFeed as SerialPortDataReceiver).Status() == SerialPortHandler.StatusCode.RUNNING; // unsafe
        else return false;
    }

    /// <summary>
    /// Gives interpolated GPS coordinates and altitude at desired time-point
    /// </summary>
    /// <param name="time_ms">Normalized time-point (in milliseconds) at which to evaluate</param>
    /// <param name="evaluated">Longitude, Latitude, Altitude</param>
    /// <returns>Whether the evaluation succeeded (false if no data)</returns>
    public bool EvaluateLocationGPS(int time_ms, out double3 evaluated)
    {
        if (!_hasData)
        {
            evaluated = double3.zero;
            return false;
        }

        time_ms += _startTimestamp;
        int clamp_time_ms = Math.Clamp(time_ms, _dataKeys[CDM.lat].First().Key, _dataKeys[CDM.lat].Last().Key);

        // taking lon as base as gps data is uniform
        int sT = _dataKeys[CDM.lon].WeakPredecessor(clamp_time_ms).Key;
        int eT = _dataKeys[CDM.lon].WeakSuccessor(clamp_time_ms).Key;

        if (sT == eT)
        {
            evaluated = new double3(
                _dataKeys[CDM.lon][sT],
                _dataKeys[CDM.lat][sT],
                _dataKeys[CDM.alt][sT]);
            return true;
        }

        float lerpRatio = (float) (time_ms - sT) / (eT - sT);

        //Debug.Log($"Lerp {lerpRatio} ({time_ms}ms)\nFrom {sK.t}ms to {eK.t}ms");

        evaluated = new double3(
            math.lerp(_dataKeys[CDM.lon][sT], _dataKeys[CDM.lon][eT], lerpRatio),
            math.lerp(_dataKeys[CDM.lat][sT], _dataKeys[CDM.lat][eT], lerpRatio),
            math.lerp(_dataKeys[CDM.alt][sT], _dataKeys[CDM.alt][eT], lerpRatio));

        return true;
    }

    /// <summary>
    /// Gives interpolated Unity-based rotation vector at desired time-point
    /// </summary>
    /// <param name="time_ms">Normalized time-point (in milliseconds) at which to evaluate</param>
    /// <param name="evaluated">Composited Pitch, Yaw, Roll</param>
    /// <returns>Whether the evaluation succeeded (false if no data)</returns>
    public bool EvaluateRotation(int time_ms, out Quaternion evaluated)
    {
        if (!_hasData)
        {
            evaluated = Quaternion.identity;
            return false;
        }

        time_ms += _startTimestamp;
        int clamp_time_ms = Math.Clamp(time_ms, _dataKeys[CDM.hdg].First().Key, _dataKeys[CDM.hdg].Last().Key);

        // taking hdg as base as rotation data is uniform
        int sT = _dataKeys[CDM.hdg].WeakPredecessor(clamp_time_ms).Key;
        int eT = _dataKeys[CDM.hdg].WeakSuccessor(clamp_time_ms).Key;

        if (sT == eT)
        {
            evaluated = Quaternion.AngleAxis((float) _dataKeys[CDM.pitch][sT], Vector3.right) 
                * Quaternion.AngleAxis((float) _dataKeys[CDM.hdg][sT], Vector3.up) 
                * Quaternion.AngleAxis((float) _dataKeys[CDM.roll][sT], Vector3.forward);
            return true;
        }

        float lerpRatio = (float) (time_ms - sT) / (eT - sT);
        Vector3 ir = Vector3.Lerp(new Vector3((float) _dataKeys[CDM.pitch][sT], (float) _dataKeys[CDM.hdg][sT], (float) _dataKeys[CDM.roll][sT]),
            new Vector3((float) _dataKeys[CDM.pitch][eT], (float) _dataKeys[CDM.hdg][eT], (float) _dataKeys[CDM.roll][eT]), 
            lerpRatio);

        evaluated = Quaternion.AngleAxis(ir.x, Vector3.right) * Quaternion.AngleAxis(ir.y, Vector3.up) * Quaternion.AngleAxis(ir.z, Vector3.forward);
        return true;
    }

    /// <summary>
    /// Gives interpolated GPS coordinates and Unity-Based rotation at desired time-point (use only when both location and rotation are logged uniformly)
    /// </summary>
    /// <param name="time_ms">Normalized time-point (in milliseconds) at which to evaluate</param>
    /// <param name="evaluated">Evaluated location (lon, lat, alt) and rotation (Unity-space quaternion)</param>
    /// <returns>Whether the evaluation succeeded (false if no data)</returns>
    public bool EvaluateTransform(int time_ms, out GeoTransform evaluated)
    {
        if (!_hasData)
        {
            evaluated = GeoTransform.identity;
            return false;
        }

        time_ms += _startTimestamp;
        int clamp_time_ms = Math.Clamp(time_ms, _dataKeys[CDM.lat].First().Key, _dataKeys[CDM.lat].Last().Key);

        // taking lon as base as GeoTransform sub-data is assumed uniform
        int sT = _dataKeys[CDM.lon].WeakPredecessor(clamp_time_ms).Key;
        int eT = _dataKeys[CDM.lon].WeakSuccessor(clamp_time_ms).Key;

        double3 p;
        Quaternion r;

        if (sT == eT)
        {
            p = new double3(_dataKeys[CDM.lon][sT], _dataKeys[CDM.lat][sT], _dataKeys[CDM.alt][sT]);
            r = Quaternion.AngleAxis((float) _dataKeys[CDM.pitch][sT], Vector3.right)
                * Quaternion.AngleAxis((float) _dataKeys[CDM.hdg][sT], Vector3.up) 
                * Quaternion.AngleAxis((float) _dataKeys[CDM.roll][sT], Vector3.forward); ;

            evaluated = new GeoTransform(sT - _startTimestamp, p, r);
            return true;
        }

        float lerpRatio = (float) (time_ms - sT) / (eT - sT);

        p = new double3(math.lerp(_dataKeys[CDM.lon][sT], _dataKeys[CDM.lon][eT], lerpRatio), 
            math.lerp(_dataKeys[CDM.lat][sT], _dataKeys[CDM.lat][eT], lerpRatio), 
            math.lerp(_dataKeys[CDM.alt][sT], _dataKeys[CDM.alt][eT], lerpRatio));
        Vector3 ir = Vector3.Lerp(new Vector3((float) _dataKeys[CDM.pitch][sT], (float) _dataKeys[CDM.hdg][sT], (float) _dataKeys[CDM.roll][sT]),
            new Vector3((float) _dataKeys[CDM.pitch][eT], (float) _dataKeys[CDM.hdg][eT], (float) _dataKeys[CDM.roll][eT]),
            lerpRatio);

        r = Quaternion.AngleAxis(ir.x, Vector3.right) * Quaternion.AngleAxis(ir.y, Vector3.up) * Quaternion.AngleAxis(ir.z, Vector3.forward);

        evaluated = new GeoTransform(time_ms - _startTimestamp, p, r);
        return true;
    }

    public Vector3[] GetTravelPathUnitySpaceRelative(CesiumGeoreference referenceGlobe, Vector3 referencePosition)
    {
        if (!_hasData) return new Vector3[0];

        Vector3[] result = new Vector3[_dataKeys.Count];

        int r = 0;
        foreach (int t in _dataKeys[CDM.lon].Keys)
        {
            double3 unityPos = referenceGlobe.TransformEarthCenteredEarthFixedPositionToUnity(
                CesiumWgs84Ellipsoid.LongitudeLatitudeHeightToEarthCenteredEarthFixed(new double3(_dataKeys[CDM.lon][t], _dataKeys[CDM.lat][t], _dataKeys[CDM.alt][t])));

            result[r] = new Vector3((float) unityPos.x, (float) unityPos.y, (float) unityPos.z) - referencePosition;
            ++r;
        }

        return result;
    }

    public Vector3 GetLastPositionUnitySpaceRelative(CesiumGeoreference referenceGlobe, Vector3 referencePosition)
    {
        if (!_hasData) return Vector3.zero;

        double3 unityPos = referenceGlobe.TransformEarthCenteredEarthFixedPositionToUnity(
                CesiumWgs84Ellipsoid.LongitudeLatitudeHeightToEarthCenteredEarthFixed(
                    new double3(_dataKeys[CDM.lon].Values.Last(), _dataKeys[CDM.lat].Values.Last(), _dataKeys[CDM.alt].Values.Last())));

        return new Vector3((float) unityPos.x, (float) unityPos.y, (float) unityPos.z) - referencePosition;
    }

    public double3[] GetTravelPathGeo()
    {
        if (!_hasData) return new double3[0];

        double3[] result = new double3[_dataKeys.Count];

        int r = 0;
        foreach (int t in _dataKeys[CDM.lon].Keys)
        {
            result[r] = new double3(_dataKeys[CDM.lon][t], _dataKeys[CDM.lat][t], _dataKeys[CDM.alt][t]);
            ++r;
        }

        return result;
    }

    public double3 GetLastPositionGeo()
    {
        if (!_hasData) return double3.zero;

        return new double3(_dataKeys[CDM.lon].Values.Last(), _dataKeys[CDM.lat].Values.Last(), _dataKeys[CDM.alt].Values.Last());
    }

    /// <summary>
    /// Gives interpolated measurement for a specified category at the desired time-point.
    /// </summary>
    /// <param name="time_ms">Normalized time-point (in milliseconds) at which to evaluate</param>
    /// <param name="category">Measurement category to evaluate</param>
    /// <param name="evaluated">Evaluated measurement</param>
    /// <returns>Whether the evaluation succeeded (false if no data)</returns>
    public bool EvaluateMeasurement(int time_ms, string category, out double evaluated)
    {
        if (!_hasData)
        {
            evaluated = 0;
            return false;
        }

        int categoryID = DynamicDataMappings[category];

        time_ms += _startTimestamp;
        int clamp_time_ms = Math.Clamp(time_ms, _dataKeys[categoryID].First().Key, _dataKeys[categoryID].Last().Key);

        int sT = _dataKeys[categoryID].WeakPredecessor(clamp_time_ms).Key;
        int eT = _dataKeys[categoryID].WeakSuccessor(clamp_time_ms).Key;

        if (sT == eT)
        {
            evaluated = _dataKeys[categoryID][sT];
            return true;
        }

        float lerpRatio = (float) (time_ms - sT) / (eT - sT);

        evaluated = math.lerp(_dataKeys[categoryID][sT], _dataKeys[categoryID][eT], lerpRatio);
        return true;
    }

    public int[] GetNormalizedKeyTimes()
    {
        if (!_hasData) return new int[0];

        int[] result = new int[_dataKeys[CDM.t].Keys.Count];

        int i = 0;
        foreach (int t in _dataKeys[CDM.t].Values)
        {
            result[i] = t - _startTimestamp;
            ++i;
        }

        return result;
    }
    #endregion
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

    public static IReadOnlyDictionary<string, MeasureProperties> MeasureProperties;
    public static void LoadMeasureProperties(TextAsset propertiesAsset)
    {
        try
        {
            MeasureProperties = JsonConvert.DeserializeObject<Dictionary<string, MeasureProperties>>(propertiesAsset.text);
        }
        catch
        {
            Debug.LogError("Failed to load measure properties.");
        }
    }
    //= new Dictionary<string, string> {
    //    { "t", "°C" },
    //    { "hum", "%" },
    //    { "pres", "hPa" },
    //    { "co2", "ppm" },
    //    { MeasureMappings.UV, "index" },
    //    { MeasureMappings.BarTemperature, "°C" },
    //    { MeasureMappings.BarAltitude, "m" },
    //    { MeasureMappings.Satellites, "" },
    //    { MeasureMappings.FixGPS, "" },
    //    { MeasureMappings.AmbientLightRaw, "raw" },
    //    { MeasureMappings.SolarVoltage, "V" },
    //    { MeasureMappings.SolarCurrent, "A" },
    //    { MeasureMappings.SolarPower, "W" },
    //    { MeasureMappings.Temperature2, "°C" },
    //    { MeasureMappings.PhotoRaw, "raw" },
    //    { MeasureMappings.PhotoVoltage, "V" },
    //    { MeasureMappings.StatusSD, "" },
    //};
}