using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class RemoteExpert : MonoBehaviourPunCallbacks
{
    public GameObject cubePrefab; // assign a simple Cube prefab in Inspector
    private GameObject remoteCube;

    void Start()
    {
        // Auto-connect to Photon
        PhotonNetwork.ConnectUsingSettings();
    }

    public override void OnConnectedToMaster()
    {
        // Join or create the same room
        PhotonNetwork.JoinOrCreateRoom("MeshVRRoom", new RoomOptions { MaxPlayers = 2 }, TypedLobby.Default);
    }

    public override void OnJoinedRoom()
    {
        Debug.Log("RemoteExpert joined room!");

        // Instantiate a cube for this remote camera
        remoteCube = PhotonNetwork.Instantiate(cubePrefab.name, Camera.main.transform.position, Camera.main.transform.rotation, 0);
        remoteCube.name = "RemoteExpertCube";
    }

    void Update()
    {
        if (remoteCube != null)
        {
            // Move the cube with the camera
            remoteCube.transform.position = Camera.main.transform.position;
            remoteCube.transform.rotation = Camera.main.transform.rotation;
        }
    }
}
