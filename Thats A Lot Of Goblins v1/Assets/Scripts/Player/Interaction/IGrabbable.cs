using UnityEngine;

public interface IGrabbable
{
    public void OnGrabbed();
    public void OnDragged(Vector3 worldPos);
    public void OnReleased(Vector3 velocity);
}