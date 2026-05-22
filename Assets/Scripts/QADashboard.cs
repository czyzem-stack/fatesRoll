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
        hero = Object.FindAnyObjectByType<HeroController>();
        spawner = Object.FindAnyObjectByType<DiceSpawner>();
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
            
            GameObject poi = GameObject.FindWithTag("POI");
            string scaleInfo = $"Scale: {settings.stepsPerDiceValue} step/die, {settings.metersPerStep}m/step";
            
            if (poi != null)
            {
                float dist = Vector3.Distance(hero.transform.position, poi.transform.position);
                distanceText.text = $"To POI: {dist:F1}m (Move: {moveRemaining:F1}m)\n{scaleInfo}";

                float avgRoll = 7.0f;
                float metersPerRoll = avgRoll * settings.stepsPerDiceValue * settings.metersPerStep;
                float rollsNeeded = dist / metersPerRoll;
                stepsRemainingText.text = $"Est. Rolls: {rollsNeeded:F1}";
            }
            else
            {
                distanceText.text = $"Move: {moveRemaining:F1}m\n{scaleInfo}";
                stepsRemainingText.text = "No Target";
            }
        }
    }
}
