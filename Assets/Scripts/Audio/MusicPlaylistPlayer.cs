using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

[RequireComponent(typeof(ObjectAudioManager))]
public class MusicPlaylistPlayer : MonoBehaviour
{
    [System.Serializable]
    public class ScenePlaylist
    {
        public string sceneName;
        public List<string> trackNames = new List<string>();
    }

    [Tooltip("Which SoundClip entries (by name, from the ObjectAudioManager on this object) play, in order, when each scene becomes active. Tracks play one after another and loop back to the first at the end. This object is meant to be persistent (DontDestroyOnLoad), so playback survives scene loads and just switches playlist instead of being recreated.")]
    public List<ScenePlaylist> playlists = new List<ScenePlaylist>();

    ObjectAudioManager _audioManager;
    List<string> _currentPlaylist = new List<string>();
    int _currentTrackIndex = -1;
    string _currentSceneName;

    void Awake()
    {
        _audioManager = GetComponent<ObjectAudioManager>();
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
    }

    void OnDestroy()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }

    void Start()
    {
        SwitchToScene(SceneManager.GetActiveScene().name);
    }

    void Update()
    {
        // AudioListener.pause makes AudioSource.isPlaying report false without actually
        // stopping the clip, so the finished-track check below would mistake a paused
        // track for a finished one and start the next track on top of it.
        if (AudioListener.pause)
            return;

        if (_currentTrackIndex < 0 || _currentPlaylist.Count == 0)
            return;

        if (!_audioManager.IsSoundPlaying(_currentPlaylist[_currentTrackIndex]))
            PlayTrack((_currentTrackIndex + 1) % _currentPlaylist.Count);
    }

    void OnActiveSceneChanged(Scene previous, Scene next)
    {
        SwitchToScene(next.name);
    }

    void SwitchToScene(string sceneName)
    {
        if (sceneName == _currentSceneName)
            return;

        if (_currentTrackIndex >= 0 && _currentTrackIndex < _currentPlaylist.Count)
            _audioManager.StopSound(_currentPlaylist[_currentTrackIndex]);

        _currentSceneName = sceneName;
        _currentTrackIndex = -1;
        _currentPlaylist.Clear();

        ScenePlaylist scenePlaylist = playlists.Find(p => p.sceneName == sceneName);
        if (scenePlaylist != null)
            _currentPlaylist.AddRange(scenePlaylist.trackNames);

        if (_currentPlaylist.Count > 0)
            PlayTrack(0);
    }

    void PlayTrack(int index)
    {
        if (_currentTrackIndex >= 0 && _currentTrackIndex < _currentPlaylist.Count && _currentTrackIndex != index)
            _audioManager.StopSound(_currentPlaylist[_currentTrackIndex]);

        _currentTrackIndex = index;
        _audioManager.PlaySound(_currentPlaylist[index]);
    }
}
