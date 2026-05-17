using System.Collections.Generic;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using XCharts.Runtime;

public class ChartDataPlotter : MonoBehaviour
{
    [Header("Chart Properties")]
    public string _majorCategory;
    public List<string> _minorCategories;

    [Header("Components")]
    public DataProcessor _data;
    public VisualConfiguration _visualization;

    [Space(10)]
    public ChartWidgetManager _parent;

    [Space(10)]
    [SerializeField] private LineChart _chartRenderer;

    [Header("UI")]
    [SerializeField] private TMP_Text _inactiveAlert;

    [SerializeField] private Button _refreshWidget;
    [SerializeField] private Button _deleteWidget;

    // Internal chart
    XAxis _majorAxis;
    YAxis _minorAxis;

    private void Awake()
    {
        _majorAxis = _chartRenderer.EnsureChartComponent<XAxis>();
        _minorAxis = _chartRenderer.EnsureChartComponent<YAxis>();

        _majorAxis.type = Axis.AxisType.Value;
        _minorAxis.type = Axis.AxisType.Value;

        _majorAxis.minMaxType = Axis.AxisMinMaxType.Custom;
        _minorAxis.minMaxType = Axis.AxisMinMaxType.Custom;

        _refreshWidget.onClick.AddListener(RefreshChart);
        _deleteWidget.onClick.AddListener(DeleteChart);
    }

    public void RefreshChart()
    {
        if (!_data._hasData)
        {
            _inactiveAlert.gameObject.SetActive(true);
            _inactiveAlert.text = "No data available.";

            _chartRenderer.gameObject.SetActive(false);
            return;
        }

        if (!CansatDataHelpers.DynamicDataMappings.ContainsKey(_majorCategory))
        {
            _inactiveAlert.gameObject.SetActive(true);
            _inactiveAlert.text = "Primary axis data does not exist.";

            _chartRenderer.gameObject.SetActive(false);
            return;
        }
        
        _inactiveAlert.gameObject.SetActive(false);
        _chartRenderer.gameObject.SetActive(true);

        _chartRenderer.RemoveData();

        if (_minorCategories.Count == 1) _chartRenderer.EnsureChartComponent<Title>().text = $"{CansatDataHelpers.MeasurePropertyMap[_minorCategories[0]]._name ?? _minorCategories[0]} over {CansatDataHelpers.MeasurePropertyMap[_majorCategory]._name ?? _majorCategory}";
        else _chartRenderer.EnsureChartComponent<Title>().text = $"{_minorCategories.Count} datasets over {CansatDataHelpers.MeasurePropertyMap[_majorCategory]._name ?? _majorCategory}";

        bool isTimeMapped = false;
        if (_majorCategory != "t") {
            double2 xBounds = CansatDataHelpers.MeasurePropertyMap[_majorCategory].GetBounds();
            _majorAxis.min = xBounds.x;
            _majorAxis.max = xBounds.y;
        } else
        {
            _majorAxis.min = 0;
            _majorAxis.max = (double) _data.TotalTime() / 1000; // special case for time-based plotting

            _majorAxis.type = Axis.AxisType.Time; // override axis to time
            isTimeMapped = true;
        }

        double2 yBounds = CansatDataHelpers.MeasurePropertyMap[_minorCategories[0]].GetBounds();
        double2 tempBounds;
        for (int b = 1; b < _minorCategories.Count; ++b)
        {
            tempBounds = CansatDataHelpers.MeasurePropertyMap[_minorCategories[b]].GetBounds();
            yBounds.x = math.min(yBounds.x, tempBounds.x);
            yBounds.y = math.max(yBounds.y, tempBounds.y);
        }

        _minorAxis.min = yBounds.x;
        _minorAxis.max = yBounds.y;

        MeasureEvaluation[] majorAxisCache = _data.GetAllMeasureKeys(_majorCategory);

        foreach (string c in _minorCategories)
        {
            if (!CansatDataHelpers.DynamicDataMappings.TryGetValue(c, out int categoryCache)) continue;

            Line chartSeries = _chartRenderer.AddSerie<Line>(CansatDataHelpers.MeasurePropertyMap[c]._name ?? c);

            foreach (var x in majorAxisCache)
            {
                if (_data.EvaluateMeasurement(x.time, categoryCache, out double y)) chartSeries.AddXYData(isTimeMapped ? (double) x.time / 1000 : x.value, y);
                else chartSeries.AddXYData(isTimeMapped ? (double) x.time / 1000 : x.value, yBounds.x); // default to minimum from bounds if not measured
            }
        }
    }

    public void DeleteChart()
    {
        if (_parent != null) _parent._chartWidgets.Remove(this);
        Destroy(gameObject);
    }
}
