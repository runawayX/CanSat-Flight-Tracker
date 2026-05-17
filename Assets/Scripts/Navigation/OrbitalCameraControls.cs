using CesiumForUnity;
using System.Linq;
using Unity.Cinemachine;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

using static CansatDataHelpers;

public class OrbitalCameraControls : MonoBehaviour
{
    public static OrbitalCameraControls PriorityInstance { get; private set; } = null;

    [Header("Configuration")]
    public float _moveSpeed;
    public float _slowMultiplierSpeed;

    [Space(10)]
    public double _groundHeight;
    [SerializeField] private LayerMask _groundLayer;

    [Space(10)]
    [SerializeField] private float _groundedAngleLimit;
    [SerializeField] private float _defaultAngleLimit;

    [Space(10)]
    [SerializeField] private Vector2 _zoomBounds;

    [Space(10)]
    public AnimationCurve _snapSmoothing;

    [Space(10)]
    public Transform _defaultLockTarget;

    [Header("Components")]
    public CesiumGeoreference _map;

    [Space(10)]
    [SerializeField] private InputActionReference _pan;

    [Space(10)]
    [SerializeField] private InputActionReference _moveOrbit;
    [SerializeField] private InputActionReference _flyOrbit;
    [SerializeField] private InputActionReference _moveOrbitSpeedModifier;

    [Space(10)]
    [SerializeField] private InputActionReference _zoom;

    [Space(10)]
    [SerializeField] private InputActionReference _relock;
    [SerializeField] private InputActionReference _modifierKey;

    //[Header("Runtime")]
    public bool _lockedOnTarget { get; private set; } = false;

    // Runtime
    public Vector3 _origin { get; private set; }
    public Transform _lockTarget { get; private set; }
    public CesiumGlobeAnchor _lockTargetGPS { get; private set; }

    private float _smoothingTime = 1f;
    private float _maxSmoothingTime;
    private double3 _smoothingReference = double3.zero;
    private double3 _smoothingStaticTarget = double3.zero;
    private float _smoothingZoomReference = 1f;
    private float _smoothingZoomStaticTarget = 1f;

    // Internal Components
    private CesiumGlobeAnchor _orbit;

    private Transform _cameraTransform;
    private CinemachineOrbitalFollow _cameraOrbiter;
    private CinemachineInputAxisController _cameraInput;

    // Input
    private InputAction _actionPan;

    private InputAction _actionMoveOrbit;
    private InputAction _actionFlyOrbit;
    private InputAction _actionMoveOrbitSlow;

    private InputAction _actionZoom;

    private InputAction _actionRelock;
    private InputAction _actionModifierKey;

    private int _inputFreezeFlags = 0;

    private void Awake()
    {
        if (PriorityInstance == null) PriorityInstance = this;

        // Bind actions
        _actionPan = _pan.action;
        _actionMoveOrbit = _moveOrbit.action;
        _actionFlyOrbit = _flyOrbit.action;
        _actionMoveOrbitSlow = _moveOrbitSpeedModifier.action;
        _actionZoom = _zoom.action;
        _actionRelock = _relock.action;
        _actionModifierKey = _modifierKey.action;

        // Setup components
        _orbit = gameObject.GetComponent<CesiumGlobeAnchor>();

        _cameraTransform = transform.GetChild(0);
        _cameraOrbiter = _cameraTransform.gameObject.GetComponent<CinemachineOrbitalFollow>();
        _cameraInput = _cameraTransform.gameObject.GetComponent<CinemachineInputAxisController>();

        if (_cameraTransform == null ||  _cameraInput == null || _cameraOrbiter == null)
        {
            Debug.LogWarning("Missing or misconfigured Orbital Camera!");
            enabled = false;
        }

        // Setup visual
        _smoothingReference = _orbit.longitudeLatitudeHeight;
        _smoothingStaticTarget = _orbit.longitudeLatitudeHeight;

        _maxSmoothingTime = _snapSmoothing.keys.Last().time;
        _smoothingTime = _maxSmoothingTime;

        if (_defaultLockTarget != null) _lockTarget = _defaultLockTarget;
    }

