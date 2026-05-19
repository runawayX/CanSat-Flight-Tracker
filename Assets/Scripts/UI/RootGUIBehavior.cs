using SFB;
using System;
using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class RootGUIBehavior : MonoBehaviour
{
    [Header("GUI Customisation")]
    [SerializeField] private Sprite _packetIcon;
    [SerializeField] private Sprite _badPacketIcon;

    [Header("References")]
    [SerializeField] private DataProcessor _dataProcessor;

    [SerializeField] private WorldPositioningManager _positioning;

    [SerializeField] private PlaybackConfiguration _playbackConfig;
    [SerializeField] private PlaybackManager _playback;

    [SerializeField] private OrbitalCameraControls _cameraControls;

    [SerializeField] private VisualConfiguration _visualizationConfig;
    [SerializeField] private WorldspaceDataPlotter _visualization;

    [Header("Components")]
    [SerializeField] private UIDocument _ui;

    [Space(10)]
    [SerializeField] private KeyCode _submitKey;
    [SerializeField] private InputActionReference _mousePosition;

    // UI Reference
    private VisualElement _settingsView;
    private ToggleButtonGroup _playbackStateControl;

    private VisualElement _noDataAlert;
    private VisualElement _serialReadAlert;
    private Image _packetPing;

    private VisualElement _worldHoverInfo;
    private DropdownField _worldHoverCategory;

    // Timing
    private float _packetPingTime = 0f;

    // Input
    private InputAction _actionMousePosition;

    private void Awake()
    {
        _actionMousePosition = _mousePosition.action;
    }

    private IEnumerator Start()
    {
        while (_ui.rootVisualElement.childCount == 0) yield return null;

        // UI References
        _settingsView = _ui.rootVisualElement.Q<VisualElement>("Settings");

        _noDataAlert = _ui.rootVisualElement.Q<VisualElement>("EmptyDataIdentifier");
        _serialReadAlert = _ui.rootVisualElement.Q<VisualElement>("SerialListenIdentifier");
        _packetPing = _ui.rootVisualElement.Q<Image>("PacketPing");

        _worldHoverInfo = _ui.rootVisualElement.Q<VisualElement>("WorldHoverInfo");

        // Binding Events
        _playbackStateControl = _ui.rootVisualElement.Q<ToggleButtonGroup>("PlayState");
        _playbackStateControl.RegisterValueChangedCallback(e => { _playbackConfig.SetStateFromMask(e.newValue); });
        _playback._onPlaybackChange.AddListener(PlaybackValueChange);

        _ui.rootVisualElement.Q<Button>("SnapToFront").clicked += RunSnapToFront;

        _ui.rootVisualElement.Q<Button>("OpenSettings").clicked += ToggleSettings;
        _ui.rootVisualElement.Q<Button>("CloseSettings").clicked += CloseSettings;

        _ui.rootVisualElement.Q<Button>("ReadImport").clicked += RunImportRead;
        _ui.rootVisualElement.Q<Button>("SelectImportFile").clicked += RunImportFileSelect;

        _ui.rootVisualElement.Q<Button>("EnableSerial").clicked += RunSerialStart;
        _ui.rootVisualElement.Q<Button>("DisableSerial").clicked += RunSerialStop;

        _ui.rootVisualElement.Q<Button>("ExportData").clicked += RunExport;
        _ui.rootVisualElement.Q<Button>("SelectExportFile").clicked += RunExportFileSelect;

        _ui.rootVisualElement.Q<Button>("ClearData").clicked += RunDataClear;

        _ui.rootVisualElement.Q<Button>("GetUserOrigin").clicked += RunUpdateOriginFromUser;
        _ui.rootVisualElement.Q<Button>("GetTrackingOrigin").clicked += RunUpdateOriginFromTrack;

        _ui.rootVisualElement.Q<Button>("RefreshOrigin").clicked += RunRefreshOrigin;
        _ui.rootVisualElement.Q<DoubleField>("LatitudeOrigin").RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == _submitKey) RunRefreshOrigin(); });
        _ui.rootVisualElement.Q<DoubleField>("LongitudeOrigin").RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == _submitKey) RunRefreshOrigin(); });
        _ui.rootVisualElement.Q<DoubleField>("AltitudeOrigin").RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == _submitKey) RunRefreshOrigin(); });

        _ui.rootVisualElement.Q<DropdownField>("WorldHoverCategory").RegisterCallback<ChangeEvent<string>>(e => { RunChangeHoverCategory(e.newValue); });

        // Preconfiguring UI
        _settingsView.SetEnabled(false);
        _settingsView.visible = false;

        _dataProcessor._onNewPacket.AddListener(PacketReceive);
        _dataProcessor._onBadPacket.AddListener(BadPacketReceive);
    }

    private void OnEnable()
    {
        _actionMousePosition.Enable();
    }

    private void OnDisable()
    {
        _actionMousePosition.Disable();
    }

    private void FixedUpdate()
    {
        _noDataAlert.style.display = _dataProcessor._hasData ? DisplayStyle.None : DisplayStyle.Flex;
        _serialReadAlert.style.display = _dataProcessor.IsListeningSerial() ? DisplayStyle.Flex : DisplayStyle.None;

        if (_packetPingTime > 0f) _packetPingTime -= Time.fixedDeltaTime;
        _packetPing.tintColor = new Color(1f, 1f, 1f, _packetPingTime);
    }

    private void Update()
    {
        if (_visualizationConfig._highlightedNode != null && !_visualizationConfig._highlightedNodeLock) _visualizationConfig._highlightInfoPosition = _actionMousePosition.ReadValue<Vector2>();

        _worldHoverInfo.visible = _visualizationConfig._highlightedNode != null;
        _worldHoverInfo.enabledSelf = _visualizationConfig._highlightedNode != null;
    }

    #region UI Actions
    // Open Close
    public void OpenSettings()
    {
        _settingsView.SetEnabled(true);
        _settingsView.visible = true;

        if (OrbitalCameraControls.PriorityInstance != null) OrbitalCameraControls.PriorityInstance.SetInputFreeze(0, true);

        _visualizationConfig._highlightedNodeLock = false;
    }

    public void ToggleSettings()
    {
        bool previous = _settingsView.enabledSelf;

        _settingsView.SetEnabled(!previous);
        _settingsView.visible = !previous;

        if (OrbitalCameraControls.PriorityInstance != null) OrbitalCameraControls.PriorityInstance.SetInputFreeze(0, !previous);

        if (!previous) _visualizationConfig._highlightedNodeLock = false;
    }

    public void CloseSettings()
    {
        _settingsView.SetEnabled(false);
        _settingsView.visible = false;

        if (OrbitalCameraControls.PriorityInstance != null) OrbitalCameraControls.PriorityInstance.SetInputFreeze(0, false);
    }

    // File
    public void RunImportRead()
    {
        if (_dataProcessor != null) _dataProcessor.ReadRecording();
    }

    public void RunImportFileSelect()
    {
        ExtensionFilter[] filters = new ExtensionFilter[2] {
            new ExtensionFilter("Log Files ", "log", "txt", "json"),
            new ExtensionFilter("All Files ", "*")
        };

        try
        {
            string[] paths = StandaloneFileBrowser.OpenFilePanel("Open Flight Recording", _dataProcessor._config._path, filters, false);
            if (paths.Length > 0 && paths[0] != null) _dataProcessor._config._path = paths[0];
        } catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    // Serial Port
    public void RunSerialStart()
    {
        if (_dataProcessor != null) _dataProcessor.StartSerial();
    }

    public void RunSerialStop()
    {
        if (_dataProcessor != null) _dataProcessor.StopSerial();
    }

    // Export
    public void RunExport()
    {
        if (_dataProcessor != null) _dataProcessor.ExportData();
    }

    public void RunExportFileSelect()
    {
        ExtensionFilter[] extensions = new ExtensionFilter[3] {
            new ExtensionFilter("Log ", "log"),
            new ExtensionFilter("Json ", "json"),
            new ExtensionFilter("Text ", "txt")
        };

        try
        {
            string path = StandaloneFileBrowser.SaveFilePanel("Export Flight Recording", _dataProcessor._config._exportPath, "CansatFlight", extensions);
            if (path != null) _dataProcessor._config._exportPath = path;
        } catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    public void RunDataClear()
    {
        if (_dataProcessor != null) _dataProcessor.ClearData();
    }

    // Origin
    public void RunUpdateOriginFromUser()
    {
        if (_positioning != null) _positioning.UpdateOriginFromUser();
    }

    public void RunUpdateOriginFromTrack()
    {
        if (_positioning != null) _positioning.UpdateOriginFromTrack();
    }

    public void RunRefreshOrigin()
    {
        if (_positioning != null) _positioning.ApplyOrigin();
    }

    // Playback
    public void RunSnapToFront()
    {
        if (_playback != null) _playback.ToFront();
    }

    // Data Visualization
    public void RunChangeHoverCategory(string category)
    {
        Debug.Log($"Category changed to {category}");
        _visualizationConfig._activeMeasureCategory = CansatDataHelpers.DynamicDataMappings[category];

        _visualization.RefreshDataPlot();
        if (_visualization._visualization._highlightedNode != null) _visualization._visualization._highlightedNode.PushData();
    }
    #endregion

    #region External Actions
    public void PlaybackValueChange(ToggleButtonGroupState state)
    {
        if (state.length != 6)
        {
            Debug.LogError("Invalid Playback State provided.");
            return;
        }

        _playbackStateControl.value = state;
    }

    public void PacketReceive(bool incremental)
    {
        _packetPing.sprite = _packetIcon;
        _packetPingTime = 1f;
    }

    public void BadPacketReceive()
    {
        _packetPing.sprite = _badPacketIcon;
        _packetPingTime = 1f;
    }
    #endregion
}
