using System.Collections.Generic;
using System.Linq;
using CesiumForUnity;
using Unity.Mathematics;
using UnityEngine;

public class FlightDatapointVisualizer : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private CesiumGeoreference _map;
    [SerializeField] private DataProcessor _data;

    private CesiumGlobeAnchor _transformGPS;

    [SerializeField] private GameObject _dataPointPrefab;
    private List<WorldspaceDatapoint> _dataPlot;

    private void Awake()
    {
        _transformGPS = gameObject.GetComponent<CesiumGlobeAnchor>();
        _dataPlot = new List<WorldspaceDatapoint>();

        _data._onNewPacket.AddListener(AppendDatapoint);
        _data._onCompleteDatabase.AddListener(GenerateDataPlot);
        _data._onClearDatabase.AddListener(ClearDataPlot);
    }

    public void AppendDatapoint(bool incremental)
    {
        if (!incremental) return;

        if (_data.EvaluateLocationGPS(0, out double3 origin)) {
            _transformGPS.longitudeLatitudeHeight = origin;

            _dataPlot.Add(Instantiate(_dataPointPrefab, _data.GetLastPositionUnitySpaceRelative(_map, transform.position), Quaternion.identity, gameObject.transform).GetComponent<WorldspaceDatapoint>());
            _dataPlot.Last()._time_ms = _data.TotalTime();
        }
    }

    public void GenerateDataPlot()
    {
        if (_data.EvaluateLocationGPS(0, out double3 origin))
        {
            _transformGPS.longitudeLatitudeHeight = origin;

            int nodeID = 0;
            foreach (Vector3 point in _data.GetTravelPathUnitySpaceRelative(_map, transform.position))
            {
                _dataPlot.Add(Instantiate(_dataPointPrefab, point, Quaternion.identity, gameObject.transform).GetComponent<WorldspaceDatapoint>());
                // to do - add time setting
                ++nodeID;
            }
        }
    }

    public void ClearDataPlot()
    {
        foreach (WorldspaceDatapoint dp in _dataPlot) Destroy(dp.gameObject);
    }
}
