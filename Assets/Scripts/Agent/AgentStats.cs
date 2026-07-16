using UnityEngine;

public class AgentStats : MonoBehaviour
{
    public float thirst = 0f;
    public float hunger = 0f;
    public float fatigue = 0f;

    public int woodCarrying = 0;
    public int maxWoodCapacity = 3;

    public int stoneCarrying = 0;
    public int maxStoneCapacity = 3;

    public bool isDead = false;

    private void Update()
    {
        if (isDead) return;

        thirst += 2.0f * Time.deltaTime;
        hunger += 1.5f * Time.deltaTime;
        fatigue += 1.0f * Time.deltaTime;

        if (hunger >= 100f || thirst >= 100f) Die();
    }

    private void Die()
    {
        isDead = true;
        Destroy(gameObject);
    }

    public void DrinkWater() => thirst = Mathf.Max(0f, thirst - 80f);
    public void ConsumeFood() => hunger = Mathf.Max(0f, hunger - 60f);
    public void Rest() => fatigue = Mathf.Max(0f, fatigue - 80f);

    // Standing at a river/pond edge quenches thirst on the spot — no carrying,
    // no trip back to camp required.
    public void DrinkWaterInstant() => thirst = Mathf.Max(0f, thirst - 80f);
}