using SwiftlyS2.Shared.SchemaDefinitions;

namespace SwiftlyS2.Shared.Helpers;

public interface IHelpers
{
    /// <summary>
    /// Get Weapon VData From Key
    /// </summary>
    /// <param name="unknown">Not sure what this argument is for, but in general it's -1</param>
    /// <param name="key">The key of the weapon (usually item idx)</param>
    /// <returns>The weapon VData</returns>
    public CCSWeaponBaseVData GetWeaponCSDataFromKey(int unknown, string key);
}