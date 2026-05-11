using CesiumForUnity;
using UnityEngine;
using Unity.Mathematics;

public class WorldPositioningManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CesiumGlobeAnchor _cameraOrbit;
    [SerializeField] private CesiumGlobeAnchor _originPointer;

    [Space(10)]
    [SerializeField] private Transform _travellerNative;
    [SerializeField] private CesiumGlobeAnchor _traveller;

    [Header("Components")]
    public PositioningConfiguration _config;
    public PlaybackConfiguration _playback;
    public DataProcessor _data;

    [Space(10)]
    public CesiumGeoreference _map;

    // Internal Trackers
    private int _lastFrameTimestamp = 0;

    private void Awake()
    {
        if (_config == null || _playback == null || _data == null)
        {
            Debug.LogError("Misconfigured Positioning Manager.");
            enabled = false;
        }
    }

    private void Update()
    {
        if (_playback._state != PlaybackConfiguration.PlaybackState.Stopped || _lastFrameTimestamp != _playback._currentTime_ms) RefreshTravellerTransform();
        _lastFrameTimestamp = _playback._currentTime_ms;
    }

    // Origin Handling
    public void UpdateOriginFromUser()
    {
        Debug.Log($"Location service state - {Input.location.status}");
        if (Input.location.status != LocationServiceStatus.Running)
        {
            Input.location.Start(_config._originAccuracy);
            Debug.Log("Initializing location service...");
        }

        LocationInfo l = Input.location.lastData;
        _config._originLatitude = l.latitude;
        _config._originLongitude = l.longitude;
        _config._originAltitude = l.altitude;
    }

    public void UpdateOriginFromTrack()
    {
        if (_data.EvaluateLocationGPS(0, out double3 fetchedOrigin))
        {
            _config._originLatitude = fetchedOrigin.y;
            _config._originLongitude = fetchedOrigin.x;
            _config._originAltitude = fetchedOrigin.z;
        } else
        {
            Debug.Log("No data has been read yet.");
        }
    }

    public void ApplyOrigin()
    {
        Debug.Log("Applying origin.");

        double3 newOrigin = new double3(_config._originLongitude, _config._originLatitude, _config._originAltitude);

        _map.SetOriginLongitudeLatitudeHeight(_config._originLongitude, _config._originLatitude, _config._originAltitude);
        _cameraOrbit.longitudeLatitudeHeight = newOrigin;
        _originPointer.longitudeLatitudeHeight = newOrigin;
    }

    // Traveller Position
    public void RefreshTravellerTransform()
    {
        if (_traveller == null || _travellerNative == null)
        {
            Debug.LogError("Traveller is not assigned!");
            return;
        }

        if (_data.EvaluateTransform(_playback._currentTime_ms, out GeoTransform newTransform))
        {
            _traveller.longitudeLatitudeHeight = newTransform.lonLatAlt;
            _travellerNative.rotation = newTransform.localRotation;
        }
    }
}
