using UnityEngine;
public class SonarWave : MonoBehaviour
{
    public float speed = 50f;
    public float maxRadius = 500f;
    float radius;
    void Update()
        {
            radius += speed * Time.deltaTime;
            transform.localScale = Vector3.one * radius;
            SphereCollider col = GetComponent<SphereCollider>();
            col.radius = 0.5f;
            if(radius > maxRadius)
            Destroy(gameObject);
        }
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("Hit: " + other.name);

        HologramReveal holo =
        other.GetComponent<HologramReveal>();

        if (holo != null)
        {
            holo.Reveal();
        }
    }
}
