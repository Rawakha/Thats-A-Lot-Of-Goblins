using UnityEngine;

public class SurfaceTag : MonoBehaviour
{
    [SerializeField] private SurfaceType surface;
    public SurfaceType Surface => surface;
}