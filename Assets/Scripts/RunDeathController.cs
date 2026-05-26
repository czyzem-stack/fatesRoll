using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Fade to black on Steve's death, reset enemies to base difficulty, respawn Steve with gear and stats.</summary>
/// <remarks>Inherits <see cref="GameServiceBehaviour{T}"/> — auto-registers in Awake via <see cref="GameServices"/>.</remarks>
[DefaultExecutionOrder(50)]
public class RunDeathController : GameServiceBehaviour<RunDeathController>
{

    [SerializeField] private float fadeOutSeconds = 0.55f;
    [SerializeField] private float holdBlackSeconds = 0.35f;
    [SerializeField] private float fadeInSeconds = 0.55f;
    [SerializeField] private float standUpSeconds = 1.85f;

    private GameObject fadeOverlay;
    private Image fadeImage;
    private bool deathInProgress;
    private Vector3 recordedSpawnPosition;
    private Quaternion recordedSpawnRotation;
    private bool hasRecordedSpawn;

    public bool IsDeathInProgress => deathInProgress;

    private void Start()
    {
        CacheSpawnFromScene();
        var hero = GameServices.Hero;
        if (hero != null)
            RecordHeroSpawn(hero);
    }

    public void RecordHeroSpawn(HeroController hero)
    {
        if (hero == null) return;

        Vector3 pos = hero.transform.position;
        if (HeroSpawnUtility.TryResolveSpawnPosition(pos, out Vector3 resolved))
            pos = resolved;

        recordedSpawnPosition = pos;
        recordedSpawnRotation = hero.transform.rotation;
        hasRecordedSpawn = true;
    }

    public void HandleHeroDeath(HeroController hero)
    {
        if (hero == null || deathInProgress) return;
        StartCoroutine(DeathSequence(hero));
    }

    private void CacheSpawnFromScene()
    {
        var marker = GameServices.HeroSpawn;
        if (marker == null) return;

        marker.SnapToPlaySpawnSurface();
        recordedSpawnPosition = marker.transform.position;
        recordedSpawnRotation = marker.transform.rotation;
        hasRecordedSpawn = true;
    }

    private IEnumerator DeathSequence(HeroController hero)
    {
        deathInProgress = true;
        GlobalSettings.LogGameplay("RunDeathController: Steve defeated — resetting run.");

        yield return FadeTo(1f, fadeOutSeconds);
        if (holdBlackSeconds > 0f)
            yield return new WaitForSecondsRealtime(holdBlackSeconds);

        ResetWorldDifficulty();
        RespawnHero(hero);

        yield return FadeTo(0f, fadeInSeconds);
        HideFadeOverlay();

        if (hero != null)
            yield return hero.PlayStandUpFromDeathRoutine(standUpSeconds);

        deathInProgress = false;
    }

    private void ResetWorldDifficulty()
    {
        if (EnemyStatManager.Instance != null)
            EnemyStatManager.Instance.ResetRunBonuses();

        if (SpawnManager.Instance != null)
            SpawnManager.Instance.ResetRunEncounters();

        if (POIManager.Instance != null)
            POIManager.Instance.RefreshRemainingVisitEnemies();
    }

    private void RespawnHero(HeroController hero)
    {
        if (hero == null) return;

        hero.PrepareForRespawnAtSpawn(GetSpawnPosition(), GetSpawnRotation());
    }

    private Vector3 GetSpawnPosition()
    {
        Vector3 desired;
        if (hasRecordedSpawn)
            desired = recordedSpawnPosition;
        else
        {
            var marker = GameServices.HeroSpawn;
            desired = marker != null ? marker.transform.position : Vector3.zero;
        }

        if (HeroSpawnUtility.TryResolveSpawnPosition(desired, out Vector3 resolved))
            return resolved;
        return desired;
    }

    private Quaternion GetSpawnRotation()
    {
        if (hasRecordedSpawn)
            return recordedSpawnRotation;

        var marker = GameServices.HeroSpawn;
        if (marker != null)
            return marker.transform.rotation;

        return Quaternion.identity;
    }

    private IEnumerator FadeTo(float targetAlpha, float duration)
    {
        EnsureFadeOverlay();
        fadeOverlay.SetActive(true);

        float start = fadeImage.color.a;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
            SetFadeAlpha(Mathf.Lerp(start, targetAlpha, t));
            yield return null;
        }

        SetFadeAlpha(targetAlpha);
    }

    private void EnsureFadeOverlay()
    {
        if (fadeOverlay != null) return;

        Canvas canvas = FindMainUiCanvas();
        if (canvas == null)
        {
            Debug.LogWarning("RunDeathController: no Canvas for death fade.");
            return;
        }

        fadeOverlay = new GameObject("DeathFadeOverlay", typeof(RectTransform));
        fadeOverlay.transform.SetParent(canvas.transform, false);
        fadeOverlay.transform.SetAsLastSibling();

        var rt = fadeOverlay.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        fadeImage = fadeOverlay.AddComponent<Image>();
        fadeImage.color = new Color(0f, 0f, 0f, 0f);
        fadeImage.raycastTarget = true;
        fadeOverlay.SetActive(false);
    }

    private void SetFadeAlpha(float alpha)
    {
        if (fadeImage == null) return;
        fadeImage.color = new Color(0f, 0f, 0f, Mathf.Clamp01(alpha));
    }

    private void HideFadeOverlay()
    {
        if (fadeOverlay != null)
            fadeOverlay.SetActive(false);
    }

    private static Canvas FindMainUiCanvas()
    {
        Canvas[] canvases = Object.FindObjectsByType<Canvas>();
        foreach (var canvas in canvases)
        {
            if (canvas != null && canvas.gameObject.name.Contains("MainUI", System.StringComparison.OrdinalIgnoreCase))
                return canvas;
        }

        return canvases.Length > 0 ? canvases[0] : null;
    }
}
