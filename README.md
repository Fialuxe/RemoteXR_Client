# RemoteXR_Client

A Unity-based remote expert collaboration system that enables real-time multi-user experiences with spatial alignment and eye gaze tracking capabilities. This client allows remote users (desktop/laptop) to view and interact with VR users' environments through networked 3D representations.

## set up eye and face mesh tracking
### Mac
if you are using mac or equivalent(zsh or bash?),
you can use run_demo.sh to run the demo to track eye-gaze and face mesh.
```
cd /path/to/remoteXR_client
./run_demo.sh
```
### Windows
if you are using windows, please install python and run the command below.

### Key Features

- **Multi-User Networking**: Real-time synchronization using Photon PUN (Photon Unity Networking)
- **Spatial Alignment System**: Automatic coordinate system alignment between VR and desktop users
- **Mesh Visualization**: View and interact with 3D-scanned environments from VR users
- **Free-Fly Camera**: Navigate the remote environment with WASD + mouse controls
- **Eye Gaze Integration**: Receive and display eye tracking data via Lab Streaming Layer (LSL)
- **Interactive Mesh Alignment**: Tools for VR users to calibrate and align their scanned meshes
- **Player Representation**: Visual markers showing remote and local user positions
- **Debug UI**: Real-time networking and alignment status display

## 🏗️ Architecture

### Core Components

1. **RemoteClient.cs**
   - Handles remote user connection and free-fly camera controls
   - Manages Photon networking connection
   - Synchronizes remote user position/rotation with other clients
   - WASD + mouse look controls for desktop navigation

2. **NetworkedPlayer.cs**
   - Represents each player in the networked scene
   - Handles spatial coordinate transformation between VR and desktop users
   - Provides visual differentiation (blue for local, red for remote)
   - Displays player name tags

3. **SpatialAlignmentManager.cs**
   - Manages coordinate system alignment between different client types
   - Supports multiple alignment modes:
     - `AutoAlign`: Automatic mesh-based alignment
     - `ManualAlign`: Manual offset configuration
     - `MarkerBased`: Calibration using alignment markers
     - `SharedOrigin`: Same coordinate system (no alignment needed)
   - Synchronizes alignment data across all clients via RPC

4. **MeshAlignmentTool.cs**
   - Interactive tool for VR users to align scanned meshes with real-world geometry
   - Keyboard controls for position, rotation, and scale adjustments
   - Fine adjustment mode for precise calibration
   - Persistent alignment storage via PlayerPrefs
   - Network synchronization of alignment changes

5. **LslGazeReceiver.cs**
   - Receives real-time eye gaze data via Lab Streaming Layer (LSL)
   - Connects to external eye tracking systems (e.g., EyeTrax)
   - Processes 3-channel gaze data: [gaze_x, gaze_y, pupil_size]

6. **PhotonDebugUI.cs**
   - On-screen debug information for networking status
   - Displays connection state, room info, and player count

## 🚀 Getting Started

### Prerequisites

- **Unity 2021.3 LTS or newer** (tested with Unity 2021.3+)
- **Photon PUN 2** (included in project)
- **Lab Streaming Layer for Unity** (optional, for eye tracking)
- **A Photon account** (free tier available at [Photon Engine](https://www.photonengine.com/))

### Installation

1. **Clone the repository**
   ```bash
   git clone https://github.com/prasanthsasikumar/RemoteXR_Client.git
   cd RemoteXR_Client
   ```

2. **Open in Unity**
   - Launch Unity Hub
   - Click "Open" and select the `RemoteXR_Client` folder
   - Wait for Unity to import all assets

3. **Configure Photon**
   - Create a free account at [Photon Engine](https://www.photonengine.com/)
   - Create a new PUN application
   - In Unity: `Window > Photon Unity Networking > PUN Wizard`
   - Enter your Photon App ID
   - Set the region to your preferred server location

4. **Setup Required Prefabs**
   - Ensure `LocalClientCube` prefab exists in `Assets/Resources/` folder
   - Add `PhotonView` component to the prefab
   - Add `PhotonTransformView` component for position/rotation sync
   - Add `NetworkedPlayer` component
   - Configure PhotonView to observe the PhotonTransformView component

### Configuration

#### Room Settings
By default, clients connect to room: **"MeshVRRoom"**
- Maximum players: 4
- Auto-reconnect enabled

To change the room name, edit the `OnConnectedToMaster()` method in `RemoteClient.cs`:
```csharp
PhotonNetwork.JoinOrCreateRoom("YourRoomName", new RoomOptions { MaxPlayers = 4 }, TypedLobby.Default);
```
cd \path\to\remoteXR_client
python -m venv .\venv
.\venv\Scripts\activate
pip install requirements.txt
python lsl_server.py
```

Thank you.

https://github.com/ck-zhang/EyeTrax
