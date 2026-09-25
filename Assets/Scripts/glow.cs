using UnityEngine;
using System.Collections;

public class SonarGlow : MonoBehaviour
{
    public Material mat;
    public Color glowColor = Color.cyan;
    public float glowDuration = 1f;

    private Color baseColor;

    void Start()
    {
        baseColor = mat.GetColor("_EmissionColor");
    }

    public void TriggerGlow()
    {
        StartCoroutine(GlowRoutine());
    }

    IEnumerator GlowRoutine()
    {
        float t = 0f;
        while (t < glowDuration)
        {
            float intensity = Mathf.PingPong(t * 2f, 1f); // up then down
            mat.SetColor("_EmissionColor", glowColor * intensity);
            t += Time.deltaTime;
            yield return null;
        }
        mat.SetColor("_EmissionColor", baseColor); // reset
    }
}
