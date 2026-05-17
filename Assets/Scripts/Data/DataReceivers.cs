using System;
using System.IO;
using UnityEngine;

using Newtonsoft.Json;
using System.Collections.Generic;

[System.Serializable]
public struct DataKeyframe
{
    [JsonProperty("t")] public int t { get; set; }

    [JsonProperty("lat")] public double lat { get; set; }
    [JsonProperty("lon")] public double lon { get; set; }
    [JsonProperty("alt")] public double alt { get; set; }

    [JsonProperty("temp")] public float temp { get; set; }
    [JsonProperty("hum")] public float hum { get; set; }
    [JsonProperty("pres")] public float pres { get; set; }
    [JsonProperty("co2")] public int co2 { get; set; }
    [JsonProperty("uv")] public float uv { get; set; }

    [JsonProperty("btemp")] public float btemp { get; set; }
    [JsonProperty("balt")] public float balt { get; set; }

    [JsonProperty("ax")] public float ax { get; set; }
    [JsonProperty("ay")] public float ay { get; set; }
    [JsonProperty("az")] public float az { get; set; }

    [JsonProperty("gx")] public float gx { get; set; }
    [JsonProperty("gy")] public float gy { get; set; }
    [JsonProperty("gz")] public float gz { get; set; }

    [JsonProperty("hdg")] public float hdg { get; set; }
    [JsonProperty("pitch")] public float pitch { get; set; }
    [JsonProperty("roll")] public float roll { get; set; }

    [JsonProperty("sats")] public int sats { get; set; }
    [JsonProperty("fix")] public int fix { get; set; }

    [JsonProperty("als")] public float als { get; set; }
    [JsonProperty("t2temp")] public float t2temp { get; set; }
    [JsonProperty("svolt")] public float svolt { get; set; }
    [JsonProperty("scurr")] public float scurr { get; set; }

    [JsonProperty("spwr")] public float spwr { get; set; }
    [JsonProperty("lraw")] public float lraw { get; set; }
    [JsonProperty("lvolt")] public float lvolt { get; set; }

    [JsonProperty("sd")] public float sd { get; set; }
}

public enum MeasureMappings
{
    Temperature, Humidity, Pressure, CO2, UV,
    BarTemperature, BarAltitude,
    Satellites, FixGPS,
    AmbientLightRaw, SolarVoltage, SolarCurrent, SolarPower,
    Temperature2,
    PhotoRaw, PhotoVoltage,
    StatusSD
}

public abstract class DataReceiver<STATUS_T>
{
    protected readonly DataProcessor _callback;
    public readonly bool _isIncremental;

    Dictionary<string, double> _lastKey;

    public DataReceiver(DataProcessor callback, bool isIncremental)
    {
        _callback = callback;
        _isIncremental = isIncremental;
    }

    public virtual void Begin()
    {
        Debug.Log("Initializing data receiver (undefined)...");
    }

    public abstract STATUS_T Status();

    public virtual void Read()
    {
        Debug.Log("Data is not incremental or reading is not implemented.");
    }

    /// <summary>
    /// Create and save a DataKeyframe to callback from a Json packet
    /// </summary>
    /// <param name="json">Json packet to convert</param>
    /// <returns>True if the creation succeeded</returns>
    protected virtual bool JsonCreateDatakey(string json)
    {
        string preprocess = json.Trim();
        if (string.IsNullOrEmpty(preprocess) || !preprocess.StartsWith('{')) return false;

        preprocess = preprocess.Replace("nan", "NaN");

        try
        {
            _lastKey = JsonConvert.DeserializeObject<Dictionary<string, double>>(preprocess, CansatDataHelpers.SerializeConfig);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Bad packet ({e.Message})\n{preprocess}");
            _callback.WarnBadDatakey();
            return false;
        }

        _callback.ProcessDatakey(_lastKey, _isIncremental);
        return true;
    }

    /// <summary>
    /// No string duplication overload of CreateDatakey
    /// </summary>
    /// <param name="json">Json packet to convert</param>
    /// <returns>True if the creation succeeded</returns>
    protected virtual bool JsonCreateDatakey(ref string json)
    {
        string preprocess = json.Trim();
        if (string.IsNullOrEmpty(preprocess) || !preprocess.StartsWith('{')) return false;

        preprocess = preprocess.Replace("nan", "NaN");

        try
        {
            _lastKey = JsonConvert.DeserializeObject<Dictionary<string, double>>(preprocess, CansatDataHelpers.SerializeConfig);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Bad packet ({e.Message})\n{preprocess}");
            _callback.WarnBadDatakey();
            return false;
        }

        _callback.ProcessDatakey(_lastKey, _isIncremental);
        return true;
    }

    public virtual void End()
    {
        Debug.Log("Stopping data receiver (undefined)...");
    }
}

public class JsonFileDataReader : DataReceiver<bool>
{
    public readonly string _path;

    // Internal
    private StreamReader _reader;

    public JsonFileDataReader(DataProcessor callback, string filePath) : base(callback, false)
    {
        _path = filePath;
    }

    public override void Begin()
    {
        if (_path == null)
        {
            Debug.LogWarning("File path is null!");
            return;
        }

        Debug.Log($"Beginning log read from '{_path}'...");

        _reader = new StreamReader(_path);
        string[] packetData = _reader.ReadToEnd().Split('\n');
        _reader.Close();

        foreach (string p in packetData)
        {
            JsonCreateDatakey(p);
        }

        _callback.CompleteData();
    }

    public override bool Status()
    {
        return _path != null && _reader != null;
    }
}

public class SerialPortDataReceiver : DataReceiver<SerialPortHandler.StatusCode>
{
    private readonly bool _logPackets = false;
    public DataKeyframe latestPacket { get; private set; }

    private SerialPortHandler _port;
    private object _portLock = new object();

    public string _latestPacket;

    public SerialPortDataReceiver(DataProcessor callback, bool logPackets = false) : base(callback, true)
    {
        _logPackets = logPackets;
    }

    public override void Begin()
    {
        lock (_portLock)
        {
            _port?.Dispose();
            _port = new SerialPortHandler(_callback._config._portName, _callback._config._baudRate, 512);
            Debug.Log(_port.GetHandshake_P());

            Debug.Log("Starting Serial Port Listening Thread...");
        }
    }

    public override SerialPortHandler.StatusCode Status()
    {
        lock (_portLock)
        {
            return _port.StatusCode_P();
        }
    }

    public override void Read()
    {
        lock (_portLock)
        {
            if (_port == null) return;
            if (!_port.IsRunning_P() || !_port.TryPacket_P(out _latestPacket)) return;

            if (_logPackets) Debug.Log($"Port reading - {_latestPacket}");
            JsonCreateDatakey(ref _latestPacket);
        }
    }

    public override void End()
    {
        lock (_portLock)
        {
            _port?.Dispose();
            Debug.Log($"Serial port stopped.");
        }
    }

    ~SerialPortDataReceiver()
    {
        lock (_portLock)
        {
            _port?.Dispose();
        }
    }
}