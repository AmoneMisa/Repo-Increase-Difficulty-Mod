using Photon.Pun;
using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// Guest-side late-join diagnostics. While this client is in a room but its level has not finished
/// generating, log (every 5 s) which step of LevelGenerator.Generate it is waiting on, so a stuck
/// mid-level join can be pinned to one handshake (players-ready, modules, local setup / navmesh,
/// GenerateDone). Read-only: it never changes game state.
/// </summary>
internal sealed class LateJoinDiagnostics : MonoBehaviour
{
    private float _next, _since;
    private bool _waiting;

    private void Update()
    {
        if (!ModConfig.LateJoinEnabled.Value) return;
        var gen = LevelGenerator.Instance;
        bool stuck = PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient && gen != null && !gen.Generated;
        if (!stuck)
        {
            if (_waiting && gen != null && gen.Generated)
                Plugin.Log.LogInfo($"[LateJoin] Guest level generated after {Time.realtimeSinceStartup - _since:0.0}s.");
            _waiting = false;
            return;
        }
        if (!_waiting) { _waiting = true; _since = Time.realtimeSinceStartup; _next = _since + 5f; }
        if (Time.realtimeSinceStartup < _next) return;
        _next = Time.realtimeSinceStartup + 5f;
        try
        {
            var room = PhotonNetwork.CurrentRoom;
            Plugin.Log.LogInfo(
                $"[LateJoin] Guest still loading {Time.realtimeSinceStartup - _since:0}s: state={gen.State}, " +
                $"allPlayersReady={gen.AllPlayersReady}, moduleAmountReceived={gen.moduleAmountReceived}, " +
                $"modules={gen.ModulesSpawned}/{gen.ModulesSpawnedTarget}, localSetupReady={gen.LocalSetupReady}, " +
                $"navMesh={gen.navMeshBuildState}, generateDonePending={gen.generateDonePending}, " +
                $"level={(RunManager.instance != null && RunManager.instance.levelCurrent != null ? RunManager.instance.levelCurrent.name : "?")}, " +
                $"director={(GameDirector.instance != null ? GameDirector.instance.currentState.ToString() : "?")}, " +
                $"queue={PhotonNetwork.IsMessageQueueRunning}, players={(room != null ? room.PlayerCount : 0)}.");
        }
        catch (System.Exception e)
        {
            Plugin.Log.LogWarning($"[LateJoin] Diagnostics failed harmlessly: {e.Message}");
        }
    }
}
