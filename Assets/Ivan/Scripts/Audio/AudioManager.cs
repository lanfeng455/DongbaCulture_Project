using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

// Global audio hub with three independent channels:
// - BGM: looping music, two A/B sources for seamless crossfade
// - BGS: looping ambience (rain, wind...), two A/B sources for crossfade
// - SE : one-shot effects via PlayOneShot
//
// Clips are loaded through Addressables: the configured name IS the asset address
// (assigned in the Addressables Groups window). A missing address is a warning,
// never an exception — current playback keeps going.
// Setup: place one empty GameObject with this component in the scene; the five
// AudioSources are created and owned by this component.
public class AudioManager : MonoBehaviour
{
    private static AudioManager s_instance;

    // Sentinel names usable in any audio-name config field: instead of playing a
    // clip, they fade the matching channel out
    public const string k_FadeOutBgmSignal = "滨崎茉幌";
    public const string k_FadeOutBgsSignal = "汐见萤";

    public static AudioManager Instance => s_instance;

    [Header("Channel Volumes")]
    [SerializeField, Range(0f, 1f)] private float m_bgmVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float m_bgsVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float m_seVolume = 1f;

    [Header("Playback Settings")]
    [Tooltip("默认淡入淡出时长（秒）。调用时可传入 fadeDuration 覆盖，0 = 立即切换")]
    [SerializeField] private float m_defaultFadeDuration = 1.5f;

    // One looping channel = an A/B source pair for gapless crossfade
    private class AudioChannel
    {
        public AudioSource A;
        public AudioSource B;

        // Latest requested clip name; also makes stale async loads self-cancel
        public string CurrentName;
    }

    private readonly Dictionary<string, AudioClip> m_clipCache = new Dictionary<string, AudioClip>();
    private readonly Dictionary<string, AsyncOperationHandle<AudioClip>> m_handles = new Dictionary<string, AsyncOperationHandle<AudioClip>>();

    private AudioChannel m_bgm;
    private AudioChannel m_bgs;
    private AudioSource m_seSource;

    public string CurrentBgmName => m_bgm?.CurrentName;
    public string CurrentBgsName => m_bgs?.CurrentName;

    private void Awake()
    {
        if (s_instance != null && s_instance != this)
        {
            Debug.LogWarning($"[{GetType().Name}] Duplicate instance destroyed.", this);
            Destroy(gameObject);
            return;
        }

        s_instance = this;

        m_bgm = new AudioChannel { A = CreateSource(loop: true), B = CreateSource(loop: true) };
        m_bgs = new AudioChannel { A = CreateSource(loop: true), B = CreateSource(loop: true) };
        m_seSource = CreateSource(loop: false);
    }

    private void OnDestroy()
    {
        if (s_instance == this)
        {
            s_instance = null;
        }

        // Release every Addressables handle we own (cached clips stay valid until here)
        foreach (KeyValuePair<string, AsyncOperationHandle<AudioClip>> pair in m_handles)
        {
            Addressables.Release(pair.Value);
        }

        m_handles.Clear();
        m_clipCache.Clear();
    }

    #region Public API
    // fadeDuration < 0 uses m_defaultFadeDuration; 0 switches instantly

    public void PlayBgm(string clipName, float fadeDuration = -1f)
    {
        if (string.IsNullOrEmpty(clipName) || m_bgm.CurrentName == clipName)
        {
            return;
        }

        // Claim the name now so rapid requests dedupe, and async loads that were
        // superseded by a newer request cancel themselves after the await
        m_bgm.CurrentName = clipName;
        _ = PlayChannelAsync(m_bgm, clipName, m_bgmVolume, fadeDuration);
    }

    public void StopBgm(float fadeDuration = -1f)
    {
        m_bgm.CurrentName = null;
        StopSource(m_bgm.A, fadeDuration);
        StopSource(m_bgm.B, fadeDuration);
    }

    public void PlayBgs(string clipName, float fadeDuration = -1f)
    {
        if (string.IsNullOrEmpty(clipName) || m_bgs.CurrentName == clipName)
        {
            return;
        }

        m_bgs.CurrentName = clipName;
        _ = PlayChannelAsync(m_bgs, clipName, m_bgsVolume, fadeDuration);
    }

    public void StopBgs(float fadeDuration = -1f)
    {
        m_bgs.CurrentName = null;
        StopSource(m_bgs.A, fadeDuration);
        StopSource(m_bgs.B, fadeDuration);
    }

