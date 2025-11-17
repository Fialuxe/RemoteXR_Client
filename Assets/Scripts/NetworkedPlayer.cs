using UnityEngine;
using Photon.Pun;

public class NetworkedPlayer : MonoBehaviourPun, IPunObservable
{
    private Vector3 networkPosition;
    private Quaternion networkRotation;
    
    public float smoothSpeed = 10f;
    public Material localPlayerMaterial;
    public Material remotePlayerMaterial;
    
    private SpatialAlignmentManager alignmentManager;

    void Start()
    {
        // Set position and rotation to network values initially
        networkPosition = transform.position;
        networkRotation = transform.rotation;
        
        // Find the alignment manager
        alignmentManager = FindFirstObjectByType<SpatialAlignmentManager>();
        
        // CRITICAL: Ensure this component is registered in PhotonView.ObservedComponents
        if (photonView != null && !photonView.ObservedComponents.Contains(this))
        {
            photonView.ObservedComponents.Add(this);
            Debug.Log($"✓ Registered NetworkedPlayer in PhotonView.ObservedComponents for {photonView.Owner?.NickName}");
        }
        
        // Color the player based on ownership
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            if (photonView.IsMine)
            {
                // Local player - make it blue
                if (localPlayerMaterial != null)
                    renderer.material = localPlayerMaterial;
                else
                    renderer.material.color = Color.blue;
            }
            else
            {
                // Remote player - make it red
                if (remotePlayerMaterial != null)
                    renderer.material = remotePlayerMaterial;
                else
                    renderer.material.color = Color.red;
            }
        }
        
        // Add a name tag
        CreateNameTag();
    }

    void Update()
    {
        if (!photonView.IsMine)
        {
            // Apply spatial alignment for remote players
            Vector3 targetPosition = networkPosition;
            Quaternion targetRotation = networkRotation;
            
            if (alignmentManager != null && alignmentManager.IsAligned())
            {
                targetPosition = alignmentManager.TransformFromPlayer(photonView.Owner.ActorNumber, networkPosition);
                targetRotation = alignmentManager.TransformFromPlayer(photonView.Owner.ActorNumber, networkRotation);
            }
            
            // Smoothly interpolate to the aligned position for remote players
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * smoothSpeed);
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * smoothSpeed);
        }
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Send position and rotation to other clients
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
        }
        else
        {
            // Receive position and rotation from owner
            networkPosition = (Vector3)stream.ReceiveNext();
            networkRotation = (Quaternion)stream.ReceiveNext();
        }
    }

    void CreateNameTag()
    {
        // Create a simple text label above the player
        GameObject nameTagObj = new GameObject("NameTag");
        nameTagObj.transform.SetParent(transform);
        nameTagObj.transform.localPosition = new Vector3(0, 0.6f, 0);
        
        TextMesh textMesh = nameTagObj.AddComponent<TextMesh>();
        textMesh.text = photonView.Owner.NickName;
        textMesh.fontSize = 20;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.color = photonView.IsMine ? Color.cyan : Color.yellow;
        textMesh.characterSize = 0.1f;
    }
}
