using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ENet;
using System.IO;

public class PlayerManager : MonoBehaviour
{
    public enum PacketId : byte
    {
        LoginRequest = 1,
        LoginResponse = 2,
        LoginEvent = 3,
        PositionUpdateRequest = 4,
        PositionUpdateEvent = 5,
        LogoutEvent = 6
    }

    [Header("Prefab")]

    [SerializeField]
    private GameObject m_OtherPlayerPrefab;

    [Header("Reference")]

    [SerializeField]
    private DYP.BasicMovementController2D m_Player;

    [Header("Settings")]

    [SerializeField]
    private string m_ServerIP = "127.0.0.1";

    [SerializeField]
    private ushort m_SererPort = 1234;

    [SerializeField]
    private float m_SyncDeltaDistance = 32;

    [SerializeField]
    private float m_SkipFramesCount = 5;

    [Header("Debug")]

    [SerializeField]
    private uint m_PlayerId;

    private Host m_Client;
    private Peer m_Peer;
    private int m_SkippedFrames = 0;

    private List<uint> m_PlayerIds = new List<uint>();
    private Dictionary<uint, GameObject> m_OtherPlayers = new Dictionary<uint, GameObject>();
    private Dictionary<uint, Vector3> m_OtherPlayerLatestPositions = new Dictionary<uint, Vector3>();

    const int channelID = 0;

    void Start()
    {
        Application.runInBackground = true;
        initENet();
    }


    void FixedUpdate()
    {
        updateENet();

        // smooth moving players
        for (int i = 0; i < m_PlayerIds.Count; i++)
        {
            var playerId = m_PlayerIds[i];
            var currPos = m_OtherPlayers[playerId].transform.position;
            var targetPos = m_OtherPlayerLatestPositions[playerId];

            var sqrMag = (currPos - targetPos).sqrMagnitude;

            if (sqrMag > Mathf.Pow(m_SyncDeltaDistance * Time.fixedDeltaTime, 2))
            {
                m_OtherPlayers[playerId].transform.position = Vector3.Lerp(currPos, targetPos, 1.0f - Mathf.Pow(0.5f, Time.fixedDeltaTime / 0.02f));
            }
            else if (sqrMag > 0.0001f)
            {
                m_OtherPlayers[playerId].transform.position = Vector3.MoveTowards(currPos, targetPos, m_SyncDeltaDistance * Time.fixedDeltaTime);
                //m_OtherPlayers[playerId].transform.position = Vector3.Lerp(currPos, targetPos, 1.0f - Mathf.Pow(0.5f, Time.fixedDeltaTime / 0.05f));
            }
            else
            {
                m_OtherPlayers[playerId].transform.position = targetPos;
            }
        }

        // count sync frames
        if (++m_SkippedFrames < m_SkipFramesCount)
            return;

        sendPositionUpdate();
        m_SkippedFrames = 0;
    }

    void OnDestroy()
    {
        m_Peer.DisconnectNow(0);
        m_Client.Dispose();
        ENet.Library.Deinitialize();
    }

    private void initENet()
    {
        ENet.Library.Initialize();
        m_Client = new Host();
        Address address = new Address();

        address.SetHost(m_ServerIP);
        address.Port = m_SererPort;
        m_Client.Create();
        Debug.Log("Connecting");
        m_Peer = m_Client.Connect(address);
    }

    private void updateENet()
    {
        ENet.Event netEvent;

        if (m_Client.CheckEvents(out netEvent) <= 0)
        {
            if (m_Client.Service(15, out netEvent) <= 0)
                return;
        }

        switch (netEvent.Type)
        {
            case ENet.EventType.None:
                break;

            case ENet.EventType.Connect:
                Debug.Log("Client connected to server - ID: " + m_Peer.ID);
                sendLogin();
                break;

            case ENet.EventType.Disconnect:
                Debug.Log("Client disconnected from server");
                break;

            case ENet.EventType.Timeout:
                Debug.Log("Client connection timeout");
                break;

            case ENet.EventType.Receive:
                Debug.Log("Packet received from server - Channel ID: " + netEvent.ChannelID + ", Data length: " + netEvent.Packet.Length);
                parsePacket(ref netEvent);
                netEvent.Packet.Dispose();
                break;
        }
    }

    private void sendPositionUpdate()
    {
        var x = m_Player.transform.position.x;
        var y = m_Player.transform.position.y;
        var facing = m_Player.FacingDirection;

        var protocol = new Protocol();
        var buffer = protocol.Serialize((byte)PacketId.PositionUpdateRequest, m_PlayerId, x, y, facing);
        var packet = default(Packet);
        packet.Create(buffer);
        m_Peer.Send(channelID, ref packet);
    }

    private void sendLogin()
    {
        Debug.Log("SendLogin");
        var protocol = new Protocol();
        var buffer = protocol.Serialize((byte)PacketId.LoginRequest, 0);
        var packet = default(Packet);
        packet.Create(buffer);
        m_Peer.Send(channelID, ref packet);
    }

    private void parsePacket(ref ENet.Event netEvent)
    {
        var readBuffer = new byte[1024];
        var readStream = new MemoryStream(readBuffer);
        var reader = new BinaryReader(readStream);

        readStream.Position = 0;
        netEvent.Packet.CopyTo(readBuffer);
        var packetId = (PacketId)reader.ReadByte();

        Debug.Log("ParsePacket received: " + packetId);

        if (packetId == PacketId.LoginResponse)
        {
            m_PlayerId = reader.ReadUInt32();
            Debug.Log("MyPlayerId: " + m_PlayerId);
        }
        else if (packetId == PacketId.LoginEvent)
        {
            var playerId = reader.ReadUInt32();
            Debug.Log("OtherPlayerId: " + playerId);
            spawnOtherPlayer(playerId);
        }
        else if (packetId == PacketId.PositionUpdateEvent)
        {
            var playerId = reader.ReadUInt32();
            var x = reader.ReadSingle();
            var y = reader.ReadSingle();
            var facing = reader.ReadInt32();
            updateOtherPlayerPosition(playerId, x, y, facing);
        }
        else if (packetId == PacketId.LogoutEvent)
        {
            var playerId = reader.ReadUInt32();
            if (m_OtherPlayers.ContainsKey(playerId))
            {
                Destroy(m_OtherPlayers[playerId]);
                m_PlayerIds.Remove(playerId);
                m_OtherPlayers.Remove(playerId);
                m_OtherPlayerLatestPositions.Remove(playerId);
            }
        }
    }

    private void spawnOtherPlayer(uint playerId)
    {
        if (playerId == m_PlayerId)
            return;

        var newPlayer = Instantiate(m_OtherPlayerPrefab);
        newPlayer.transform.position = newPlayer.transform.position + new Vector3(Random.Range(-5.0f, 5.0f), Random.Range(-5.0f, 5.0f));
        Debug.Log("Spawn other object " + playerId);
        m_PlayerIds.Add(playerId);
        m_OtherPlayers[playerId] = newPlayer;
        m_OtherPlayerLatestPositions[playerId] = newPlayer.transform.position;
    }

    private void updateOtherPlayerPosition(uint playerId, float x, float y, int facing)
    {
        if (playerId == m_PlayerId)
            return;

        Debug.Log("UpdatePosition " + playerId);
        m_OtherPlayers[playerId].transform.localScale = new Vector3(facing, 1, 1);
        m_OtherPlayerLatestPositions[playerId] = new Vector3(x, y, 0);
    }

    private void OnGUI()
    {
        if (GUILayout.Button("Reload"))
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(0);
        }
    }
}
