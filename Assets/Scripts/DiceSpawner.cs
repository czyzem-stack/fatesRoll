using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

public class DiceSpawner : MonoBehaviour
{
    public GameObject d6Prefab;
    public Transform spawnPoint;
    public float throwForce = 8f;
    public float torque = 15f;

    private List<GameObject> activeDice = new List<GameObject>();
    private bool isRolling = false;
    public int LastRoll { get; private set; }
    public string LastIndividualRolls { get; private set; }

    public void OnRoll(InputAction.CallbackContext ctx)
    {
        if (ctx.performed && CanRoll())
        {
            RollDice();
        }
    }

    private bool CanRoll()
    {
        if (isRolling) return false;
        
        var hero = Object.FindAnyObjectByType<HeroController>();
        if (hero != null && hero.IsMoving) return false;
        
        return true;
    }

    [ContextMenu("Roll Dice")]
    public void RollDice()
    {
        if (!CanRoll()) return;
        StartCoroutine(RollRoutine());
    }

    private IEnumerator RollRoutine()
    {
        if (isRolling) yield break;
        isRolling = true;
        
        try 
        {
            Debug.Log("DiceSpawner: Starting Roll Routine.");

            // 1. Aggressive Cleanup of old dice
            var existingDice = Object.FindObjectsByType<DieResult>(FindObjectsInactive.Exclude);
            foreach (var d in existingDice)
            {
                if (d != null && d.gameObject != null) Destroy(d.gameObject);
            }
            activeDice.Clear();

            if (d6Prefab == null || spawnPoint == null) 
            {
                Debug.LogError("DiceSpawner: Prefab or SpawnPoint is NULL!");
                yield break;
            }

            // 2. Spawn new dice
            for (int i = 0; i < 2; i++)
            {
                Vector3 offset = new Vector3(i * 0.6f - 0.3f, 0.5f, Random.Range(-0.2f, 0.2f));
                GameObject die = Instantiate(d6Prefab, spawnPoint.position + offset, Random.rotation);
                if (die.GetComponent<DieResult>() == null) die.AddComponent<DieResult>();
                activeDice.Add(die);
                
                Rigidbody rb = die.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.AddForce(transform.forward * throwForce + Vector3.up * 5f, ForceMode.Impulse);
                    rb.AddTorque(Random.onUnitSphere * torque, ForceMode.Impulse);
                }
            }

            // 3. Wait for dice to settle
            yield return new WaitForSeconds(2.0f); 
            
            float settleTimeout = 5f;
            while (settleTimeout > 0)
            {
                bool allSettled = true;
                foreach (var d in activeDice)
                {
                    if (d != null)
                    {
                        var res = d.GetComponent<DieResult>();
                        if (res != null && !res.IsSettled())
                        {
                            allSettled = false;
                            break;
                        }
                    }
                }
                if (allSettled) break;
                settleTimeout -= Time.deltaTime;
                yield return null;
            }

            // 4. Calculate result
            int total = 0;
            List<int> individual = new List<int>();
            foreach (var d in activeDice)
            {
                if (d != null)
                {
                    int val = d.GetComponent<DieResult>().GetValue();
                    total += val;
                    individual.Add(val);
                }
            }
            LastRoll = total;
            LastIndividualRolls = string.Join(", ", individual);
            Debug.Log($"DiceSpawner: Total result is {total} ({LastIndividualRolls})");

            // 5. Trigger Hero Movement
            var hero = Object.FindAnyObjectByType<HeroController>();
            if (hero != null)
            {
                hero.MoveSteps(total);
            }
            else
            {
                Debug.LogError("DiceSpawner: HeroController NOT found!");
            }
        }
        finally
        {
            isRolling = false;
        }
    }
}
