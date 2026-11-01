using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;
using TMPro;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// Small main-menu overlay that TCP-probes Photon name servers so you can see, before
/// hosting/joining, whether Photon is reachable and roughly how fast.
///
/// Why this exists: REPO connects with an empty ("best") region, which makes it ping the
/// Photon NAME SERVER first (log line "Photon - Set Region: " with nothing after). If that
/// name server is unreachable (firewall/ISP/outage) the game just sits on the loading
/// screen — the classic "stuck loading online". This HUD makes that failure visible.
///
/// Implementation is asset-free IMGUI. Probes run on short-lived background threads (a
/// TcpClient connect with a timeout) so the game never stalls; results are copied back to
/// the main thread under a lock and drawn in OnGUI. Nothing here touches Photon's own
/// connection — it's a passive, read-only reachability check.
/// </summary>
internal sealed class PhotonHud : MonoBehaviour
{
    private sealed class Probe
    {
        public string Label;
        public string Host;
        public int Port;
        public volatile int State; // 0 = checking, 1 = reachable, 2 = unreachable
        public volatile int LatencyMs;
    }

    private const float RefreshInterval = 15f;
    private const int TimeoutMs = 3000;

    private readonly List<Probe> _probes = new();
    private readonly object _lock = new();
    private float _nextRefresh;
    private string _parsedFrom;
    private GUIStyle _title;
    private GUIStyle _row;
    private NativeHudPanel _surface;
    private TextMeshProUGUI _text;

    private void Update()
    {
        if (!ModConfig.PhotonHudEnabled.Value || !SafeMenuLevel())
        {
            if(_surface!=null)_surface.gameObject.SetActive(false);
            return;
        }

        if (_parsedFrom != ModConfig.PhotonHudRegions.Value)
        {
            Parse(ModConfig.PhotonHudRegions.Value);
            _nextRefresh = 0f; // force an immediate check after a config edit
        }

        if (Time.realtimeSinceStartup >= _nextRefresh)
        {
            _nextRefresh = Time.realtimeSinceStartup + RefreshInterval;
            StartProbes();
        }
        RenderNative();
    }

    private void RenderNative()
    {
        if(HUDCanvas.instance==null)return;
        if(_surface==null)
        {
            _surface=NativeHudPanel.Create("IHM_PhotonDiagnostics",new Vector2(300,140),new Vector2(1,1),new Vector2(-18,-12),0f);
            if(_surface==null)return;
            _text=_surface.Label("Diagnostics",18f,TextAlignmentOptions.TopRight);
            _text.richText=false;
            _text.rectTransform.anchorMin=Vector2.zero;_text.rectTransform.anchorMax=Vector2.one;
            _text.rectTransform.offsetMin=_text.rectTransform.offsetMax=Vector2.zero;
            _text.color=new Color(.7f,.8f,.85f);
        }
        string lines="Photon reachability";
        lock(_lock)foreach(var probe in _probes)
            lines+="\n"+probe.Label+": "+(probe.State==1?probe.LatencyMs+" ms":probe.State==2?"unreachable":"checking...");
        _text.text=lines;
        _surface.gameObject.SetActive(true);
        _surface.Fit();
    }

    private void OnDestroy(){if(_surface!=null)Destroy(_surface.gameObject);}

    private void Parse(string spec)
    {
        lock (_lock)
        {
            _probes.Clear();
            foreach (string pair in spec.Split(','))
            {
                int eq = pair.IndexOf('=');
                if (eq <= 0)
                    continue;
                string label = pair.Substring(0, eq).Trim();
                string hostPart = pair.Substring(eq + 1).Trim();
                if (label.Length == 0 || hostPart.Length == 0)
                    continue;

                int port = 443;
                int colon = hostPart.LastIndexOf(':');
                if (colon > 0 && int.TryParse(hostPart.Substring(colon + 1), out int p))
                {
                    port = p;
                    hostPart = hostPart.Substring(0, colon);
                }
                _probes.Add(new Probe { Label = label, Host = hostPart, Port = port, State = 0 });
            }
        }
        _parsedFrom = spec;
    }

    private void StartProbes()
    {
        Probe[] snapshot;
        lock (_lock)
            snapshot = _probes.ToArray();

        foreach (var probe in snapshot)
        {
            probe.State = 0;
            var t = new Thread(() => RunProbe(probe)) { IsBackground = true };
            t.Start();
        }
    }

    private static void RunProbe(Probe probe)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            using var client = new TcpClient();
            var result = client.BeginConnect(probe.Host, probe.Port, null, null);
            bool ok = result.AsyncWaitHandle.WaitOne(TimeoutMs);
            sw.Stop();
            if (ok && client.Connected)
            {
                client.EndConnect(result);
                probe.LatencyMs = (int)sw.ElapsedMilliseconds;
                probe.State = 1;
            }
            else
            {
                probe.State = 2;
            }
        }
        catch (Exception)
        {
            probe.State = 2;
        }
    }

    private void OnGUI()
    {
        if (!ModConfig.PhotonHudEnabled.Value || !SafeMenuLevel() || HUDCanvas.instance!=null)
            return;

        Probe[] snapshot;
        lock (_lock)
        {
            if (_probes.Count == 0)
                return;
            snapshot = _probes.ToArray();
        }

        if (_title == null)
        {
            _title = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
            _title.normal.textColor = Color.white;
            _row = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
        }

        const float w = 210f;
        float h = 24f + snapshot.Length * 18f;
        var box = new Rect(Mathf.Max(10f,Screen.width-w-10f), 10f, w, h);

        GUI.Label(new Rect(box.x + 8f, box.y + 4f, w - 16f, 18f), "Photon reachability", _title);

        float y = box.y + 24f;
        foreach (var probe in snapshot)
        {
            string status;
            Color color;
            switch (probe.State)
            {
                case 1:
                    status = $"{probe.LatencyMs} ms";
                    color = probe.LatencyMs < 120 ? new Color(0.4f, 1f, 0.4f) : new Color(1f, 0.85f, 0.3f);
                    break;
                case 2:
                    status = "unreachable";
                    color = new Color(1f, 0.4f, 0.4f);
                    break;
                default:
                    status = "checking...";
                    color = new Color(0.8f, 0.8f, 0.8f);
                    break;
            }

            _row.normal.textColor = color;
            GUI.Label(new Rect(box.x + 8f, y, w - 16f, 18f), $"{probe.Label}: {status}", _row);
            y += 18f;
        }
    }

    private static bool SafeMenuLevel()
    {
        try
        {
            return SemiFunc.MenuLevel();
        }
        catch
        {
            return false;
        }
    }
}
