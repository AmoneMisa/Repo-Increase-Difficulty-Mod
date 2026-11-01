using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using TMPro;

namespace IncreaseHardMod.Patches.Effects;

/// <summary>
/// Passive lobby/menu overlay showing the current Photon connection: region, ping,
/// room code and player count. It ONLY reads already-available PhotonNetwork state
/// (no sockets, no callbacks, no RaiseEvent), so it is safe to run on the main menu
/// and cannot interfere with REPO's own "pick best region" step. Asset-free IMGUI.
/// </summary>
internal sealed class ServerInfoHud : MonoBehaviour
{
    private GUIStyle _title;
    private GUIStyle _row;
    private bool _loggedOnce;
    private NativeHudPanel _surface;
    private TextMeshProUGUI _nativeText;

    private void Awake()
    {
        Plugin.Log.LogInfo("[ServerInfo] Awake — component alive.");
    }

    private void Start()
    {
        Plugin.Log.LogInfo("[ServerInfo] Start — reached next frame.");
    }

    private void OnEnable()
    {
        Plugin.Log.LogInfo("[ServerInfo] OnEnable.");
    }

    private void OnDisable()
    {
        Plugin.Log.LogInfo("[ServerInfo] OnDisable — component/object was disabled.");
    }

    private void OnDestroy()
    {
        if (_surface != null) Destroy(_surface.gameObject);
        Plugin.Log.LogInfo("[ServerInfo] OnDestroy — object was destroyed.");
    }

    private void Update()
    {
        if (!ModConfig.ServerInfoEnabled.Value || HUDCanvas.instance == null)
        {
            if (_surface != null) _surface.gameObject.SetActive(false);
            return;
        }
        if (_surface == null)
        {
            _surface = NativeHudPanel.Create("IHM_ServerInfo", new Vector2(420f, 70f),
                new Vector2(0f, 1f), new Vector2(16f, -12f), 0f);
            if (_surface == null) return;
            _nativeText = _surface.Label("Connection", 18f, TextAlignmentOptions.TopLeft);
            _nativeText.richText = false;
            _nativeText.rectTransform.anchorMin = Vector2.zero;
            _nativeText.rectTransform.anchorMax = Vector2.one;
            _nativeText.rectTransform.offsetMin = new Vector2(0f, 0f);
            _nativeText.rectTransform.offsetMax = new Vector2(0f, 0f);
        }
        var room=PhotonNetwork.CurrentRoom;
        string role=room==null ? "" : PhotonNetwork.IsMasterClient ? " | HOST" : " | CLIENT";
        string region=string.IsNullOrEmpty(PhotonNetwork.CloudRegion)?"-":PhotonNetwork.CloudRegion;
        string ping=PhotonNetwork.IsConnected ? $" | {PhotonNetwork.GetPing()} ms" : "";
        string players=room!=null ? $" | {room.PlayerCount}/{room.MaxPlayers} players" : "";
        _nativeText.text=$"SERVER: {SafeState()}{role}\n{region}{ping}{players}";
        if(room!=null && !string.IsNullOrEmpty(room.Name)) _nativeText.text+=$"\nROOM: {room.Name}";
        _surface.gameObject.SetActive(true);
        _surface.AvoidVitals(HealthUI.instance,EnergyUI.instance);
    }

    private void OnGUI()
    {
        if (!ModConfig.ServerInfoEnabled.Value || HUDCanvas.instance != null)
            return;

        if (!_loggedOnce)
        {
            _loggedOnce = true;
            Plugin.Log.LogInfo("[ServerInfo] OnGUI is running — panel should be visible.");
        }

        if (_title == null)
        {
            _title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperLeft,
            };
            _title.normal.textColor = new Color(0.5f, 0.85f, 1f);
            _row = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperLeft, wordWrap = false,
            };
            _row.normal.textColor = Color.white;
        }

        const float x = 12f;
        float y = 70f;

        // Solid backdrop so the panel is unmistakable while we confirm it renders.
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect(x - 4f, y - 4f, 220f, 150f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        DrawShadowed(new Rect(x, y, 300f, 20f), "SERVER", _title);
        y += 24f;

        foreach (string line in BuildLines())
        {
            DrawShadowed(new Rect(x, y, 300f, 18f), line, _row);
            y += 18f;
        }
    }

    private static System.Collections.Generic.IEnumerable<string> BuildLines()
    {
        string region = string.IsNullOrEmpty(PhotonNetwork.CloudRegion) ? "-" : PhotonNetwork.CloudRegion;
        yield return $"State: {SafeState()}";
        yield return $"Region: {region}";

        if (PhotonNetwork.IsConnected)
            yield return $"Ping: {PhotonNetwork.GetPing()} ms";

        Room room = PhotonNetwork.CurrentRoom;
        if (room != null)
        {
            if (!string.IsNullOrEmpty(room.Name))
                yield return $"Room: {room.Name}";
            yield return $"Players: {room.PlayerCount}/{room.MaxPlayers}";
            yield return PhotonNetwork.IsMasterClient ? "You are: HOST" : "You are: CLIENT";
        }
    }

    private static string SafeState()
    {
        try
        {
            return PhotonNetwork.NetworkClientState.ToString();
        }
        catch
        {
            return "Unknown";
        }
    }

    private void DrawShadowed(Rect rect, string text, GUIStyle style)
    {
        Color prev = style.normal.textColor;
        style.normal.textColor = new Color(0f, 0f, 0f, 0.7f);
        GUI.Label(new Rect(rect.x + 1.5f, rect.y + 1.5f, rect.width, rect.height), text, style);
        style.normal.textColor = prev;
        GUI.Label(rect, text, style);
    }
}
