using UnityEngine;

public class scan : MonoBehaviour
{
    public GameObject Terrainscanner;
    public float duration= 10;
    public float size= 500;


    // Update is called once per frame
    void Update()
    {
        if(Input.GetKeyDown(KeyCode.E))
        {
            SpawnScanner();
        }
    }

    void SpawnScanner()
    {
        GameObject terrainScanner= Instantiate(Terrainscanner, gameObject.transform.position, Quaternion.identity) as GameObject;
         
        
        Destroy(terrainScanner, duration+1);
    }
}
