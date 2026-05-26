using UnityEngine;
using TMPro;

public class QADashboard : MonoBehaviour
{
    public TextMeshProUGUI rollText;
    public TextMeshProUGUI distanceText;
    public TextMeshProUGUI stepsRemainingText;

    private HeroController hero;
    private DiceSpawner spawner;

    void Start()
    {
        hero = GameServices.Hero;
        spawner = DiceSpawner.Instance;
    }

    void Update()
    {
        if (spawner != null)
        {
            rollText.text = $"Last Roll: {spawner.LastRoll} ({spawner.LastIndividualRolls})";
        }

        var settings = GlobalSettings.Instance;
        if (hero != null && settings != null)
        {
            UnityEngine.AI.NavMeshAgent agent = hero.GetComponent<UnityEngine.AI.NavMeshAgent>();
            float moveRemaining = (agent != null && agent.hasPath) ? agent.remainingDistance : 0;
            
            string scaleInfo = $"Scale: {settings.stepsPerDiceValue} step/die, {settings.metersPerStep}m/step";
            
            GameObject poi = null;
            if (POIManager.Instance != null)
            {
                poi = POIManager.Instance.GetNearestPOI(hero.transform.position);
            }

            if (poi != null)
            {
                float dist = Vector3.Distance(hero.transform.position, poi.transform.position);
                distanceText.text = $"To POI: {dist:F1}m (Move: {moveRemaining:F1}m)\n{scaleInfo}";
            }
            else
            {
                distanceText.text = $"Move: {moveRemaining:F1}m\n{scaleInfo}";
            }
            
            stepsRemainingText.text = "";
        }
    }
}
