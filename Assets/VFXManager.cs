using UnityEngine;
using System.Collections.Generic;

public class VFXManager : MonoBehaviour
{
    public static VFXManager Instance { get; private set; }

    [Header("Gem Destroy FX  (index 0-5 matches gem color index)")]
    public GameObject[] destroyFX = new GameObject[6];
    // Suggested assignments:
    // [0] Red    → Chip_Destroy_Red_FX
    // [1] Blue   → Chip_Destroy_Blue_Root_FX
    // [2] Green  → Chip_Destroy_Green_FX
    // [3] Yellow → Chip_Destroy_Orange_FX
    // [4] Purple → Chip_Destroy_Violet_FX
    // [5] Orange → Chip_Destroy_Pink_FX

    [Header("Power-up FX")]
    public GameObject bombFX;       // Bomb_Explosion_FX
    public GameObject rocketHFX;    // RocketHorizontal
    public GameObject rocketVFX;    // RocketVertical

    [Header("Win FX")]
    public GameObject confettiFX;   // Confetti_Loop
    public GameObject fireworkFX;   // Firework

    private readonly List<GameObject> winFXObjects = new List<GameObject>();

    void Awake()
    {
        Instance = this;
    }

    public void PlayDestroyFX(int colorIndex, Vector3 worldPos)
    {
        if (destroyFX == null || colorIndex < 0 || colorIndex >= destroyFX.Length) return;
        Spawn(destroyFX[colorIndex], worldPos);
    }

    public void PlayBombFX(Vector3 worldPos)    => Spawn(bombFX, worldPos);
    public void PlayRocketFX(Vector3 worldPos, bool horizontal)
        => Spawn(horizontal ? rocketHFX : rocketVFX, worldPos);

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
        return Instantiate(prefab, pos, Quaternion.identity);
    }
}
