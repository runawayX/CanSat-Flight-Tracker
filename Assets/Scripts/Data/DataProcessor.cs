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
    private TreeDictionary<int, DataKeyframe> _dataKeys;
    public bool _hasData { get; private set; } = false;

    public DataReceiver<bool> _recordedDataFeed { get; private set; }
    public DataReceiver<SerialPortHandler.StatusCode> _liveDataFeed { get; private set; }

    private void Awake()
    {
        _dataKeys = new TreeDictionary<int, DataKeyframe>();
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
    public void ProcessDatakey(DataKeyframe p, bool incremental)
    {
        if (_logAddPacket) Debug.Log($"Processing {p}...");

        if (_dataKeys.Keys.Contains(p.t))
        {
            Debug.LogWarning($"Tried to add duplicate keyframe {p}");
            return;
        }

        _dataKeys.Add(p.t, p);
        _startTimestamp = _dataKeys.First().Key;

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
        if (_hasData) return _dataKeys.Last().Key - _startTimestamp;
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
        int clamp_time_ms = Math.Clamp(time_ms, _dataKeys.First().Key, _dataKeys.Last().Key);

        var sK = _dataKeys.WeakPredecessor(clamp_time_ms).Value;
        var eK = _dataKeys.WeakSuccessor(clamp_time_ms).Value;

        if (sK.t == eK.t)
        {
            evaluated = new double3(sK.lon, sK.lat, sK.alt);
            return true;
        }

        float lerpRatio = (float) (time_ms - sK.t) / (eK.t - sK.t);

        //Debug.Log($"Lerp {lerpRatio} ({time_ms}ms)\nFrom {sK.t}ms to {eK.t}ms");

        evaluated = new double3(math.lerp(sK.lon, eK.lon, lerpRatio),
            math.lerp(sK.lat, eK.lat, lerpRatio),
            math.lerp(sK.alt, eK.alt, lerpRatio));

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
        int clamp_time_ms = Math.Clamp(time_ms, _dataKeys.First().Key, _dataKeys.Last().Key);

        var sK = _dataKeys.WeakPredecessor(clamp_time_ms).Value;
        var eK = _dataKeys.WeakSuccessor(clamp_time_ms).Value;

        if (sK.t == eK.t)
        {
            evaluated = Quaternion.AngleAxis(sK.pitch, Vector3.right) * Quaternion.AngleAxis(sK.hdg, Vector3.up) * Quaternion.AngleAxis(sK.roll, Vector3.forward); ;
            return true;
        }

        float lerpRatio = (float) (time_ms - sK.t) / (eK.t - sK.t);
        Vector3 ir = Vector3.Lerp(new Vector3(sK.pitch, sK.hdg, sK.roll), new Vector3(eK.pitch, eK.hdg, eK.roll), lerpRatio);

        evaluated = Quaternion.AngleAxis(ir.x, Vector3.right) * Quaternion.AngleAxis(ir.y, Vector3.up) * Quaternion.AngleAxis(ir.z, Vector3.forward);
        return true;
    }

    /// <summary>
    /// Gives interpolated GPS coordinates and Unity-Based rotation at desired time-point
    /// </summary>
    /// <param name="time_ms">Normalized time-point (in milliseconds) at which to evaluate</param>
    /// <param name="evaluated">Evaluated location and rotation</param>
    /// <returns>Whether the evaluation succeeded (false if no data)</returns>
    public bool EvaluateTransform(int time_ms, out GeoTransform evaluated)
    {
        if (!_hasData)
        {
            evaluated = GeoTransform.identity;
            return false;
        }

        time_ms += _startTimestamp;
        int clamp_time_ms = Math.Clamp(time_ms, _dataKeys.First().Key, _dataKeys.Last().Key);

        var sK = _dataKeys.WeakPredecessor(clamp_time_ms).Value;
        var eK = _dataKeys.WeakSuccessor(clamp_time_ms).Value;

        double3 p;
        Quaternion r;

        if (sK.t == eK.t)
        {
            p = new double3(sK.lon, sK.lat, sK.alt);
            r = Quaternion.AngleAxis(sK.pitch, Vector3.right) * Quaternion.AngleAxis(sK.hdg, Vector3.up) * Quaternion.AngleAxis(sK.roll, Vector3.forward); ;

            evaluated = new GeoTransform(sK.t - _startTimestamp, p, r);
            return true;
        }

        float lerpRatio = (float) (time_ms - sK.t) / (eK.t - sK.t);

        p = new double3(math.lerp(sK.lon, eK.lon, lerpRatio), math.lerp(sK.lat, eK.lat, lerpRatio), math.lerp(sK.alt, eK.alt, lerpRatio));
        Vector3 ir = Vector3.Lerp(new Vector3(sK.pitch, sK.hdg, sK.roll), new Vector3(eK.pitch, eK.hdg, eK.roll), lerpRatio);
        r = Quaternion.AngleAxis(ir.x, Vector3.right) * Quaternion.AngleAxis(ir.y, Vector3.up) * Quaternion.AngleAxis(ir.z, Vector3.forward);

        evaluated = new GeoTransform(time_ms - _startTimestamp, p, r);
        return true;
    }

    public Vector3[] GetTravelPathUnitySpaceRelative(CesiumGeoreference referenceGlobe, Vector3 referencePosition)
    {
        if (!_hasData) return new Vector3[0];

        Vector3[] result = new Vector3[_dataKeys.Count];

        int r = 0;
        foreach (DataKeyframe k in _dataKeys.Values)
        {
            double3 unityPos = referenceGlobe.TransformEarthCenteredEarthFixedPositionToUnity(
                CesiumWgs84Ellipsoid.LongitudeLatitudeHeightToEarthCenteredEarthFixed(new double3(k.lon, k.lat, k.alt)));

            result[r] = new Vector3((float) unityPos.x, (float) unityPos.y, (float) unityPos.z) - referencePosition;
            ++r;
        }

        return result;
    }

    public Vector3 GetLastPositionUnitySpaceRelative(CesiumGeoreference referenceGlobe, Vector3 referencePosition)
    {
        if (!_hasData) return Vector3.zero;

        DataKeyframe k = _dataKeys.Values.Last();
        double3 unityPos = referenceGlobe.TransformEarthCenteredEarthFixedPositionToUnity(
                CesiumWgs84Ellipsoid.LongitudeLatitudeHeightToEarthCenteredEarthFixed(new double3(k.lon, k.lat, k.alt)));

        return new Vector3((float) unityPos.x, (float) unityPos.y, (float) unityPos.z) - referencePosition;
    }

    public double3[] GetTravelPathGeo()
    {
        if (!_hasData) return new double3[0];

        double3[] result = new double3[_dataKeys.Count];

        int r = 0;
        foreach (DataKeyframe k in _dataKeys.Values)
        {
            result[r] = new double3(k.lon, k.lat, k.alt);
            ++r;
        }

        return result;
    }

    public double3 GetLastPositionGeo()
    {
        if (!_hasData) return double3.zero;

        DataKeyframe k = _dataKeys.Values.Last();
        return new double3(k.lon, k.lat, k.alt);
    }

    /// <summary>
    /// Gives interpolated measurement for a specified category at the desired time-point.
    /// </summary>
    /// <param name="time_ms">Normalized time-point (in milliseconds) at which to evaluate</param>
    /// <param name="category">Measurement category to evaluate</param>
    /// <param name="evaluated">Evaluated measurement</param>
    /// <returns>Whether the evaluation succeeded (false if no data)</returns>
    public bool EvaluateMeasurement(int time_ms, MeasureMappings category, out double evaluated)
    {
        if (!_hasData)
        {
            evaluated = 0;
            return false;
        }

        time_ms += _startTimestamp;
        int clamp_time_ms = Math.Clamp(time_ms, _dataKeys.First().Key, _dataKeys.Last().Key);

        var sK = _dataKeys.WeakPredecessor(clamp_time_ms).Value;
        var eK = _dataKeys.WeakSuccessor(clamp_time_ms).Value;

        if (sK.t == eK.t)
        {
            evaluated = GetMeasurement[category](sK);
            return true;
        }

        float lerpRatio = (float) (time_ms - sK.t) / (eK.t - sK.t);

        evaluated = math.lerp(GetMeasurement[category](sK), GetMeasurement[category](eK), lerpRatio);
        return true;
    }

    public int[] GetNormalizedKeyTimes()
    {
        if (!_hasData) return new int[0];

        int[] result = new int[_dataKeys.Keys.Count];

        int i = 0;
        foreach (int t in _dataKeys.Keys)
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

    /// <summary>
    /// Returns a specific measurement from a keyframe based on the provided category.
    /// </summary>
    public static readonly IReadOnlyDictionary<MeasureMappings, Func<DataKeyframe, double>> GetMeasurement = new Dictionary<MeasureMappings, Func<DataKeyframe, double>> {
        { MeasureMappings.Temperature, k => k.temp },
        { MeasureMappings.Humidity, k => k.hum },
        { MeasureMappings.Pressure, k => k.pres },
        { MeasureMappings.CO2, k => k.co2 },
        { MeasureMappings.UV, k => k.uv },
        { MeasureMappings.BarTemperature, k => k.btemp },
        { MeasureMappings.BarAltitude, k => k.balt },
        { MeasureMappings.Satellites, k => k.sats },
        { MeasureMappings.FixGPS, k => k.fix },
        { MeasureMappings.AmbientLightRaw, k => k.als },
        { MeasureMappings.SolarVoltage, k => k.svolt },
        { MeasureMappings.SolarCurrent, k => k.scurr },
        { MeasureMappings.SolarPower, k => k.spwr },
        { MeasureMappings.Temperature2, k => k.t2temp },
        { MeasureMappings.PhotoRaw, k => k.lraw },
        { MeasureMappings.PhotoVoltage, k => k.lvolt },
        { MeasureMappings.StatusSD, k => k.sd },
    };

    public static IReadOnlyDictionary<MeasureMappings, string> GetMeasureSuffix = new Dictionary<MeasureMappings, string> {
        { MeasureMappings.Temperature, "°C" },
        { MeasureMappings.Humidity, "%" },
        { MeasureMappings.Pressure, "hPa" },
        { MeasureMappings.CO2, "ppm" },
        { MeasureMappings.UV, "index" },
        { MeasureMappings.BarTemperature, "°C" },
        { MeasureMappings.BarAltitude, "m" },
        { MeasureMappings.Satellites, "" },
        { MeasureMappings.FixGPS, "" },
        { MeasureMappings.AmbientLightRaw, "raw" },
        { MeasureMappings.SolarVoltage, "V" },
        { MeasureMappings.SolarCurrent, "A" },
        { MeasureMappings.SolarPower, "W" },
        { MeasureMappings.Temperature2, "°C" },
        { MeasureMappings.PhotoRaw, "raw" },
        { MeasureMappings.PhotoVoltage, "V" },
        { MeasureMappings.StatusSD, "" },
    };
}