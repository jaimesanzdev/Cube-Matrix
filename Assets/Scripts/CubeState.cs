using UnityEngine;

public class CubeState : MonoBehaviour
{
    public enum FaceType
    {
        //different faces of the cube (should later be renamed depdning on shaders/style of sides for clairty as it rolls)
        Top,
        Bottom,
        Left,
        Right,
        Front,
        Back
    }

    //keep track of the current bottom face because it is what we will need to know to detect if a win is made
    public FaceType currentBottomFace;
    public bool isSnapped = true;
    private Vector3 lastSafePosition;
    void Update()
    {
        CheckSnap();
        UpdateBottomFace();
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
        //if all are within grid, it means cubeis snapped in place 
        isSnapped = xSnapped && ySnapped && zSnapped;
    }

    void UpdateBottomFace()
    {
        Vector3 down = Vector3.down;
        float maxDot = -Mathf.Infinity;
        FaceType bestFace = FaceType.Bottom;

        CheckFace(Vector3.up, FaceType.Top, ref maxDot, ref bestFace);
        CheckFace(Vector3.down, FaceType.Bottom, ref maxDot, ref bestFace);
        CheckFace(Vector3.left, FaceType.Left, ref maxDot, ref bestFace);
        CheckFace(Vector3.right, FaceType.Right, ref maxDot, ref bestFace);
        CheckFace(Vector3.forward, FaceType.Front, ref maxDot, ref bestFace);
        CheckFace(Vector3.back, FaceType.Back, ref maxDot, ref bestFace);

        currentBottomFace = bestFace;
    }

    void CheckFace(Vector3 localDir, FaceType face, ref float maxDot, ref FaceType bestFace)
    {
        //detects which direction a cube side is facing by converting local direction into world direction
        Vector3 worldDir = transform.TransformDirection(localDir);
        float dot = Vector3.Dot(worldDir, Vector3.down);

        //if dot product == 1 (max), then it is facing down. == 0, sideways, == -1 pointing up
        if (dot > maxDot)
        {
            maxDot = dot;
            bestFace = face;
        }
    }
}