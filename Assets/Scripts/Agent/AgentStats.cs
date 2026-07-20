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

        thirst += 1.0f * Time.deltaTime; // was 2.0 — gave agents too little time to find/reach water before dying
        hunger += 0.8f * Time.deltaTime; // was 1.5 — hit critical/death too fast to demo GOAP behavior over time
        fatigue += 1.0f * Time.deltaTime;

        if (hunger >= 100f || thirst >= 100f) Die();
    }

    private void Die()
    {
        // Stay in the scene as a visible "crossed out" corpse (see
        // AgentDeathVisual) instead of vanishing — AgentGOAP and UnityAgent
        // both check isDead and stop acting/moving once it's true.
        isDead = true;
    }

    // Used by the simulation UI's Kill Agent button — an explicit, immediate
    // death rather than waiting for hunger/thirst to naturally cross 100.
    public void ForceKill() => Die();

    public void DrinkWater() => thirst = Mathf.Max(0f, thirst - 80f);
    public void ConsumeFood() => hunger = Mathf.Max(0f, hunger - 60f);
    public void Rest() => fatigue = Mathf.Max(0f, fatigue - 80f);

    // Standing at a river/pond edge quenches thirst on the spot — no carrying,
    // no trip back to camp required.
    public void DrinkWaterInstant() => thirst = Mathf.Max(0f, thirst - 80f);
}