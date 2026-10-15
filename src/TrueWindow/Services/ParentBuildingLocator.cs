using StardewValley;
using StardewValley.Buildings;
using StardewValley.Locations;

// ----------------------------
// 親建物特定
// ----------------------------
namespace TrueWindow.Services;

internal sealed class ParentBuildingLocator
{
    // 室内ロケーションに対応する親建物を特定する
    public Building? Find(GameLocation location)
    {
        if (location.ParentBuilding is not null)
        {
            return location.ParentBuilding;
        }

        if (location is FarmHouse)
        {
            return Game1.getFarm()?.GetMainFarmHouse();
        }

        if (location is Cabin cabin)
        {
            Farm? farm = Game1.getFarm();
            if (farm is null)
            {
                return null;
            }

            foreach (Building building in farm.buildings)
            {
                GameLocation? indoors = building.GetIndoors();
                if (indoors is not null && indoors.NameOrUniqueName == cabin.NameOrUniqueName)
                {
                    return building;
                }
            }
        }

        return null;
    }
}
