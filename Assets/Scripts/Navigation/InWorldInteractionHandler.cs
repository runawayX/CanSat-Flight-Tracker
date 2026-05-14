using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class InWorldInteractionHandler : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private LayerMask _clickLayers;

    [Header("Components")]
    [SerializeField] private PlaybackManager _playback;
    [SerializeField] private VisualConfiguration _visualization;

    [Space(10)]
    [SerializeField] private InputActionReference _mousePos;

    [Space(10)]
    [SerializeField] private InputActionReference _click;
    [SerializeField] private InputActionReference _doubleClick;

    // Internal Components
    private Camera _view;
    private OrbitalCameraControls _orbiter;

    // Input
    private InputAction _actionMousePos;

    private InputAction _actionClick;
    private InputAction _actionDoubleClick;

    private void Awake()
    {
        _actionMousePos = _mousePos.action;

        _actionClick = _click.action;
        _actionDoubleClick = _doubleClick.action;

        _view = Camera.main;
        _orbiter = gameObject.GetComponent<OrbitalCameraControls>();
    }

    private void FixedUpdate()
    {
        if (TraceClick(out RaycastHit target) && target.collider.gameObject.CompareTag("Datapoint"))
        {
            if (_visualization._highlightedNode == null)
            {
                _visualization._highlightedNode = target.collider.gameObject.GetComponent<WorldspaceDatapoint>();
                _visualization._highlightedNode.PushData();
            }
        } else if (!_visualization._highlightedNodeLock)
        {
            _visualization._highlightedNode = null;
        }
    }

    private void OnEnable()
    {
        _actionMousePos.Enable();

        _actionClick.Enable();
        _actionDoubleClick.Enable();

        // Linking
        _actionClick.performed += OnClickPressed;
        _actionDoubleClick.performed += OnDoubleClickPressed;
    }

    private void OnDisable()
    {
        _actionMousePos.Disable();

        _actionClick.Disable();
        _actionDoubleClick.Disable();

        // Delinking
        _actionClick.performed -= OnClickPressed;
        _actionDoubleClick.performed -= OnDoubleClickPressed;
    }

    // Internal Helpers

    private bool TraceClick(out RaycastHit trace)
    {
        Ray ct = _view.ScreenPointToRay(_actionMousePos.ReadValue<Vector2>());

        bool result = Physics.Raycast(ct, out RaycastHit t, Mathf.Infinity, _clickLayers, QueryTriggerInteraction.Ignore);
        trace = t;

        return result;
    }

    // Input Handlers

    public void OnClickPressed(InputAction.CallbackContext c)
    {
        if (EventSystem.current.IsPointerOverGameObject()) return;

        if (_visualization._highlightedNodeLock) _visualization._highlightedNodeLock = false;
        else if (_visualization._highlightedNode != null) _visualization._highlightedNodeLock = true;
    }

    public void OnDoubleClickPressed(InputAction.CallbackContext c)
    {
        if (EventSystem.current.IsPointerOverGameObject() || !TraceClick(out RaycastHit trace)) return;

        if (trace.collider.gameObject.CompareTag("Focusable")) _orbiter.SelectLockTarget(trace.collider.gameObject.transform);
        else if (_visualization._highlightedNode != null) _playback.JumpTime(_visualization._highlightedNode._time_ms);
        else _orbiter.JumpToLocation(trace.point);
    }
}
