using UnityEngine;

public class TrackTargetBillboard : MonoBehaviour
{
    [Header("Visuals")]
    [SerializeField] private Sprite _idleSprite;
    [SerializeField] private Sprite _trackingSprite;

    [Space(10)]
    [SerializeField] private Color _idleColor;
    [SerializeField] private Color _trackingColor;

    [Header("Components")]
    [SerializeField] private OrbitalCameraControls _controls;
    [SerializeField] private Transform _billboard;
    [SerializeField] private SpriteRenderer _billboardRenderer;
    [SerializeField] private Transform _defaultFocus;

    private void Start()
    {
        if (_controls._defaultLockTarget != null) _defaultFocus = _controls._defaultLockTarget;
    }

    private void Update()
    {
        if (_controls == null || _billboard == null) return;

        //_billboard.gameObject.SetActive(_controls._lockedOnTarget);

        if (_controls._lockTarget != null) _billboard.transform.position = _controls._lockTarget.position;
        else _billboard.transform.position = _defaultFocus.position;

        if (_controls._lockedOnTarget)
        {
            _billboardRenderer.sprite = _trackingSprite;
            _billboardRenderer.color = _trackingColor;
        }
        else
        {
            _billboardRenderer.sprite = _idleSprite;
            _billboardRenderer.color = _idleColor;
        }
    }
}
