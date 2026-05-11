using SFB;
using System;
using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class MainUIHandler : MonoBehaviour
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

    [Header("Components")]
    [SerializeField] private UIDocument _ui;

    [Space(10)]
    [SerializeField] private KeyCode _submitKey;

    // UI Reference
    private VisualElement _settingsView;
    private ToggleButtonGroup _playbackStateControl;

    private VisualElement _noDataAlert;
    private VisualElement _serialReadAlert;
    private Image _packetPing;

    // Timing
    private float _packetPingTime = 0f;

    private IEnumerator Start()
    {
        while (_ui.rootVisualElement.childCount == 0) yield return null;

        _settingsView = _ui.rootVisualElement.Q<VisualElement>("Settings");

        _noDataAlert = _ui.rootVisualElement.Q<VisualElement>("EmptyDataIdentifier");
        _serialReadAlert = _ui.rootVisualElement.Q<VisualElement>("SerialListenIdentifier");
        _packetPing = _ui.rootVisualElement.Q<Image>("PacketPing");

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

        // Preconfiguring UI
        _settingsView.SetEnabled(false);
        _settingsView.visible = false;

        _dataProcessor._onNewPacket.AddListener(PacketReceive);
        _dataProcessor._onBadPacket.AddListener(BadPacketReceive);
    }

    private void FixedUpdate()
    {
        _noDataAlert.visible = !_dataProcessor._hasData;
        _serialReadAlert.visible = _dataProcessor.IsListeningSerial();

        if (_packetPingTime > 0f) _packetPingTime -= Time.fixedDeltaTime;
        _packetPing.tintColor = new Color(1f, 1f, 1f, _packetPingTime);
    }

    #region UI Actions
    // Open Close
    public void OpenSettings()
    {
        _settingsView.SetEnabled(true);
        _settingsView.visible = true;

        if (_cameraControls != null) _cameraControls.SetInputFreeze(0, true);
    }

    public void ToggleSettings()
    {
        bool previous = _settingsView.enabledSelf;

        _settingsView.SetEnabled(!previous);
        _settingsView.visible = !previous;

        if (_cameraControls != null) _cameraControls.SetInputFreeze(0, !previous);
    }

    public void CloseSettings()
    {
        _settingsView.SetEnabled(false);
        _settingsView.visible = false;

        if (_cameraControls != null) _cameraControls.SetInputFreeze(0, false);
    }

    // File
    public void RunImportRead()
    {
        if (_dataProcessor != null) _dataProcessor.ReadRecording();
    }

    public void RunImportFileSelect()
    {
        ExtensionFilter[] filters = new ExtensionFilter[2] {
            new ExtensionFilter("Log Files", "log", "txt", "json"),
            new ExtensionFilter("All Files", "*")
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
            new ExtensionFilter("Log", "log"),
            new ExtensionFilter("Json", "json"),
            new ExtensionFilter("Text", "txt")
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
    #endregion

    #region External Actions
    public void PlaybackValueChange(ToggleButtonGroupState state)
    {
        if (state.length != 5)
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
