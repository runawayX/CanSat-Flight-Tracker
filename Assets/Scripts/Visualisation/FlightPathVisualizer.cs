using CesiumForUnity;
using Unity.Mathematics;
using UnityEngine;

public class FlightPathVisualizer : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private CesiumGeoreference _map;
    [SerializeField] private DataProcessor _data;

    private CesiumGlobeAnchor _transformGPS;
    private LineRenderer _pathLine;

    private void Awake()
    {
        _transformGPS = gameObject.GetComponent<CesiumGlobeAnchor>();
        _pathLine = gameObject.GetComponent<LineRenderer>();

        if (_pathLine == null)
        {
            Debug.LogError("Flight Path Visualizer has no Line Renderer component!");

            enabled = false;
            return;
        }

        _data._onNewPacket.AddListener(AppendFlighPath);
        _data._onCompleteDatabase.AddListener(GenerateFlightPath);
        _data._onClearDatabase.AddListener(ClearFlightPath);
    }

    public void AppendFlighPath(bool incremental)
    {
        if (!incremental) return;

        if (_data.EvaluateLocationGPS(0, out double3 origin)) {
            _transformGPS.longitudeLatitudeHeight = origin;

            _pathLine.SetPosition(_pathLine.positionCount++, _data.GetLastPositionUnitySpaceRelative(_map, transform.position));
        }
    }

    public void GenerateFlightPath()
    {
        if (_data.EvaluateLocationGPS(0, out double3 origin))
        {
            _transformGPS.longitudeLatitudeHeight = origin;

            Vector3[] path = _data.GetTravelPathUnitySpaceRelative(_map, transform.position);
            _pathLine.positionCount = path.Length;
            _pathLine.SetPositions(path);
        }
    }

    public void ClearFlightPath()
    {
        _pathLine.positionCount = 0;
    }
}
