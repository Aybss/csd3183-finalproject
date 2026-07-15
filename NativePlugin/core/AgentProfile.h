// AgentProfile.h
#pragma once

enum class DisabilityType {
    None,
    Wheelchair,
    Blind,
    Deaf,
    Elderly
};

struct AgentProfile {
    DisabilityType disability = DisabilityType::None;
    
    // Dynamic Internal States (change during runtime)
    float fatigue = 0.0f;
    float cognitiveLoad = 0.0f;

    // Static Multipliers (define how hard actions are for this agent)
    float crowdCostMultiplier = 1.0f;
    float rampCostMultiplier = 1.0f;
    float stairCostMultiplier = 1.0f;
    float tactilePavingBonus = 1.0f;

    // Factory functions for quick testing
    static AgentProfile CreateWheelchairProfile() {
        AgentProfile p;
        p.disability = DisabilityType::Wheelchair;
        p.rampCostMultiplier = 0.5f;   // Prefers ramps
        p.stairCostMultiplier = 1e9f;  // Cannot use stairs
        return p;
    }

    static AgentProfile CreateBlindProfile() {
        AgentProfile p;
        p.disability = DisabilityType::Blind;
        p.tactilePavingBonus = 0.5f;   // Prefers tactile paving
        return p;
    }
};