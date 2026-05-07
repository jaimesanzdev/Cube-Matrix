using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NextLevel : MonoBehaviour
{
    [SerializeField] private float delay = 3f;
    private bool isLoading = false;
    private bool isOnGoalTile = false;
    private CubeOrientation cubeOrientation;
    private CubeRollMovement cubeMovement;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        isOnGoalTile = true;
        cubeOrientation = other.GetComponent<CubeOrientation>();
        cubeMovement = other.GetComponent<CubeRollMovement>();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        isOnGoalTile = false;
        cubeOrientation = null;
        cubeMovement = null;
    }

    private void Update()
    {
        if (isLoading || !isOnGoalTile || cubeOrientation == null || cubeMovement == null) return;
        if (cubeMovement.isMoving) return;

        // extra safety check - make sure hollow is actually down
        Debug.Log("On goal tile - Hollow down: " + cubeOrientation.IsHollowFaceDown() + " | Bottom face: " + cubeOrientation.GetBottomFaceName());

        if (cubeOrientation.IsHollowFaceDown())
        {
            isLoading = true;
            StartCoroutine(LoadNextLevel());
        }
    }

    private IEnumerator LoadNextLevel()
    {
        yield return new WaitForSeconds(delay);
        int nextScene = SceneManager.GetActiveScene().buildIndex + 1;
        SceneManager.LoadScene(nextScene);
    }
}