    private void OnEnable()
    {
        _actionMoveOrbit.Enable();
        _actionFlyOrbit.Enable();
        _actionMoveOrbitSlow.Enable();

        _actionZoom.Enable();

        _actionRelock.Enable();

        // Linking
        _actionRelock.performed += OnRelockPressed;
        _actionMoveOrbit.performed += OnMoveAnyPressed;
    }

    private void OnDisable()
    {
        _actionMoveOrbit.Disable();
        _actionFlyOrbit.Disable();
        _actionMoveOrbitSlow.Disable();

        _actionZoom.Disable();

        _actionRelock.Disable();

        // Delinking
        _actionRelock.performed -= OnRelockPressed;
        _actionMoveOrbit.performed -= OnMoveAnyPressed;
    }

    private void OnDestroy()
    {
        if (PriorityInstance == this) PriorityInstance = null;
    }

    private void Update()
    {
        _cameraInput.enabled = _actionPan.IsPressed() && !EventSystem.current.IsPointerOverGameObject() && _inputFreezeFlags == 0;

        if (!_lockedOnTarget)
        {
            Vector2 move = Vector2.zero;
            float fly = 0f;

            if (_inputFreezeFlags == 0)
            {
                move = _actionMoveOrbit.ReadValue<Vector2>();
                fly = _actionFlyOrbit.ReadValue<float>();
            }

            move *= _moveSpeed;
            fly *= _moveSpeed;
            if (_actionMoveOrbitSlow.IsPressed())
            {
                move *= _slowMultiplierSpeed;
                fly *= _slowMultiplierSpeed;
            }

            Vector2 forward = move.y * new Vector2(_cameraTransform.forward.x, _cameraTransform.forward.z);
            Vector2 right = move.x * new Vector2(_cameraTransform.right.x, _cameraTransform.right.z);

            if (_smoothingTime < _maxSmoothingTime)
            {
                _orbit.longitudeLatitudeHeight = LerpDouble3(_smoothingReference, _smoothingStaticTarget, _snapSmoothing.Evaluate(_smoothingTime));
                _cameraOrbiter.Radius = Mathf.Clamp(Mathf.Lerp(_smoothingZoomReference, _smoothingZoomStaticTarget, _snapSmoothing.Evaluate(_smoothingTime)), _zoomBounds.x, _zoomBounds.y);
            }
            else
            {
                TranslateGPS(_orbit, (forward + right) * Time.deltaTime);
                _orbit.longitudeLatitudeHeight += new double3(0, 0, 1) * fly * Time.deltaTime;
                _cameraOrbiter.Radius = Mathf.Clamp(_cameraOrbiter.Radius - _actionZoom.ReadValue<float>() * (_inputFreezeFlags == 0 ? 1 : 0), _zoomBounds.x, _zoomBounds.y);
            }
            
            //_orbit.longitudeLatitudeHeight += (forward + right + new double3(0, 0, 1) * fly) * Time.deltaTime;

            _orbit.longitudeLatitudeHeight = new double3(_orbit.longitudeLatitudeHeight.x, _orbit.longitudeLatitudeHeight.y, _orbit.longitudeLatitudeHeight.z >= _groundHeight ? _orbit.longitudeLatitudeHeight.z : _groundHeight);
        } else
        {
            if (_lockTargetGPS == null) {
                double3 trackingECEF = _map.TransformUnityPositionToEarthCenteredEarthFixed(new double3(_lockTarget.position.x, _lockTarget.position.y, _lockTarget.position.z));
                double3 trackingGPS = CesiumWgs84Ellipsoid.EarthCenteredEarthFixedToLongitudeLatitudeHeight(trackingECEF);

                _orbit.longitudeLatitudeHeight = LerpDouble3(_smoothingReference, trackingGPS, _snapSmoothing.Evaluate(_smoothingTime));
            }
            else
            {
                _orbit.longitudeLatitudeHeight = LerpDouble3(_smoothingReference, _lockTargetGPS.longitudeLatitudeHeight, _snapSmoothing.Evaluate(_smoothingTime));
            }

            if (_smoothingTime < _maxSmoothingTime) _cameraOrbiter.Radius = Mathf.Clamp(Mathf.Lerp(_smoothingZoomReference, _smoothingZoomStaticTarget, _snapSmoothing.Evaluate(_smoothingTime)), _zoomBounds.x, _zoomBounds.y);
            else _cameraOrbiter.Radius = Mathf.Clamp(_cameraOrbiter.Radius - _actionZoom.ReadValue<float>() * (_inputFreezeFlags == 0 ? 1 : 0), _zoomBounds.x, _zoomBounds.y);
        }

        _cameraOrbiter.VerticalAxis.Range.x = Mathf.Lerp(_groundedAngleLimit, _defaultAngleLimit, Mathf.Abs((float) (_orbit.longitudeLatitudeHeight.z - _groundHeight)) / _cameraOrbiter.Radius);

        if (_smoothingTime < _maxSmoothingTime) _smoothingTime += Time.deltaTime;
    }

