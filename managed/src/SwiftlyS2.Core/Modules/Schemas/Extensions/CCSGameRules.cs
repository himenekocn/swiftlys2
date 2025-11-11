using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.SchemaDefinitions;
using SwiftlyS2.Shared.Schemas;

namespace SwiftlyS2.Shared.SchemaDefinitions;

public partial interface CCSGameRules
{
    /// <summary>
    /// Find the player that the controller is targetting
    /// </summary>
    /// <typeparam name="T">Entity Class</typeparam>
    /// <param name="controller">Player Controller</param>
    public T? FindPickerEntity<T>( CBasePlayerController controller ) where T : ISchemaClass<T>;

    public void TerminateRound( RoundEndReason reason, float delay );
}