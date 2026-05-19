using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using XCharts.Runtime;

public class DataWidgetManager : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private DataProcessor _data;
    [SerializeField] private PlaybackConfiguration _playback;

    [Space(10)]
    [SerializeField] private GameObject _dataWidget;

    [Header("UI")]
    [SerializeField] private RectTransform _dataViewer;

    [Space(10)]
    [SerializeField] private Button _dataViewerButton;

    [SerializeField] private string _dataViewerOnText;
    [SerializeField] private string _dataViewerOffText;

    [Space(10)]
    [SerializeField] private Toggle _liveToggle;

    [Space(10)]
    [SerializeField] private Transform _dataWidgetContainer;

    // Internal UI
    private TMP_Text _chartToggleText;

    // Runtime
    public List<DataWidget> _dataWidgets { get; private set; }
    public bool _dataViewerActive { get; private set; } = false;

    private void Awake()
    {
        // UI setup
        _chartToggleText = _dataViewerButton.transform.GetChild(0).GetComponent<TMP_Text>();
        _dataViewerButton.onClick.AddListener(ToggleDataViewer);

        _dataWidgets = new List<DataWidget>();

        // External Event setup
        _data._onClearDatabase.AddListener(ClearWidgets);
        _data._onFirstPacket.AddListener((bool inc) => { if (inc) SetupWidgets(); });
        _data._onCompleteDatabase.AddListener(SetupWidgets);
    }

    private void Start()
    {
        _dataViewer.anchoredPosition = new Vector2(-_dataViewer.sizeDelta.x / 2, _dataViewer.anchoredPosition.y);
        _chartToggleText.text = _dataViewerOffText;
    }

    #region External Event Handling
    public void ClearWidgets()
    {
        foreach (var dataWidget in _dataWidgets) Destroy(_dataWidget);
    }

    public void SetupWidgets()
    {
        ClearWidgets();
        foreach (int category in CansatDataHelpers.DynamicDataMappings.Values)
        {
            DataWidget d = Instantiate(_dataWidget, _dataWidgetContainer, false).GetComponent<DataWidget>();
            d._data = _data;
            d._parent = this;
            d._category = category;

            d.Init();
        }
    }
    #endregion

    #region UI Handling
    // Actions
    public void ToggleDataViewer()
    {
        _dataViewerActive = !_dataViewerActive;

        if (_dataViewerActive)
        {
            _dataViewer.anchoredPosition = new Vector2(_dataViewer.sizeDelta.x / 2, _dataViewer.anchoredPosition.y);
            _chartToggleText.text = _dataViewerOnText;

            foreach (var dataWidget in _dataWidgets) dataWidget.enabled = true;
        } else
        {
            _dataViewer.anchoredPosition = new Vector2(-_dataViewer.sizeDelta.x / 2, _dataViewer.anchoredPosition.y);
            _chartToggleText.text = _dataViewerOffText;

            foreach (var dataWidget in _dataWidgets) dataWidget.enabled = false;
        }
    }
    #endregion

    public int GetTime()
    {
        if (_liveToggle.isOn) return _playback._duration_ms;
        return _playback._currentTime_ms;
    }
}
