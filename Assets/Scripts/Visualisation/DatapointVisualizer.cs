using CesiumForUnity;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Events;
using Color = UnityEngine.Color;

public class DatapointVisualizer : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private CesiumGeoreference _map;

    [Space(10)]
    public DataProcessor _data;
    public VisualConfiguration _visualization;

    private CesiumGlobeAnchor _transformGPS;

    [Space(10)]
    [SerializeField] private GameObject _datapointPrefab;

    public float _minDatapointProximity = 1f;
    [SerializeField] private LayerMask _datapointLayer;

    private static InstantiateParameters _prefabSpawnParameters;
    private List<WorldspaceDatapoint> _dataPlot;

    //[Header("Events")]
    //public UnityEvent _refreshData;

    private void Awake()
    {
        _visualization.LoadVisualCustomization();
        _visualization._measureCategories = new List<string>();
        //_visualization.SerializeTestCustomization();

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

        Vector3 instancePos = _data.GetLastPositionUnitySpaceRelative(_map, transform.position);
        //if (Physics.CheckSphere(instancePos, _minDatapointProximity, _datapointLayer, QueryTriggerInteraction.Collide)) return;
        if (_dataPlot.Count > 0 && Vector3.Distance(_dataPlot[^1].transform.position, instancePos) < _minDatapointProximity) return;

        if (_data.EvaluateLocationGPS(0, out double3 origin)) {
            _transformGPS.longitudeLatitudeHeight = origin;

            WorldspaceDatapoint dp = Instantiate(_datapointPrefab, instancePos, Quaternion.identity, _prefabSpawnParameters).GetComponent<WorldspaceDatapoint>();
            
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

            //StringBuilder debugString = new StringBuilder("Timestamps:\n");
            //foreach (int t in timeStamps) debugString.Append(t + "\n");
            //debugString.Append("Locations:\n");
            //foreach (double3 loc in locations) debugString.Append(loc + "\n");

            //Debug.Log(debugString.ToString());

            int nodeID = 0;
            foreach (Vector3 point in _data.GetTravelPathUnitySpaceRelative(_map, transform.position))
            {
                //if (Physics.CheckSphere(point, _minDatapointProximity, _datapointLayer, QueryTriggerInteraction.Collide))
                //{
                //    ++nodeID;
                //    continue;
                //}
                if (_dataPlot.Count > 0 && Vector3.Distance(_dataPlot[^1].transform.localPosition, point) < _minDatapointProximity)
                {
                    ++nodeID;
                    continue;
                }

                WorldspaceDatapoint dp = Instantiate(_datapointPrefab, point, Quaternion.identity, _prefabSpawnParameters).GetComponent<WorldspaceDatapoint>();

                dp._parent = this;
                dp._location = locations[nodeID];
                dp._time_ms = timeStamps[nodeID];
                dp.RefreshData();

                _dataPlot.Add(dp);
                ++nodeID;
            }
        }
    }

    public Color ActiveCategoryEvaluateDataColor(double data)
    {
        //int category = Math.Min((int) _visualization._activeMeasureCategory, _visualization._categoryColors.Count - 1);
        //float lerpRatio = Mathf.Clamp01((float)((data - _visualization._categoryBounds[category].x) / _visualization._categoryBounds[category].y));

        return _visualization.EvaluateDataColor(CansatDataHelpers.InverseDynamicDataMappings()[_visualization._activeMeasureCategory], data);
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
