using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using XCharts.Runtime;

public class ChartWidgetManager : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private DataProcessor _data;
    [SerializeField] private VisualConfiguration _visualization;

    [Space(10)]
    [SerializeField] private GameObject _chartWidget;

    [Header("UI")]
    [SerializeField] private RectTransform _chartViewer;

    [Space(10)]
    [SerializeField] private Button _chartViewerButton;

    [SerializeField] private string _chartViewerOnText;
    [SerializeField] private string _chartViewerOffText;

    [Space(10)]
    [SerializeField] private Button _addButton;
    [SerializeField] private GameObject _chartCreator;

    [Space(10)]
    [SerializeField] private Transform _chartWidgetContainer;

    // Internal UI
    private TMP_Text _chartToggleText;

    private TMPro.TMP_Dropdown _minorCategorySelector;
    private TMPro.TMP_Dropdown _majorCategorySelector;
    private Button _creationConfirm;
    private Button _creationCancel;

    // Runtime
    public List<ChartDataPlotter> _chartWidgets { get; private set; }
    public bool _chartViewerActive { get; private set; } = false;

    private void Awake()
    {
        // UI setup
        _minorCategorySelector = _chartCreator.transform.Find("MinorCategorySelector").GetComponent<TMPro.TMP_Dropdown>();
        _majorCategorySelector = _chartCreator.transform.Find("MajorCategorySelector").GetComponent<TMPro.TMP_Dropdown>();
        _creationConfirm = _chartCreator.transform.Find("CreationConfirm").GetComponent<Button>();
        _creationCancel = _chartCreator.transform.Find("CreationCancel").GetComponent<Button>();

        _chartToggleText = _chartViewerButton.transform.GetChild(0).GetComponent<TMP_Text>();

        _chartViewerButton.onClick.AddListener(ToggleChartViewer);

        _addButton.onClick.AddListener(OpenChartCreator);
        _creationCancel.onClick.AddListener(CloseChartCreator);
        _creationConfirm.onClick.AddListener(ConfirmChartCreation);

        _chartWidgets = new List<ChartDataPlotter>();

        // External Event setup
        _data._onClearDatabase.AddListener(RefreshChartsGlobal);
        _data._onCompleteDatabase.AddListener(RefreshChartsGlobal);
    }

    private void Start()
    {
        _chartViewer.anchoredPosition = new Vector2(_chartViewer.sizeDelta.x / 2, _chartViewer.anchoredPosition.y);
        _chartToggleText.text = "<";
    }

    #region External Event Handling
    public void RefreshChartsGlobal()
    {
        foreach (var chart in _chartWidgets) chart.RefreshChart();
    }
    #endregion

    #region UI Handling
    // Actions
    public void ToggleChartViewer()
    {
        _chartViewerActive = !_chartViewerActive;

        if (_chartViewerActive)
        {
            _chartViewer.anchoredPosition = new Vector2(-_chartViewer.sizeDelta.x / 2, _chartViewer.anchoredPosition.y);
            _chartToggleText.text = _chartViewerOnText;
        } else
        {
            _chartViewer.anchoredPosition = new Vector2(_chartViewer.sizeDelta.x / 2, _chartViewer.anchoredPosition.y);
            _chartToggleText.text = _chartViewerOffText;
        }
    }

    public void OpenChartCreator()
    {
        _chartCreator.SetActive(true);

        _minorCategorySelector.options.Clear();
        _majorCategorySelector.options.Clear();

        _minorCategorySelector.AddOptions(CansatDataHelpers.InverseDynamicDataMappings());
        _majorCategorySelector.AddOptions(CansatDataHelpers.InverseDynamicDataMappings());

        if (OrbitalCameraControls.PriorityInstance != null) OrbitalCameraControls.PriorityInstance.SetInputFreeze(2, true);
    }

    public void CloseChartCreator()
    {
        _chartCreator.SetActive(false);

        if (OrbitalCameraControls.PriorityInstance != null) OrbitalCameraControls.PriorityInstance.SetInputFreeze(2, false);
    }

    public void ConfirmChartCreation()
    {
        CloseChartCreator();

        if (_majorCategorySelector.options[_majorCategorySelector.value].text.Equals(_minorCategorySelector.options[_minorCategorySelector.value].text))
        {
            Debug.LogError("Primary and secondary axes cannot match.");
            return;
        }

        if (!CansatDataHelpers.InverseDynamicDataMappings().Contains(_majorCategorySelector.options[_majorCategorySelector.value].text)
            || !CansatDataHelpers.InverseDynamicDataMappings().Contains(_minorCategorySelector.options[_minorCategorySelector.value].text))
        {
            Debug.LogError("Undefined category selected for chart creation.");
            return;
        }

        ChartDataPlotter chart = Instantiate(_chartWidget, _chartWidgetContainer, false).GetComponent<ChartDataPlotter>();
        chart._parent = this;

        chart._data = _data;
        chart._visualization = _visualization;

        chart._majorCategory = _majorCategorySelector.options[_majorCategorySelector.value].text;
        chart._minorCategories = new List<string>();
        chart._minorCategories.Add(_minorCategorySelector.options[_minorCategorySelector.value].text);

        chart.RefreshChart();

        _chartWidgets.Add(chart);
    }
    #endregion
}
