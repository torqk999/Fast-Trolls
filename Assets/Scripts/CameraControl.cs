using UnityEngine;

public class CameraControl : MonoBehaviour
{
    public Transform Socket;
    public Transform Camera;
    public float OrbitScale;
    public float ZoomMin, ZoomMax, ZoomScale;

    private void Orbit()
    {
        if (Socket != null)
            Socket.Rotate(Vector3.up, Input.GetAxis("Orbit") * OrbitScale, Space.World);
    }

    private void Zoom()
    {
        if (Camera == null)
            return;

        float newZ = Camera.localPosition.z + (Input.GetAxis("Mouse ScrollWheel") * ZoomScale);
        newZ = newZ < ZoomMin ? ZoomMin : newZ > ZoomMax ? ZoomMax : newZ;
        Camera.localPosition = new Vector3(0, 0, newZ);
    }

    private void FixedUpdate()
    {
        Orbit();
        Zoom();
    }
}
