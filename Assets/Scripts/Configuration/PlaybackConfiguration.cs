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
    }

    [Header("Configuration")]
    public PlaybackState _state;
    public float _fastMultiplier;

    public int _currentTime_ms;
    public int _duration_ms;

    [Space(5)]
    public int _smallStep_ms;

    [Header("GUI")]
    public string _formattedPlaybackTime = "0:00:00.00/0:00:00";

    public ToggleButtonGroupState GetStateAsMask()
    {
        ToggleButtonGroupState stateMask = new ToggleButtonGroupState(0b00000, 5);
        stateMask[(int) _state] = true;
        return stateMask;
    }

    public void SetStateFromMask(ToggleButtonGroupState stateMask)
    {
        //Debug.Log($"SO received message, changing to {stateMask}.");
        for (int id = 0; id < 5; id++) if (stateMask[id])
        {
            _state = (PlaybackState) id;
            return;
        }

        _state = PlaybackState.Stopped;
    }
}