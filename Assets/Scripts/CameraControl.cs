using System;
using UnityEngine;

public class CameraControl : MonoBehaviour
{
    public Transform Camera;

    static readonly float RAD_2_DEG = (float)(180 / Math.PI);
    static readonly float DEG_2_RAD = (float)(Math.PI / 180);

    public float OrbitScale;
    public float LerpSpeed;
    public float IdleOrbitSpeed;
    public float CameraPanSpeed;
    public float CameraRotateSpeed;
    public float ZoomMin, ZoomMax, ZoomScale;

    private Transform Socket;
    private Transform LastSocket;
    private Transform Home;

    //////////////////////////
    public Vector3 OldPosition, StartPosition;
    public Quaternion HomeRotation, OldRotation, StartRotation;
    public float oldZoom, targetZoomDistance, homeZoom, lerpTimer, lerpZoom;
    public bool unIdling = false;
    //////////////////////////
    private bool Idle => Socket == Home;


    public void GoHome()
    {
        SocketCamera(Home);
    }
    public void GoBack()
    {
        SocketCamera(LastSocket, true);
    }
    public void SocketCamera(Transform newSocket = null, bool goBack = false)
    {
        if (newSocket == Socket)
            return;

        // Pre-Checks
        unIdling = Idle && newSocket != Home;

        targetZoomDistance = newSocket == Home ? homeZoom : unIdling ? oldZoom : Camera.localPosition.z;
        OldPosition = goBack ? OldPosition : transform.localPosition;
        OldRotation = goBack ? OldRotation : transform.localRotation;

        oldZoom = Camera.localPosition.z;


        // Socket Change
        LastSocket = Socket;
        Socket = newSocket;
        transform.parent = Socket;

        // Post-Checks
        lerpTimer = unIdling || newSocket != null ? 1 : 0;
        StartPosition = transform.localPosition;
        StartRotation = transform.localRotation;
    }

    private void Zoom()
    {
        if (Camera == null ||
            Idle ||
            unIdling)
            return;

        float newZ = Camera.localPosition.z + (Input.GetAxis("Mouse ScrollWheel") * ZoomScale);
        newZ = newZ < ZoomMin ? ZoomMin : newZ > ZoomMax ? ZoomMax : newZ;
        Camera.localPosition = new Vector3(0, 0, newZ);
    }
    private void Orbit()
    {
        Vector3 eulers = Home.rotation.eulerAngles;
        eulers.y += IdleOrbitSpeed;
        Home.rotation = Quaternion.Euler(eulers);

        if (!Idle)
        {
            int middle = Input.GetMouseButton(2) ? 1 : 0;

            transform.Rotate(Vector3.up, Input.GetAxis("Mouse X") * middle * OrbitScale, Space.World);
            transform.Rotate(Vector3.right, Input.GetAxis("Mouse Y") * middle * OrbitScale, Space.Self);

            var inY = Input.GetAxis("Move Y");
            var inX = Input.GetAxis("Move X");

            if (Socket != null && (inX > 0 || inY > 0))
                Game.CameraControl.SocketCamera();

            var forward = transform.forward * inY;
            var right = transform.right * inX;
            forward.y = 0;
            right.y = 0;
            transform.position += forward;
            transform.position += right;
        }
    }
    private void Lerp()
    {
        if (lerpTimer <= 0)
        {
            unIdling = false;
            return;
        }

        if (Idle || unIdling)
        {
            lerpZoom = targetZoomDistance - ((targetZoomDistance - oldZoom) * lerpTimer);

            Camera.localPosition = new Vector3(0, 0, lerpZoom);

            transform.localRotation = Quaternion.Lerp(Idle ? HomeRotation : OldRotation, StartRotation, lerpTimer);
        }

        transform.localPosition = Vector3.Lerp(Socket != null ? Vector3.zero : OldPosition, StartPosition, lerpTimer);

        lerpTimer -= LerpSpeed;
    }


    private void Start()
    {
        if (Camera == null)
            return;

        homeZoom = Camera.localPosition.z;
        Home = new GameObject("HomeCameraAnchor").transform;

        Home.position = transform.localPosition;
        HomeRotation = transform.localRotation;

        //transform.parent = Home;

        GoHome();
    }

    private void FixedUpdate()
    {
        Orbit();
        Zoom();
        Lerp();
    }
}
