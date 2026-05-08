using UnityEngine;

public class PortalDisappear : MonoBehaviour
{
    public Transform visualsRoot;

    public float sinkDistance = 2f;
    public float sinkSpeed = 5f;

    private Vector3 originalPos;
    private bool insidePortal = false;

    private void Start()
    {
        originalPos = visualsRoot.localPosition;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            insidePortal = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            insidePortal = false;
            visualsRoot.localPosition = originalPos;
        }
    }

    private void Update()
    {
        if (insidePortal)
        {
            visualsRoot.localPosition = Vector3.Lerp(
                visualsRoot.localPosition,
                originalPos + Vector3.down * sinkDistance,
                Time.deltaTime * sinkSpeed
            );
        }
    }
}