    private void FixedUpdate()
    {
        //_groundHeight = (float) _map;
        if (Physics.Raycast(_orbit.transform.position + Vector3.up * 10000, Vector3.down, out RaycastHit hit, Mathf.Infinity, _groundLayer, QueryTriggerInteraction.Ignore))
            _groundHeight = _map.height + hit.point.y;
    }

    // Input Handlers

    private void OnRelockPressed(InputAction.CallbackContext c)
    {
        if (_inputFreezeFlags != 0) return;

        if (_actionModifierKey.IsPressed()) SelectLockTarget(_defaultLockTarget);
        else if (_lockTarget != null) SelectLockTarget(_lockTarget);
        else JumpToLocation(_origin);
        
        Debug.Log("Returning to lock target or origin.");
    }

    private void OnMoveAnyPressed(InputAction.CallbackContext c)
    {
        if (_inputFreezeFlags == 0) _lockedOnTarget = false;
    }

    // Accessors

    public void SelectLockTarget(Transform lockTarget)
    {
        if (lockTarget == null) _lockedOnTarget = false;
        else _lockedOnTarget = true;
        
        _lockTarget = lockTarget;
        if (lockTarget.gameObject.TryGetComponent(out CesiumGlobeAnchor gpsTarget)) _lockTargetGPS = gpsTarget;
        _smoothingZoomStaticTarget = lockTarget.localScale.magnitude * 2;

        _smoothingReference = _orbit.longitudeLatitudeHeight;
        _smoothingZoomReference = _cameraOrbiter.Radius;
        _smoothingTime = 0;
    }

    public void JumpToLocation(Vector3 target)
    {
        _lockedOnTarget = false;

        double3 targetECEF = _map.TransformUnityPositionToEarthCenteredEarthFixed(new double3(target.x, target.y, target.z));
        double3 targetGPS = CesiumWgs84Ellipsoid.EarthCenteredEarthFixedToLongitudeLatitudeHeight(targetECEF);
        _smoothingStaticTarget = targetGPS;
        _smoothingZoomStaticTarget = _cameraOrbiter.Radius;

        _smoothingReference = _orbit.longitudeLatitudeHeight;
        _smoothingZoomReference = _smoothingZoomStaticTarget; // dont lerp
        _smoothingTime = 0;
    }

    public void SetInputFreeze(int flagID, bool freeze)
    {
        if (flagID < 0 || flagID > 7) Debug.LogError("Tried to set out of bounds input freeze flag!");

        if (freeze) _inputFreezeFlags |= 1 << flagID;
        else _inputFreezeFlags &= ~(1 << flagID);
    }
}
