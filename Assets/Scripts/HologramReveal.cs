using System.Collections;
using UnityEngine;

public class HologramReveal : MonoBehaviour
{
    Renderer rend;
    Material mat;

    public float revealDuration = 3f;

    void Start()
    {
        rend = GetComponent<Renderer>();
        mat = rend.material;
        Debug.Log(mat.HasProperty("_RevealAmount"));

        mat.SetFloat("_RevealAmount", 0);
    }

    public void Reveal()
{
    Debug.Log(gameObject.name + " was revealed!");

    StopAllCoroutines();
    StartCoroutine(RevealRoutine());
}

    IEnumerator RevealRoutine()
    {
        mat.SetFloat("_RevealAmount", 1);

        yield return new WaitForSeconds(revealDuration);

        mat.SetFloat("_RevealAmount", 0);
    }
}