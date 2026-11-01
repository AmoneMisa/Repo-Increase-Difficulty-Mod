using System;
using Photon.Pun;
using Photon.Realtime;
using Photon.Voice.PUN;
using Photon.Voice.Unity;
using UnityEngine;

namespace IncreaseHardMod.Patches.Effects;

// Runs only on the local client. Silence and TransmitEnabled=false are valid states,
// so neither is used as evidence of a failed microphone.
internal sealed class VoiceRecoveryController : MonoBehaviour
{
    private Recorder _recorder;
    private volatile bool _deviceChanged;
    private float _deviceReadyAt;
    private float _nextStreamRecovery;
    private bool _manualPending;
    private float _nextCheck;
    private float _failedSince = -1;
    private float _nextRecovery;
    private float _nextReport;
    private int _attempts;

    private void OnEnable() => AudioSettings.OnAudioConfigurationChanged += OnAudioChanged;
    private void OnDisable() => AudioSettings.OnAudioConfigurationChanged -= OnAudioChanged;
    private void OnAudioChanged(bool changed)
    {
        // Unity can invoke this during audio reinitialization. Defer all work to Update.
        if (changed) _deviceChanged = true;
    }

    private void Update()
    {
        float now = Time.realtimeSinceStartup;
        try
        {
            var key = ModConfig.AudioRecoveryKey.Value;
            if (key != KeyCode.None && Input.GetKey(KeyCode.LeftControl) &&
                Input.GetKeyDown(key) && now >= _nextRecovery)
            {
                _nextRecovery = now + 15f;
                bool reset = AudioSettings.Reset(AudioSettings.GetConfiguration());
                Plugin.Log.LogInfo($"[VoiceRecovery] Manual audio reset accepted={reset}.");
                _deviceReadyAt = now + 2f;
                _manualPending = true;
                _attempts = 0;
            }

            if (_deviceChanged)
            {
                _deviceChanged = false;
                if (ModConfig.VoiceRecoveryEnabled.Value || _manualPending)
                    _deviceReadyAt = now + 2f;
            }
            if (_deviceReadyAt > 0f && now >= _deviceReadyAt && now >= _nextStreamRecovery)
            {
                _deviceReadyAt = 0f;
                _manualPending = false;
                _nextStreamRecovery = now + 15f;
                RestartVoiceStreams();
            }
            if (!ModConfig.VoiceRecoveryEnabled.Value) return;
            if (now < _nextCheck) return;
            _nextCheck = now + 2f;

            if (!PhotonNetwork.InRoom || !Application.isFocused)
            {
                _failedSince = -1;
                return;
            }
            if (ModConfig.NetworkDiagnosticsEnabled.Value && now >= _nextReport)
            {
                _nextReport = now + 60f;
                var peer = PhotonNetwork.NetworkingClient.LoadBalancingPeer;
                Plugin.Log.LogInfo($"[Network] Region={PhotonNetwork.CloudRegion}, RTT={peer.RoundTripTime}ms, variance={peer.RoundTripTimeVariance}, reliableResendsTotal={peer.ResentReliableCommands}.");
            }

            var local = PlayerVoiceChat.instance;
            var recorder = local != null ? local.GetComponent<Recorder>() : null;
            if (recorder != _recorder)
            {
                _recorder = recorder;
                _attempts = 0;
                _failedSince = -1;
            }
            if (recorder == null || !recorder.isActiveAndEnabled || !recorder.RecordingEnabled ||
                recorder.SourceType != Recorder.InputSourceType.Microphone ||
                PunVoiceClient.Instance == null || PunVoiceClient.Instance.Client.State != ClientState.Joined)
            {
                _failedSince = -1;
                return;
            }

            bool failed = recorder.LevelMeter == null;
            if (recorder.MicrophoneType == Recorder.MicType.Unity)
                failed |= !Microphone.IsRecording(recorder.MicrophoneDevice.IDString);
            if (!failed)
            {
                _failedSince = -1;
                return;
            }
            if (_failedSince < 0f) _failedSince = now;
            if (now - _failedSince < 10f || now < _nextRecovery || _attempts >= 3) return;
            _attempts++;
            _nextRecovery = now + 30f;
            recorder.RestartRecording();
            Plugin.Log.LogWarning($"[VoiceRecovery] Local capture failed for 10s; restart {_attempts}/3 requested. Transmission setting preserved.");
        }
        catch (Exception e)
        {
            _nextCheck = now + 30f;
            Plugin.Log.LogWarning($"[VoiceRecovery] Check deferred: {e.GetType().Name}: {e.Message}");
        }
    }

    private static void RestartVoiceStreams()
    {
        var local = PlayerVoiceChat.instance;
        var recorder = local != null ? local.GetComponent<Recorder>() : null;
        if (recorder != null && recorder.isActiveAndEnabled && recorder.RecordingEnabled)
            recorder.RestartRecording();
        foreach (var speaker in UnityEngine.Object.FindObjectsOfType<Speaker>())
            if (speaker.isActiveAndEnabled) speaker.RestartPlayback();
        Plugin.Log.LogInfo("[VoiceRecovery] Voice stream restart requested after audio reinitialization.");
    }
}
