using UnityEngine;

public class SoundManager : MonoBehaviour
{
   public static SoundManager instance;

   [SerializeField] private AudioSource SFXobject;
   
   private int currentIndex;

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

      public void PlayRandomSFX(AudioClip[] audio, Transform spawnTransform, float volume)
   {
        int rand= Random.Range(0, audio.Length); 

        AudioSource source=Instantiate(SFXobject, spawnTransform.position, Quaternion.identity);

        source.clip=audio[rand];

        source.volume=volume;

        source.Play();

        float len=source.clip.length;

        Destroy(source.gameObject, len);
   }


     public void PlaySFXInOrder(AudioClip[] audio, Transform spawnTransform, float volume)
     {
          if (audio?.Length > 0)
          {
               AudioSource source = Instantiate(SFXobject, spawnTransform.position, Quaternion.identity);
               source.clip = audio[currentIndex];
               source.volume = volume;
               source.Play();

               Destroy(source.gameObject, source.clip.length);

               currentIndex = Mathf.Min(currentIndex + 1, audio.Length - 1);
          }
     }

     public AudioSource PlayLoopingSFX(AudioClip clip, Transform target, float volume)
     {
          AudioSource source = Instantiate(
          SFXobject,
          target.position,
          Quaternion.identity
          );

          source.transform.SetParent(target);
          source.clip = clip;
          source.volume = volume;
          source.loop = true;
          source.Play();

          return source;
     }
}
