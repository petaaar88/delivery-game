using UnityEngine;
using System.Collections.Generic;

public class AudioPauseManager : MonoBehaviour
{
    [SerializeField] private GameObject ignoreObject;

    private List<AudioSource> pausedSources = new List<AudioSource>();
    private bool isPaused = false;

    public void PauseAllExcept()
    {
        if (isPaused) return;

        pausedSources.Clear();

        AudioSource[] allSources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);

        foreach (AudioSource source in allSources)
        {
            if (source.transform.IsChildOf(ignoreObject.transform))
                continue;

            if (source.isPlaying)
            {
                source.Pause();
                pausedSources.Add(source);
            }
        }

        isPaused = true;
    }

    public void ResumeAll()
    {
        if (!isPaused) return;

        foreach (AudioSource source in pausedSources)
        {
            if (source != null)
            {
                source.UnPause();
            }
        }

        pausedSources.Clear();
        isPaused = false;
    }
}
