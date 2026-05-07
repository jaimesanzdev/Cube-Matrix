using UnityEngine;

public class CubeOrientation : MonoBehaviour
{
    // The 6 face directions in world space, updated after every roll
    // "Hollow" face starts facing up — change this to match your actual model
    private Vector3 faceTop    =  Vector3.up;
    private Vector3 faceBottom =  Vector3.down;
    private Vector3 faceFront  =  Vector3.forward;
    private Vector3 faceBack   =  Vector3.back;
    private Vector3 faceLeft   =  Vector3.left;
    private Vector3 faceRight  =  Vector3.right;

    // Which face is the hollow/win face? We mark it by tracking faceTop at start.
    // After rolls, hollowFaceDirection updates to wherever that face ends up.
    private Vector3 hollowFaceDirection;

    private bool wasHollowDown = false;

    void Start()
    {
        // Hollow face starts on top — adjust if your model differs
        hollowFaceDirection = faceTop;
    }

    void Update()
    {
        bool isHollowDown = IsHollowFaceDown();
    
        if (isHollowDown != wasHollowDown)
        {
            Debug.Log("Bottom face: " + GetBottomFaceName() + " | Hollow down: " + isHollowDown);
            wasHollowDown = isHollowDown;
        }
    }

   //need it when detecting a win in the win block 
    public bool IsHollowFaceOnBottom()
    {
    return Vector3.Dot(hollowFaceDirection, Vector3.down) > 0.99f;
    }
    
    // Call this from CubeRollMovement after every roll, passing the same rotationAxis and 90f
    public void UpdateOrientation(Vector3 rotationAxis, float angle)
    {
        Quaternion rotation = Quaternion.AngleAxis(angle, rotationAxis);

        faceTop    = rotation * faceTop;
        faceBottom = rotation * faceBottom;
        faceFront  = rotation * faceFront;
        faceBack   = rotation * faceBack;
        faceLeft   = rotation * faceLeft;
        faceRight  = rotation * faceRight;

        hollowFaceDirection = rotation * hollowFaceDirection;
    }

    // Returns true if the hollow face is currently facing down (touching the ground)
    public bool IsHollowFaceDown()
    {
        return Vector3.Dot(hollowFaceDirection, Vector3.down) > 0.9f;
        
    }

    // Returns true if the hollow face is NOT down (safe to press buttons)
    public bool CanPressButton()
    {
        return !IsHollowFaceDown();
    }

    // Get which face is on the bottom (useful for debugging or future mechanics)
    public string GetBottomFaceName()
    {
        Vector3[] faces = { faceTop, faceBottom, faceFront, faceBack, faceLeft, faceRight };
        string[] names  = { "Top",   "Bottom",   "Front",   "Back",   "Left",   "Right"  };

        int best = 0;
        float bestDot = -2f;
        for (int i = 0; i < faces.Length; i++)
        {
            float dot = Vector3.Dot(faces[i], Vector3.down);
            if (dot > bestDot) { bestDot = dot; best = i; }
        }
        return names[best];
    }
}