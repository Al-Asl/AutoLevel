using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

[RequireComponent(typeof(CinemachineVirtualCamera))]
public class CameraController : MonoBehaviour
{
    [Header("Zoom")]
    public float TargetZoomLevel = 10f;
    public float MinZoom = 1f;
    public float MaxZoom = 10f;
    public float ZoomDamp = 1f;
    public float ScrollSpeed = 10f;
    [Header("Pan")]
    public float MaxSpeed = 10f;
    public float deceleration = 0.5f;
    public BoxCollider Bound;
    [Space]
    public float planeHeight = 0;

    private CinemachineVirtualCamera vcam;
    private CinemachineTransposer transposer;
    private Camera cam;

    private const int mouseButton = 1;
    private bool dragging;
    private Vector3 targetStart;
    private Vector2 mouseStart;
    private Vector3 velocity;
    private float ZoomLevel;
    private bool clampEnable;
    private Bounds bounds;
    private Plane raycastPlane;

    private void Start()
    {
        raycastPlane = new Plane(Vector3.up, Vector3.up * planeHeight);
        vcam = GetComponent<CinemachineVirtualCamera>();
        transposer = vcam.GetCinemachineComponent<CinemachineTransposer>();
        cam = Camera.main;

        if (Bound != null)
        {
            bounds = Bound.bounds;
            clampEnable = true;
            Destroy(Bound.gameObject);
        }

        ZoomLevel = TargetZoomLevel;
    }

    private void SetZoomLevel()
    {
        transposer.m_FollowOffset.Scale(Vector3.one * (ZoomLevel / transposer.m_FollowOffset.magnitude));
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(mouseButton))
        {
            dragging = true;
            mouseStart = Input.mousePosition;
            targetStart = vcam.Follow.position;
            velocity = Vector3.zero;
        }
        if (Input.GetMouseButtonUp(mouseButton))
        {
            dragging = false;
        }

        TargetZoomLevel -= Input.mouseScrollDelta.y * ScrollSpeed * Time.deltaTime;
        TargetZoomLevel = Mathf.Clamp(TargetZoomLevel, MinZoom, MaxZoom);

        var d = TargetZoomLevel - ZoomLevel;
        d = Cinemachine.Utility.Damper.Damp(d, ZoomDamp, Time.deltaTime);
        ZoomLevel = ZoomLevel + d;
        SetZoomLevel();

        var oldPos = vcam.m_Follow.position;
        Vector3 newPos = oldPos;

        if (dragging)
        {
            if (Raycast(Input.mousePosition, out var point))
            {
                Raycast(mouseStart, out var startPoint);
                var delta = point - startPoint;
                delta.y = 0;

                newPos = targetStart - delta;
                velocity = (newPos - oldPos) * (1f / Time.deltaTime);
            }
        }
        else
        {
            newPos += velocity * Time.deltaTime;
            float speed = Mathf.Min(MaxSpeed, velocity.magnitude);
            velocity = velocity.normalized * (Mathf.Max(0, speed - deceleration));
            //velocity.Scale(Vector3.one * deceleration);
        }

        if (clampEnable)
        {
            if (!bounds.Contains(newPos))
                newPos = bounds.ClosestPoint(newPos);
        }
        vcam.m_Follow.position = newPos;
    }

    bool Raycast(Vector2 screenPos, out Vector3 point)
    {
        var ray = cam.ScreenPointToRay(screenPos);
        bool success = raycastPlane.Raycast(ray, out var dist);
        point = ray.GetPoint(dist);
        return success;
    }
}