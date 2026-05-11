using UnityEngine;

public class WorldBillboard : MonoBehaviour
{
    public bool _uniformScreenScale = true;
    [SerializeField] private Vector2 _uniformScaleBounds = Vector2.up;

    [Space(5)]
    [Tooltip("The local scale of the GameObject at distance of 1 from the camera")] [SerializeField] private Vector3 _scaleAtUnitDistance = Vector3.one;

    private static Transform _camera;

    private void Awake()
    {
        if (_camera == null) _camera = Camera.main.transform;
    }

    private void LateUpdate()
    {
        if (_camera == null) return;
        transform.rotation = _camera.rotation;
        if (_uniformScreenScale) transform.localScale = _scaleAtUnitDistance * Mathf.Clamp(Vector3.Distance(transform.position, _camera.position), _uniformScaleBounds.x, _uniformScaleBounds.y);
    }
}
