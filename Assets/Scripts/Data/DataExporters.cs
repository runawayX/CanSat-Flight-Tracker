using C5;
using Newtonsoft.Json;
using System.IO;
using TMPro;
using UnityEngine;

public abstract class DataExporter<T> where T : struct
{
    protected readonly TreeDictionary<int, T> _dataOrigin;
    private JsonSerializerSettings _serializationSettings;

    public DataExporter(TreeDictionary<int, T> origin)
    {
        _dataOrigin = origin;

        _serializationSettings = new JsonSerializerSettings();
        _serializationSettings.DefaultValueHandling = DefaultValueHandling.Populate;
        _serializationSettings.MissingMemberHandling = MissingMemberHandling.Ignore;
        _serializationSettings.FloatParseHandling = FloatParseHandling.Double;

        JsonConvert.DefaultSettings = () => _serializationSettings;
    }

    protected virtual string SerializeDatakey(DataKeyframe k)
    {
        return JsonConvert.SerializeObject(k, _serializationSettings);
    }

    public virtual void Export()
    {
        Debug.Log("Initializing data receiver (undefined)...");
    }
}

public class JsonFileDataExporter : DataExporter<DataKeyframe>
{
    public readonly string _path;

    // Internal
    private StreamWriter _writer;

    public JsonFileDataExporter(TreeDictionary<int, DataKeyframe> origin, string outputFilePath) : base(origin)
    {
        _path = outputFilePath;
    }

    public override void Export()
    {
        Debug.Log($"Exporting recorded data to {_path}...");

        _writer = new StreamWriter(_path);

        foreach (DataKeyframe p in _dataOrigin.Values)
        {
            _writer.Write($"{SerializeDatakey(p)}\n");
        }

        _writer.Flush();
        _writer.Close();
    }
}
