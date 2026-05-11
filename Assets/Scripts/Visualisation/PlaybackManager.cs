using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/*
public static class PlaybackConversionRegister
{
#if UNITY_EDITOR
    [InitializeOnLoadMethod]
#endif
    [RuntimeInitializeOnLoadMethod]
    public static void Init()
    {
        _maxPlaybackState = Enum.GetValues(typeof(PlaybackConfiguration.PlaybackState)).Length;

        ConverterGroup playbackGroup = new ConverterGroup("PlaybackDataConverters");

        playbackGroup.AddConverter((ref PlaybackConfiguration.PlaybackState s) => GetStateBitmask(s));
        playbackGroup.AddConverter((ref ToggleButtonGroupState m) => GetStateFromBitmask(ref m));

        ConverterGroups.RegisterConverterGroup(playbackGroup);
    }

    private static int _maxPlaybackState;
    public static PlaybackConfiguration.PlaybackState GetStateFromBitmask(ref ToggleButtonGroupState m)
    {
        for (int id = 0; id < _maxPlaybackState; id++) if (m[id]) return (PlaybackConfiguration.PlaybackState) id;
        return PlaybackConfiguration.PlaybackState.Stopped; // default to stop
    }

    public static ToggleButtonGroupState GetStateBitmask(PlaybackConfiguration.PlaybackState s)
    {
        ToggleButtonGroupState m = new ToggleButtonGroupState(0, _maxPlaybackState);
        m[(int) s] = true;
        return m;
    }
} */

public class PlaybackManager : MonoBehaviour
{
    [Header("Components")]
    public PlaybackConfiguration _playback;
    public DataProcessor _env;

    [Space(10)]
    [SerializeField] private InputActionReference _playbackToggle;
    [SerializeField] private InputActionReference _playbackSmallStep;

    [Header("Events")]
    public UnityEvent<ToggleButtonGroupState> _onPlaybackChange;

    // Input
    private InputAction _actionPlaybackToggle;
    private InputAction _actionPlaybackSmallStep;

    // Change tracking
    private int _lastTime = 0;
    private int _lastDuration = 0;

    private void Awake()
    {
        _actionPlaybackToggle = _playbackToggle.action;
        _actionPlaybackSmallStep = _playbackSmallStep.action;

        if (_playback == null || _env == null)
        {
            Debug.LogError("Playback environment not initialized!");
            enabled = false;
        }

        _playback._currentTime_ms = 0;
        _playback._duration_ms = 0;

        _env._onNewPacket.AddListener(OnKeyAdded);
        _env._onClearDatabase.AddListener(() => {
            _playback._currentTime_ms = 0;
            _playback._duration_ms = 0;
        });
    }

    private void OnEnable()
    {
        _actionPlaybackToggle.Enable();
        _actionPlaybackSmallStep.Enable();

        // Linking
        _actionPlaybackToggle.performed += TogglePlayback;
        _actionPlaybackSmallStep.performed += (InputAction.CallbackContext c) => { ManualStepPlayback((int) _actionPlaybackSmallStep.ReadValue<float>()); };
    }

    private void OnDisable()
    {
        _actionPlaybackToggle.Disable();
        _actionPlaybackSmallStep.Disable();

        // Delinking
        _actionPlaybackToggle.performed -= TogglePlayback;
        _actionPlaybackSmallStep.performed -= (InputAction.CallbackContext c) => { ManualStepPlayback((int) _actionPlaybackSmallStep.ReadValue<float>()); };
    }

    private void Start()
    {
        _playback._state = PlaybackConfiguration.PlaybackState.Stopped; // pause on load
        _onPlaybackChange.Invoke(_playback.GetStateAsMask());
    }

    private void Update()
    {
        StepPlayback();

        if (_playback._currentTime_ms != _lastTime || _playback._duration_ms != _lastDuration)
        {
            _playback._formattedPlaybackTime = GetFormattedTime();
        }

        _lastTime = _playback._currentTime_ms;
        _lastDuration = _playback._duration_ms;
    }

    // Internal
    private void StepPlayback()
    {
        switch (_playback._state)
        {
            case PlaybackConfiguration.PlaybackState.Forward:
                _playback._currentTime_ms = Math.Min(_playback._duration_ms, _playback._currentTime_ms + (int) (Time.deltaTime * 1000));
                break;
            case PlaybackConfiguration.PlaybackState.FastForward:
                _playback._currentTime_ms = Math.Min(_playback._duration_ms, _playback._currentTime_ms + (int) (Time.deltaTime * _playback._fastMultiplier * 1000));
                break;
            case PlaybackConfiguration.PlaybackState.Rewind:
                _playback._currentTime_ms = Math.Max(0, _playback._currentTime_ms - (int) (Time.deltaTime * 1000));
                break;
            case PlaybackConfiguration.PlaybackState.FastRewind:
                _playback._currentTime_ms = Math.Max(0, _playback._currentTime_ms - (int) (Time.deltaTime * _playback._fastMultiplier * 1000));
                break;
            default:
                break;
        }
    }

    // Helpers
    public string GetFormattedTime()
    {
        TimeSpan c = TimeSpan.FromMilliseconds(_playback._currentTime_ms);
        TimeSpan d = TimeSpan.FromMilliseconds(_playback._duration_ms);
        
        return $"{c.ToString(@"hh\:mm\:ss\.ff")}/{d.ToString(@"hh\:mm\:ss")}";
    }

    // Access & Management
    public void OnKeyAdded(bool incremental)
    {
        _playback._duration_ms = _env.TotalTime();
    }

    public void TogglePlayback(InputAction.CallbackContext c)
    {
        if (_playback._state == PlaybackConfiguration.PlaybackState.Stopped) _playback._state = PlaybackConfiguration.PlaybackState.Forward;
        else _playback._state = PlaybackConfiguration.PlaybackState.Stopped;

        _onPlaybackChange.Invoke(_playback.GetStateAsMask());
    }

    public void ManualStepPlayback(int direction)
    {
        _playback._currentTime_ms = Math.Clamp(_playback._currentTime_ms + direction * _playback._smallStep_ms, 0, _playback._duration_ms);
    }

    public void ToFront()
    {
        _playback._currentTime_ms = _playback._duration_ms;
    }
}
