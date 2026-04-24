using UnityEngine;

public class CubeState : MonoBehaviour
{
    public bool isSnapped = true;
    private Vector3 lastSafePosition;
    //uses cube orientation to check positions
    public CubeOrientation orientation;

    void Update()
    {
        CheckSnap();
    }

    //makes sure cube is snapped in place before checking if win condition is met to prevent passing rolls from being detected 
    void CheckSnap()
    {
        Vector3 rot = transform.eulerAngles;

        float snapTolerance = 1f;
        //checks if it is within approx 1 degree of nearest valid grid angle indicating full cube rotation along the x y and z axis
        bool xSnapped = Mathf.Abs(Mathf.DeltaAngle(rot.x, Mathf.Round(rot.x / 90f) * 90f)) < snapTolerance;
        bool ySnapped = Mathf.Abs(Mathf.DeltaAngle(rot.y, Mathf.Round(rot.y / 90f) * 90f)) < snapTolerance;
        bool zSnapped = Mathf.Abs(Mathf.DeltaAngle(rot.z, Mathf.Round(rot.z / 90f) * 90f)) < snapTolerance;

        isSnapped = xSnapped && ySnapped && zSnapped;
    }
}