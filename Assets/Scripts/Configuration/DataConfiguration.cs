using UnityEngine;

[CreateAssetMenu(fileName = "New Data Processing Configuration", menuName = "Cansat/Data Processing Configuration")]
public class DataConfiguration : ScriptableObject
{
    [Tooltip("Runtime")] public bool _hasData;

    [Header("File Reading Config")]
    public string _path;

    [Header("Serial Port Config")]
    public string _portName;
    public int _baudRate;

    [Tooltip("Runtime")] public SerialPortHandler.StatusCode _serialStatus;

    [Header("Exporting Config")]
    public string _exportPath;
}