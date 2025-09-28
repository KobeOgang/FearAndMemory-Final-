using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimpleAudioPlayer : MonoBehaviour
{

    public void PlayAudio(AudioClip SFX)
    {
        if (AudioManager.instance != null)
        {
            AudioManager.instance.PlayClip(SFX);
        }
        else
        {
            Debug.LogWarning("SimpleAudioPlayer: AudioManager instance not found!");
        }
    }
}
