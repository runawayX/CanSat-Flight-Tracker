using C5;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;

public abstract class DataExporter
{
    protected readonly IReadOnlyList<TreeDictionary<int, double>> _dataOrigin;
    protected readonly IReadOnlyDictionary<string, int> _dataMappings;

    public DataExporter(IReadOnlyList<TreeDictionary<int, double>> origin, IReadOnlyDictionary<string, int> originMappings)
    {
        _dataOrigin = origin;
        _dataMappings = originMappings;
    }

    public virtual void Export()
    {
        Debug.Log("Initializing data receiver (undefined)...");
    }
}

public class JsonFileDataExporter : DataExporter
{
    public readonly string _path;

    // Internal
    private StreamWriter _writer;

    public JsonFileDataExporter(IReadOnlyList<TreeDictionary<int, double>> origin, IReadOnlyDictionary<string, int> originMappings, string outputFilePath) : base(origin, originMappings)
    {
        _path = outputFilePath;
    }

    public override void Export()
    {
        Debug.Log($"Exporting recorded data to {_path}...");

        _writer = new StreamWriter(_path);
        Dictionary<string, double> tempKeyframe = new Dictionary<string, double>();

        foreach (int t in _dataOrigin[CDM.t].Values)
        {
            foreach (var sub in _dataMappings)
            {
                if (_dataOrigin[sub.Value].TryWeakPredecessor(t, out KeyValuePair<int, double> v)) tempKeyframe[sub.Key] = v.Value;
                else tempKeyframe[sub.Key] = double.NaN;
            }

            _writer.Write($"{JsonConvert.SerializeObject(tempKeyframe, CansatDataHelpers.SerializeConfig)}\n");
        }

        _writer.Flush();
        _writer.Close();
    }
}
