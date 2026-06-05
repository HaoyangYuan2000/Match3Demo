using UnityEngine;
using System.Collections.Generic;

public class VFXManager : MonoBehaviour
{
    public static VFXManager Instance { get; private set; }

    [Header("Gem Destroy FX  (index 0-5 matches gem color index)")]
    public GameObject[] destroyFX = new GameObject[6];

    [Header("Power-up FX")]
    public GameObject bombFX;
    public GameObject rocketHFX;
    public GameObject rocketVFX;

    [Header("Win FX")]
    public GameObject confettiFX;
    public GameObject fireworkFX;

    [Header("Sound Effects")]
    public AudioClip bombSound;     // drag: Explosion006.wav
    public AudioClip rocketSound;   // drag: Missile010.wav
    public AudioClip[] popSounds;   // drag all 4 bubble sounds here

    private AudioSource audioSource;
    private readonly List<GameObject> winFXObjects = new List<GameObject>();

    void Awake()
    {
        Instance = this;
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    // ── VFX ───────────────────────────────────────────────────
    public void PlayDestroyFX(int colorIndex, Vector3 worldPos)
    {
        if (destroyFX == null || colorIndex < 0 || colorIndex >= destroyFX.Length) return;
        Spawn(destroyFX[colorIndex], worldPos);
        if (popSounds != null && popSounds.Length > 0)
            PlaySound(popSounds[Random.Range(0, popSounds.Length)], 0.45f);
    }

    public void PlayBombFX(Vector3 worldPos)
    {
        Spawn(bombFX, worldPos);
        PlaySound(bombSound, 0.8f);
    }

    public void PlayRocketFX(Vector3 worldPos, bool horizontal)
    {
        Spawn(horizontal ? rocketHFX : rocketVFX, worldPos);
        PlaySound(rocketSound, 0.7f);
    }

    public void PlayWinFX()
    {
        winFXObjects.Add(SpawnTracked(confettiFX, Vector3.zero));
        winFXObjects.Add(SpawnTracked(fireworkFX, new Vector3(0f, 2f, 0f)));
    }

    public void StopWinFX()
    {
        foreach (var obj in winFXObjects)
            if (obj != null) Destroy(obj);
        winFXObjects.Clear();
    }

    // ── Spawn helpers ─────────────────────────────────────────
    void Spawn(GameObject prefab, Vector3 pos)
    {
        if (prefab == null) return;
        GameObject fx = Instantiate(prefab, pos, Quaternion.identity);
        ParticleSystem ps = fx.GetComponentInChildren<ParticleSystem>();
        float lifetime = ps != null
            ? ps.main.duration + ps.main.startLifetime.constantMax
            : 3f;
        Destroy(fx, Mathf.Max(lifetime, 2f));
    }

    GameObject SpawnTracked(GameObject prefab, Vector3 pos)
    {
        if (prefab == null) return null;
        GameObject fx = Instantiate(prefab, pos, Quaternion.identity);
        foreach (var psr in fx.GetComponentsInChildren<ParticleSystemRenderer>())
            psr.sortingOrder = 200;
        return fx;
    }

    // ── Audio ─────────────────────────────────────────────────
    void PlaySound(AudioClip clip, float volume = 1f)
    {
        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip, volume);
    }

}
