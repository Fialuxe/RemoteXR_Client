using UnityEngine;
using Photon.Pun;

/// <summary>
/// Add this script to an empty GameObject in your scene to see debug info
/// This helps troubleshoot multiplayer issues
/// </summary>
public class PhotonDebugUI : MonoBehaviourPunCallbacks
{
    private Vector2 scrollPos;
    private bool showDebug = true;

    void OnGUI()
    {
        if (!showDebug) return;

        GUILayout.BeginArea(new Rect(10, 10, 400, 500));
        GUILayout.BeginVertical("box");
        
        GUILayout.Label("=== PHOTON DEBUG INFO ===", GUI.skin.box);
        
        // Connection Status
        GUILayout.Label($"Connected: {PhotonNetwork.IsConnected}");
        GUILayout.Label($"In Room: {PhotonNetwork.InRoom}");
        GUILayout.Label($"Server: {PhotonNetwork.Server}");
        GUILayout.Label($"Region: {PhotonNetwork.CloudRegion}");
        
        if (PhotonNetwork.InRoom)
        {
            GUILayout.Label($"Room Name: {PhotonNetwork.CurrentRoom.Name}");
            GUILayout.Label($"Players in Room: {PhotonNetwork.CurrentRoom.PlayerCount}");
            GUILayout.Label($"Max Players: {PhotonNetwork.CurrentRoom.MaxPlayers}");
            
            GUILayout.Space(10);
            GUILayout.Label("--- PLAYERS ---", GUI.skin.box);
            
            foreach (var player in PhotonNetwork.PlayerList)
            {
                string isMine = player.IsLocal ? " (YOU)" : " (REMOTE)";
                GUILayout.Label($"• {player.NickName} - ID:{player.ActorNumber}{isMine}");
            }
            
            GUILayout.Space(10);
            GUILayout.Label("--- INSTANTIATED OBJECTS ---", GUI.skin.box);
            
            // Find all PhotonView objects
            PhotonView[] photonViews = FindObjectsOfType<PhotonView>();
            GUILayout.Label($"Total Objects: {photonViews.Length}");
            
            foreach (var pv in photonViews)
            {
                string owner = pv.IsMine ? "MINE" : $"Owner:{pv.Owner?.NickName}";
                string color = pv.IsMine ? "BLUE" : "RED";
                GUILayout.Label($"• {pv.gameObject.name} - {owner} ({color})");
            }
        }
        else
        {
            GUILayout.Label("Not in a room yet...");
        }
        
        GUILayout.Space(10);
        if (GUILayout.Button("Hide Debug UI"))
        {
            showDebug = false;
        }
        
        GUILayout.EndVertical();
        GUILayout.EndArea();
        
        // Button to show again
        if (!showDebug)
        {
            if (GUI.Button(new Rect(10, 10, 120, 30), "Show Debug UI"))
            {
                showDebug = true;
            }
        }
    }

    public override void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer)
    {
        Debug.Log($"<color=green>Player Joined: {newPlayer.NickName} (ID: {newPlayer.ActorNumber})</color>");
    }

    public override void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer)
    {
        Debug.Log($"<color=red>Player Left: {otherPlayer.NickName} (ID: {otherPlayer.ActorNumber})</color>");
    }
}
