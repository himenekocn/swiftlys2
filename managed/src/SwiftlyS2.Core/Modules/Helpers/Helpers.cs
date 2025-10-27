using SwiftlyS2.Core.Natives;
using SwiftlyS2.Core.SchemaDefinitions;
using SwiftlyS2.Shared.Helpers;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace SwiftlyS2.Core.Services;

internal class HelpersService : IHelpers
{
    public CCSWeaponBaseVData GetWeaponCSDataFromKey(int unknown, string key)
    {
        nint weaponDataPtr = GameFunctions.GetWeaponCSDataFromKey(unknown, key);
        return new CCSWeaponBaseVDataImpl(weaponDataPtr);
    }
}