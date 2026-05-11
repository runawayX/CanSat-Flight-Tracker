using UnityEngine;

[CreateAssetMenu(fileName = "New Positioning Configuration", menuName = "Cansat/Positioning Configuration")]
public class PositioningConfiguration : ScriptableObject
{
    public double _originLatitude;
    public double _originLongitude;
    public double _originAltitude;

    public float _originAccuracy;
}