using ExitGames.Client.Photon;
using IncreaseHardMod.Modifiers;
using IncreaseHardMod.Patches.Effects;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace IncreaseHardMod.Networking;

/// <summary>
/// Handles sending and receiving the rolled modifier set over Photon.
///
/// We use Photon's low-level RaiseEvent API (the same one REPO itself uses for
/// kick/ban, codes 123/124). It doesn't need a PhotonView, which is exactly what
/// we want since a BepInEx mod can't easily own one.
///
/// This is a MonoBehaviour so it can implement IOnEventCallback and be registered
/// with PhotonNetwork.AddCallbackTarget — that's how Photon delivers raised
/// events to us via OnEvent().
///
/// IMPORTANT: RaiseEvent to ReceiverGroup.All does NOT echo back to the sender.
/// So the host must apply its rolled state locally itself (ModifierManager does
/// that) and only RaiseEvent to inform the OTHER clients.
/// </summary>
public sealed class ModNetwork : MonoBehaviour, IOnEventCallback
{
    // Our private event codes. Must not collide with REPO's (123/124/199) or PUN's
    // reserved range (200+). 178/179 are free.
    public const byte ModifierEventCode = 178;
    public const byte LevelTimerEventCode = 179;
    public const byte CartUpgradeEventCode = 180;

    public static ModNetwork Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable() => PhotonNetwork.AddCallbackTarget(this);
    private void OnDisable() => PhotonNetwork.RemoveCallbackTarget(this);

    // Drives the LevelTimer modifier: the host counts down and periodically
    // broadcasts the authoritative remaining time; clients smooth locally between
    // broadcasts. Persistent (DontDestroyOnLoad), so it survives level loads.
    private void Update()
    {
        LevelTimerController.Tick(Time.deltaTime);
        LateJoinController.Tick();
    }

    /// <summary>Host broadcasts the authoritative timer state to other clients.</summary>
    public void BroadcastLevelTimer(float remaining, bool running)
    {
        if (!PhotonNetwork.InRoom)
            return;

        var options = new RaiseEventOptions { Receivers = ReceiverGroup.All };
        PhotonNetwork.RaiseEvent(
            LevelTimerEventCode,
            new object[] { remaining, running },
            options,
            SendOptions.SendUnreliable);
    }

    /// <summary>
    /// Host broadcasts the rolled modifiers to all other clients. Reliable so it
    /// can't be dropped. (Late joiners are not covered yet — REPO players join at
    /// the lobby, not mid-level, so this is an acceptable v1 limitation.)
    /// </summary>
    public void BroadcastModifiers(LevelModifiers modifiers)
    {
        if (!PhotonNetwork.InRoom)
            return;

        var options = new RaiseEventOptions { Receivers = ReceiverGroup.All };
        PhotonNetwork.RaiseEvent(
            ModifierEventCode,
            modifiers.Serialize(),
            options,
            SendOptions.SendReliable);
    }

    /// <summary>Host broadcasts the current cart-upgrade levels to other clients.</summary>
    public void BroadcastCartUpgrades(int healLevel, int chargeLevel, bool sellUnlocked)
    {
        if (!PhotonNetwork.InRoom)
            return;

        var options = new RaiseEventOptions { Receivers = ReceiverGroup.All };
        PhotonNetwork.RaiseEvent(
            CartUpgradeEventCode,
            new object[] { healLevel, chargeLevel, sellUnlocked },
            options,
            SendOptions.SendReliable);
    }

    /// <summary>Photon calls this on every client for every raised event.</summary>
    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code == ModifierEventCode)
        {
            var data = (object[])photonEvent.CustomData;
            var modifiers = LevelModifiers.Deserialize(data);
            ModifierManager.ApplyReceived(modifiers);
            return;
        }

        if (photonEvent.Code == LevelTimerEventCode)
        {
            var data = (object[])photonEvent.CustomData;
            LevelTimerController.ApplyNetworkState((float)data[0], (bool)data[1]);
            return;
        }

        if (photonEvent.Code == CartUpgradeEventCode)
        {
            var data = (object[])photonEvent.CustomData;
            CartUpgradeState.ApplyNetworkState((int)data[0], (int)data[1], (bool)data[2]);
        }
    }
}
