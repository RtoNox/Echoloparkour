using UnityEngine;

public class NewMonoBehaviourScript : MonoBehaviour
{
    public Material sonarMat;
    public Transform EchoOrigin;
    public float speed=5f;
    private float radius;

    // Update is called once per frame
   void Update()
    {
        radius += speed * Time.deltaTime;
        if (radius > 1f) radius = 0f;

        var mat = GetComponent<Renderer>().material;
        mat.SetFloat("Radius", radius);

        if (EchoOrigin != null)
            mat.SetVector("EchoCenter", EchoOrigin.position);
    }

}
