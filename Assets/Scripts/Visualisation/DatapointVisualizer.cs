using System.Collections.Generic;
using System.Linq;
using CesiumForUnity;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Events;

public class DatapointVisualizer : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private CesiumGeoreference _map;

    [Space(10)]
    public DataProcessor _data;
    public VisualConfiguration _visualization;

    private CesiumGlobeAnchor _transformGPS;

    [SerializeField] private GameObject _dataPointPrefab;
    private static InstantiateParameters _prefabSpawnParameters;
    private List<WorldspaceDatapoint> _dataPlot;

    //[Header("Events")]
    //public UnityEvent _refreshData;

    private void Awake()
    {
        _prefabSpawnParameters = new InstantiateParameters();
        _prefabSpawnParameters.parent = gameObject.transform;
        _prefabSpawnParameters.worldSpace = false;

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

            WorldspaceDatapoint dp = Instantiate(_dataPointPrefab, _data.GetLastPositionUnitySpaceRelative(_map, transform.position), Quaternion.identity, _prefabSpawnParameters).GetComponent<WorldspaceDatapoint>();
            
            dp._parent = this;
            dp._location = _data.GetLastPositionGeo();
            dp._time_ms = _data.TotalTime();
            dp.RefreshData();

            _dataPlot.Add(dp);
        }
    }

    public void GenerateDataPlot()
    {
        if (_data.EvaluateLocationGPS(0, out double3 origin))
        {
            _transformGPS.longitudeLatitudeHeight = origin;

            int[] timeStamps = _data.GetNormalizedKeyTimes();
            double3[] locations = _data.GetTravelPathGeo();

            int nodeID = 0;
            foreach (Vector3 point in _data.GetTravelPathUnitySpaceRelative(_map, transform.position))
            {
                WorldspaceDatapoint dp = Instantiate(_dataPointPrefab, point, Quaternion.identity, _prefabSpawnParameters).GetComponent<WorldspaceDatapoint>();

                dp._parent = this;
                dp._location = locations[nodeID];
                dp._time_ms = timeStamps[nodeID];
                dp.RefreshData();

                _dataPlot.Add(dp);
                ++nodeID;
            }
        }
    }

    public void RefreshDataPlot()
    {
        foreach (WorldspaceDatapoint dp in _dataPlot) dp.RefreshData();
    }

    public void ClearDataPlot()
    {
        foreach (WorldspaceDatapoint dp in _dataPlot) Destroy(dp.gameObject);
    }
}
