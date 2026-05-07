using UnityEngine;
using UnityEngine.SceneManagement;

public class WinBlock : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        CubeState state = other.GetComponent<CubeState>();
        CubeOrientation orientation = other.GetComponent<CubeOrientation>();

        if (state == null || orientation == null) return;
        
        if (!state.isSnapped)
        {
            Debug.Log("Cube not snapped yet");
            return;
        }

        if (orientation.IsHollowFaceOnBottom())
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