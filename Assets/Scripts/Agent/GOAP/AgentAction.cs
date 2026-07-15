// Mirrors the ActionCode enum in PathfinderCore/goap/AgentActions.h — the
// int NativeBridge.PlanNextAction() returns. Keep these two enums in sync;
// nothing enforces it automatically since they cross the C++/C# boundary as
// a plain int.
public enum AgentAction
{
    ExploreUnknown = 0,
    NavigateToWood = 1,
    CollectWood = 2,
    NavigateToFood = 3,
    Eat = 4,
    DetectFoodBySound = 5,
    NavigateToStone = 6,
    MineStone = 7,
    NavigateToWater = 8,
    DrinkWater = 9,
    NavigateToBuildSite = 10,
    DeliverWood = 11,
    DeliverStone = 12,
    NavigateToCamp = 13,
    Rest = 14,
}
