using C5;
using CesiumForUnity;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using TMPro;
using Unity.Android.Gradle.Manifest;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.UIElements;

using static CansatDataHelpers;
using static UnityEngine.Rendering.DebugUI;

public readonly struct MeasureEvaluation
{
    public readonly int time;
    public readonly double value;

    public MeasureEvaluation(int time, double value)
    {
        this.time = time;
        this.value = value;
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
        LoadMeasureProperties(_config._measureProperties);
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

        DataExporter export = new JsonFileDataExporter(_dataKeys, DynamicDataMappings, _config._exportPath);

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

        DynamicDataMappings.Clear();
        CDM.Clear();
        _hasUpdatedMappings = true;

        ResetMeasureBounds();

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

                if (!MeasurePropertyMap.ContainsKey(sub.Key)) 
                    MeasurePropertyMap.Add(sub.Key, new MeasureProperties(double2.zero, false));

                _dataKeys.Add(new TreeDictionary<int, double>());
                _hasUpdatedMappings = true;
            }

            _dataKeys[DynamicDataMappings[sub.Key]].Add(t, sub.Value);
            MeasurePropertyMap[sub.Key].CheckRuntimeBounds(sub.Value);

            if (_hasData) _startTimestamp = Math.Min(_startTimestamp, t);
            else _startTimestamp = t;

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

        if (!CDM.IsValidGPS())
        {
            Debug.LogWarning("Cannot evaluate GPS location - missing/invalid Longitude, Latitude or Altitude components.");
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

        if (!CDM.IsValidRotation())
        {
            Debug.LogWarning("Cannot evaluate Rotation - missing/invalid Heading, Pitch or Roll components.");
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

        if (!CDM.IsValidGPS())
        {
            Debug.LogWarning("Cannot evaluate GeoTransform - missing/invalid data.");
            evaluated = GeoTransform.identity;
            return false;
        }

        time_ms += _startTimestamp;
        int clamp_time_ms = Math.Clamp(time_ms, _dataKeys[CDM.lon].First().Key, _dataKeys[CDM.lon].Last().Key);

        // taking lon as base as GeoTransform sub-data is assumed uniform
        int sT = _dataKeys[CDM.lon].WeakPredecessor(clamp_time_ms).Key;
        int eT = _dataKeys[CDM.lon].WeakSuccessor(clamp_time_ms).Key;

        double3 p = double3.zero;
        Quaternion r = Quaternion.identity;

        if (sT == eT)
        {
            p = new double3(_dataKeys[CDM.lon][sT], _dataKeys[CDM.lat][sT], _dataKeys[CDM.alt][sT]);
            if (CDM.IsValidRotation()) r = Quaternion.AngleAxis((float) _dataKeys[CDM.pitch][sT], Vector3.right)
                * Quaternion.AngleAxis((float) _dataKeys[CDM.hdg][sT], Vector3.up) 
                * Quaternion.AngleAxis((float) _dataKeys[CDM.roll][sT], Vector3.forward);

            evaluated = new GeoTransform(sT - _startTimestamp, p, r);
            return true;
        }

        float lerpRatio = (float) (time_ms - sT) / (eT - sT);

        p = new double3(math.lerp(_dataKeys[CDM.lon][sT], _dataKeys[CDM.lon][eT], lerpRatio),
            math.lerp(_dataKeys[CDM.lat][sT], _dataKeys[CDM.lat][eT], lerpRatio),
            math.lerp(_dataKeys[CDM.alt][sT], _dataKeys[CDM.alt][eT], lerpRatio));

        if (CDM.IsValidRotation())
        {
            Vector3 ir = Vector3.Lerp(new Vector3((float) _dataKeys[CDM.pitch][sT], (float) _dataKeys[CDM.hdg][sT], (float) _dataKeys[CDM.roll][sT]),
                new Vector3((float) _dataKeys[CDM.pitch][eT], (float) _dataKeys[CDM.hdg][eT], (float) _dataKeys[CDM.roll][eT]),
                lerpRatio);

            r = Quaternion.AngleAxis(ir.x, Vector3.right) * Quaternion.AngleAxis(ir.y, Vector3.up) * Quaternion.AngleAxis(ir.z, Vector3.forward);
        }

        evaluated = new GeoTransform(time_ms - _startTimestamp, p, r);
        return true;
    }

    public Vector3[] GetTravelPathUnitySpaceRelative(CesiumGeoreference referenceGlobe, Vector3 referencePosition)
    {
        if (!_hasData) return new Vector3[0];

        Vector3[] result = new Vector3[_dataKeys[CDM.lon].Count];

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

        double3[] result = new double3[_dataKeys[CDM.lon].Count];

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

        if (!DynamicDataMappings.TryGetValue(category, out int categoryID))
        {
            evaluated = 0;
            return false;
        }

        time_ms += _startTimestamp;
        int clamp_time_ms = Math.Clamp(time_ms, _dataKeys[categoryID].First().Key, _dataKeys[categoryID].Last().Key);

        int sT = _dataKeys[categoryID].WeakPredecessor(clamp_time_ms).Key;
        int eT = _dataKeys[categoryID].WeakSuccessor(clamp_time_ms).Key;

        if (sT == eT)
        {
            evaluated = _dataKeys[categoryID][sT];

            if (double.IsNaN(evaluated)) return false;
            return true;
        }

        float lerpRatio = (float) (time_ms - sT) / (eT - sT);

        evaluated = math.lerp(_dataKeys[categoryID][sT], _dataKeys[categoryID][eT], lerpRatio);

        if (double.IsNaN(evaluated)) return false;
        return true;
    }

    /// <summary>
    /// Gives interpolated measurement for a specified category (internal id) at the desired time-point.
    /// </summary>
    /// <param name="time_ms">Normalized time-point (in milliseconds) at which to evaluate</param>
    /// <param name="category">Measurement category to evaluate</param>
    /// <param name="evaluated">Evaluated measurement</param>
    /// <returns>Whether the evaluation succeeded (false if no data)</returns>
    public bool EvaluateMeasurement(int time_ms, int category, out double evaluated)
    {
        if (!_hasData)
        {
            evaluated = 0;
            return false;
        }

        time_ms += _startTimestamp;
        int clamp_time_ms = Math.Clamp(time_ms, _dataKeys[category].First().Key, _dataKeys[category].Last().Key);

        int sT = _dataKeys[category].WeakPredecessor(clamp_time_ms).Key;
        int eT = _dataKeys[category].WeakSuccessor(clamp_time_ms).Key;

        if (sT == eT)
        {
            evaluated = _dataKeys[category][sT];

            if (double.IsNaN(evaluated)) return false;
            return true;
        }

        float lerpRatio = (float) (time_ms - sT) / (eT - sT);

        evaluated = math.lerp(_dataKeys[category][sT], _dataKeys[category][eT], lerpRatio);

        if (double.IsNaN(evaluated)) return false;
        return true;
    }

    /// <summary>
    /// Gives all normalized timestamps and values of a selected category
    /// </summary>
    /// <param name="category">The measurement category to get the keyframes of</param>
    /// <returns></returns>
    public MeasureEvaluation[] GetAllMeasureKeys(string category)
    {
        if (!_hasData) return new MeasureEvaluation[0];

        MeasureEvaluation[] result = new MeasureEvaluation[_dataKeys[DynamicDataMappings[category]].Keys.Count];

        int i = 0;
        foreach (var t in _dataKeys[DynamicDataMappings[category]])
        {
            result[i] = new MeasureEvaluation(t.Key - _startTimestamp, double.IsNaN(t.Value) ? 0 : t.Value);
            ++i;
        }

        return result;
    }

    /// <summary>
    /// Gives all master timestamps (for all packets)
    /// </summary>
    /// <returns></returns>
    public int[] GetNormalizedBaseTimes()
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

    /// <summary>
    /// Gives all timestamps of measurement of a specific category
    /// </summary>
    /// <param name="category">Measurement category of which to get the keyframe times of</param>
    /// <returns></returns>
    public int[] GetNormalizedMeasureTimes(string category)
    {
        if (!_hasData) return new int[0];

        int[] result = new int[_dataKeys[DynamicDataMappings[category]].Keys.Count];

        int i = 0;
        foreach (int t in _dataKeys[DynamicDataMappings[category]].Values)
        {
            result[i] = t - _startTimestamp;
            ++i;
        }

        return result;
    }
    #endregion
}