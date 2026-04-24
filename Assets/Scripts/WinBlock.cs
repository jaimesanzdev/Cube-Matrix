using UnityEngine;

public class WinBlock : MonoBehaviour
{
    public CubeState.FaceType requiredFace;

    private void OnTriggerEnter(Collider other)
    {
        CubeState cube = other.GetComponent<CubeState>();

        if (cube != null)
        {
            if (!cube.isSnapped) return;

            if (cube.currentBottomFace == requiredFace)
            {
                Debug.Log("win!");
                //go to next level: SceneManager.LoadScene(...);
            }
            else
            {
                Debug.Log("does not match to win");

                //give player feedback that the move is wrong 
            }
        }
    }
}