    public void PlaySe(string clipName, float volumeScale = 1f)
    {
        if (string.IsNullOrEmpty(clipName))
        {
            return;
        }

        _ = PlaySeAsync(clipName, volumeScale);
    }

    public float BgmVolume
    {
        get => m_bgmVolume;
        set
        {
            m_bgmVolume = Mathf.Clamp01(value);
            m_bgm.A.volume = m_bgmVolume;
            m_bgm.B.volume = m_bgmVolume;
        }
    }

    public float BgsVolume
    {
        get => m_bgsVolume;
        set
        {
            m_bgsVolume = Mathf.Clamp01(value);
            m_bgs.A.volume = m_bgsVolume;
            m_bgs.B.volume = m_bgsVolume;
        }
    }

    public float SeVolume
    {
        get => m_seVolume;
        set => m_seVolume = Mathf.Clamp01(value);
    }
    #endregion

    private AudioSource CreateSource(bool loop)
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        return source;
    }

    // Loads the clip, then crossfades through the idle source of the A/B pair.
    // Between await and playback the request may have been superseded (a newer
    // Play/Stop changed CurrentName) — in that case this load is simply dropped.
    private async Awaitable PlayChannelAsync(AudioChannel channel, string clipName,
        float channelVolume, float fadeDuration)
    {
        AudioClip clip = await LoadClipAsync(clipName);

        if (this == null)
        {
            return;
        }

        if (clip == null)
        {
            // Load failed: release the claim so the same name can be retried later
            if (channel.CurrentName == clipName)
            {
                channel.CurrentName = null;
            }

            return;
        }

        if (channel.CurrentName != clipName)
        {
            return; // superseded by a newer request while loading
        }

        AudioSource outgoing = channel.A.isPlaying ? channel.A : channel.B;
        AudioSource incoming = channel.A.isPlaying ? channel.B : channel.A;

        float fade = ResolveFade(fadeDuration);

        outgoing.DOKill();
        incoming.DOKill();

        incoming.clip = clip;
        incoming.volume = 0f;
        incoming.Play();

        if (fade <= 0f)
        {
            outgoing.Stop();
            outgoing.clip = null;
            incoming.volume = channelVolume;
            return;
        }

        outgoing.DOFade(0f, fade).OnComplete(() =>
        {
            outgoing.Stop();
            outgoing.clip = null;
        });
        incoming.DOFade(channelVolume, fade);
    }

    private async Awaitable PlaySeAsync(string clipName, float volumeScale)
    {
        AudioClip clip = await LoadClipAsync(clipName);

        if (this == null || clip == null)
        {
            return;
        }

        m_seSource.PlayOneShot(clip, m_seVolume * volumeScale);
    }

    private void StopSource(AudioSource source, float fadeDuration)
    {
        if (!source.isPlaying)
        {
            return;
        }

        float fade = ResolveFade(fadeDuration);
        source.DOKill();

        if (fade <= 0f)
        {
            source.Stop();
            source.clip = null;
            return;
        }

        source.DOFade(0f, fade).OnComplete(() =>
        {
            source.Stop();
            source.clip = null;
        });
    }

    private float ResolveFade(float fadeDuration)
    {
        return fadeDuration < 0f ? m_defaultFadeDuration : Mathf.Max(0f, fadeDuration);
    }

    // Loads by address with caching. A failed/missing address is a warning, never
    // an exception: callers keep their current playback so the scene never goes
    // silent by accident. Successful handles are released together in OnDestroy.
    private async Awaitable<AudioClip> LoadClipAsync(string clipName)
    {
        if (m_clipCache.TryGetValue(clipName, out AudioClip cached))
        {
            return cached;
        }

        AsyncOperationHandle<AudioClip> handle = Addressables.LoadAssetAsync<AudioClip>(clipName);

        try
        {
            await handle.Task;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[{GetType().Name}] Failed to load AudioClip '{clipName}': {exception.Message}. Keep current playback.", this);
            Addressables.Release(handle);
            return null;
        }

        if (this == null)
        {
            Addressables.Release(handle);
            return null;
        }

        if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
        {
            Debug.LogWarning($"[{GetType().Name}] AudioClip address not found: {clipName}. Keep current playback.", this);
            Addressables.Release(handle);
            return null;
        }

        if (m_clipCache.TryGetValue(clipName, out AudioClip raced))
        {
            // A concurrent load of the same address already cached the clip
            Addressables.Release(handle);
            return raced;
        }

        m_clipCache[clipName] = handle.Result;
        m_handles[clipName] = handle;
        return handle.Result;
    }
}
