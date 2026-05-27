using UnityEngine;

/// <summary>
/// Compile-time constants and well-known magic values used across the project.
/// Tunable values that designers may want to change live should live in <see cref="GlobalSettings"/> instead.
/// </summary>
public static class GameConstants
{
    // === Async / Scene Loading (Unity limitation) ===
    /// <summary>Unity caps AsyncOperation.progress at 0.9 until allowSceneActivation is set to true.</summary>
    public const float AsyncLoadProgressCap = 0.9f;

    // === Small comparison tolerances (avoid magic 0.01f / 0.0001f everywhere) ===
    public const float MovementEpsilon = 0.01f;
    public const float VelocityEpsilon = 0.0001f;
    public const float DistanceTolerance = 0.25f; // common path remainingDistance buffer used in SteveMovement

    // === Default NavMeshAgent values (used when GlobalSettings not yet available) ===
    public const float DefaultHeroAcceleration = 30f;
    public const float DefaultHeroStoppingDistance = 1f;
    public const float EnemyPathStoppingDistance = 1f;
    public const float ChestPathStoppingDistance = 0.35f;
    public const float EnemyMeleeStoppingDistance = 0.12f;

    // === Face / LookAt defaults ===
    public const float DefaultFaceSpeed = 16f;

    // === UI / Misc ===
    public const float DefaultLineWidth = 0.2f;
    public const float ThinLineWidth = 0.1f;
    public const float ThinLineWidthAlt = 0.12f;
}