using UnityEngine;

public class SoundManager : MonoBehaviour
{
   public static SoundManager instance;

   [SerializeField] private AudioSource SFXobject;

   private void Awake()
   {
        if(instance==null)
        {
            instance=this;
        }
   }

   public void PlaySFX(AudioClip audio, Transform spawnTransform, float volume)
   {
        AudioSource source=Instantiate(SFXobject, spawnTransform.position, Quaternion.identity);

        source.clip=audio;

        source.volume=volume;

        source.Play();

        float len=source.clip.length;

        Destroy(source.gameObject, len);
   }
}
