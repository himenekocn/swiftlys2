using SwiftlyS2.Core.Natives;
using SwiftlyS2.Core.SchemaDefinitions;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.ProtobufDefinitions;
using SwiftlyS2.Shared.SchemaDefinitions;
using SwiftlyS2.Shared.Translation;

namespace SwiftlyS2.Core.Players;

internal class Player : IPlayer
{
    private int _pid;

    public Player(int pid)
    {
        _pid = pid;
    }

    public int PlayerID => _pid;

    public bool IsFakeClient => NativePlayer.IsFakeClient(_pid);

    public bool IsAuthorized => NativePlayer.IsAuthorized(_pid);

    public uint ConnectedTime => NativePlayer.GetConnectedTime(_pid);

    public Language PlayerLanguage => new(NativePlayer.GetLanguage(_pid));

    public ulong SteamID => NativePlayer.GetSteamID(_pid);

    public ulong UnauthorizedSteamID => NativePlayer.GetUnauthorizedSteamID(_pid);

    public CCSPlayerController Controller => new CCSPlayerControllerImpl(NativePlayer.GetController(_pid));

    public CCSPlayerController RequiredController => Controller is { IsValid: true } controller ? controller : throw new InvalidOperationException("Controller is not valid");

    public CBasePlayerPawn? Pawn => Controller?.Pawn.Value;

    public CBasePlayerPawn RequiredPawn => Pawn is { IsValid: true } pawn ? pawn : throw new InvalidOperationException("Pawn is not valid");

    public CCSPlayerPawn? PlayerPawn => Controller?.PlayerPawn.Value;

    public CCSPlayerPawn RequiredPlayerPawn => PlayerPawn is { IsValid: true } pawn ? pawn : throw new InvalidOperationException("PlayerPawn is not valid");

    public GameButtonFlags PressedButtons => (GameButtonFlags)NativePlayer.GetPressedButtons(_pid);

    public string IPAddress => NativePlayer.GetIPAddress(_pid);

    public VoiceFlagValue VoiceFlags { get => (VoiceFlagValue)NativeVoiceManager.GetClientVoiceFlags(_pid); set => NativeVoiceManager.SetClientVoiceFlags(_pid, (int)value); }

    public bool IsValid =>
        Controller is { IsValid: true, IsHLTV: false, Connected: PlayerConnectedState.PlayerConnected } &&
        Pawn is { IsValid: true };

    Language IPlayer.PlayerLanguage => PlayerLanguage;

    public void ChangeTeam(Team team)
    {
        NativePlayer.ChangeTeam(_pid, (byte)team);
    }

    public void ClearTransmitEntityBlocks()
    {
        NativePlayer.ClearTransmitEntityBlocked(_pid);
    }

    public ListenOverride GetListenOverride(int player)
    {
        return (ListenOverride)NativeVoiceManager.GetClientListenOverride(_pid, player);
    }

    public bool IsTransmitEntityBlocked(int entityid)
    {
        return NativePlayer.IsTransmitEntityBlocked(_pid, entityid);
    }

    public void Kick(string reason, ENetworkDisconnectionReason gameReason)
    {
        NativePlayer.Kick(_pid, reason, (int)gameReason);
    }

    public void SendMessage(MessageType kind, string message)
    {
        NativePlayer.SendMessage(_pid, (int)kind, message);
    }

    public void SetListenOverride(int player, ListenOverride listenOverride)
    {
        NativeVoiceManager.SetClientListenOverride(_pid, player, (int)listenOverride);
    }

    public void ShouldBlockTransmitEntity(int entityid, bool shouldBlockTransmit)
    {
        NativePlayer.ShouldBlockTransmitEntity(_pid, entityid, shouldBlockTransmit);
    }

    public void SwitchTeam(Team team)
    {
        NativePlayer.SwitchTeam(_pid, (byte)team);
    }

    public void TakeDamage(CTakeDamageInfo damageInfo)
    {
        unsafe
        {
            NativePlayer.TakeDamage(_pid, (nint)(&damageInfo));
        }
    }

    public void Teleport(Vector pos, QAngle angle, Vector velocity)
    {
        NativePlayer.Teleport(_pid, pos, angle, velocity);
    }

    public void Respawn()
    {
        Controller?.Respawn();
    }

    public void ExecuteCommand(string command)
    {
        NativePlayer.ExecuteCommand(_pid, command);
    }

    public bool Equals(IPlayer? other)
    {
        if (other is null) return false;
        return PlayerID == other.PlayerID;
    }

    public override bool Equals(object? obj)
    {
        return obj is IPlayer player && Equals(player);
    }

    public override int GetHashCode()
    {
        return PlayerID.GetHashCode();
    }

    public static bool operator ==(Player? left, Player? right)
    {
        if (left is null) return right is null;
        return left.Equals(right);
    }

    public static bool operator !=(Player? left, Player? right)
    {
        return !(left == right);
    }
}