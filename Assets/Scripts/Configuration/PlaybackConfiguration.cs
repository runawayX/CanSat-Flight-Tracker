using UnityEngine;
using UnityEngine.UIElements;

[CreateAssetMenu(fileName = "New Playback", menuName = "Cansat/Playback Configuration")]
public class PlaybackConfiguration : ScriptableObject
{
    public enum PlaybackState
    {
        FastRewind,
        Rewind,

        Stopped,

        Forward,
        FastForward,

        Live
    }

    [Header("Configuration")]
    public PlaybackState _state;
    public float _fastMultiplier;

    public int _currentTime_ms;
    public int _duration_ms;

    [Space(5)]
    public float _liveSeekTime = 1f;
    public int _smallStep_ms;

    [Header("GUI")]
    public string _formattedPlaybackTime = "0:00:00.00/0:00:00";

    public ToggleButtonGroupState GetStateAsMask()
    {
        ToggleButtonGroupState stateMask = new ToggleButtonGroupState(0b000000, 6);
        stateMask[(int) _state] = true;
        return stateMask;
    }

    public void SetStateFromMask(ToggleButtonGroupState stateMask)
    {
        //Debug.Log($"SO received message, changing to {stateMask}.");
        for (int id = 0; id < 6; id++) if (stateMask[id])
        {
            _state = (PlaybackState) id;
            return;
        }

        _state = PlaybackState.Stopped;
    }
}