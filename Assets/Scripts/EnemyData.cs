using UnityEngine;

[CreateAssetMenu(menuName = "POI/Enemy Data")]
public class EnemyData : ScriptableObject
{
    public string enemyName = "Orc";
    
    [Header("Core Stats")]
    public float strength = 10f;
    public float agility = 10f;
    public float vitality = 10f;
    public float luck = 10f;

    [Header("Visuals")]
    public GameObject prefab;
}