using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ENet;

public class TestClient : MonoBehaviour
{
    [SerializeField]
    private string m_AddressIP;

    [SerializeField]
    private ushort m_Port;

    private ENet.Host m_Client;
    private ENet.Peer m_Peer;

    private bool m_IsRunning = false;

    private void Awake()
    {
        ENet.Library.Initialize();
    }

    private void OnDestroy()
    {
        ENet.Library.Deinitialize();
    }

    // Update is called once per frame
    void Update()
    {
        if (m_IsRunning)
        {
            ENet.Event evt;
            var chkEvtCode = m_Client.CheckEvents(out evt);
            if (chkEvtCode <= 0)
            {
                Debug.Log($"CheckEvents <= 0: {chkEvtCode}");
                var serviceCode = m_Client.Service(15, out evt);
                if (serviceCode <= 0)
                {
                    Debug.Log($"Service <= 0: {serviceCode}");
                    return;
                }
            }

            switch (evt.Type)
            {
                case ENet.EventType.None:
                    Debug.Log("Event None");
                    break;

                case ENet.EventType.Connect:
                    Debug.Log("Client connected to server - ID: " + m_Peer.ID);
                    break;

                case ENet.EventType.Disconnect:
                    Debug.Log("Client disconnected from server");
                    break;

                case ENet.EventType.Timeout:
                    Debug.Log("Client connection timeout");
                    break;

                case ENet.EventType.Receive:
                    Debug.Log("Packet received from server - Channel ID: " + evt.ChannelID + ", Data length: " + evt.Packet.Length);
                    evt.Packet.Dispose();
                    break;
            }
        }
    }

    private void OnGUI()
    {
        /*
        if (GUILayout.Button("Blocking Connect"))
        {
            Address address = new Address();

            address.SetHost("127.0.0.1");
            address.Port = 1234;

            m_Client = new Host();
            m_Client.Create();

            m_Peer = m_Client.Connect(address);
            m_IsRunning = true;
        }
        */
        if (m_IsRunning)
        {
            if (GUILayout.Button("Disconnect"))
            {
                m_Client.Dispose();
                m_IsRunning = false;
            }
        }
        else
        {
            if (GUILayout.Button("Connect"))
            {
                m_Client = new ENet.Host();

                ENet.Address address = new ENet.Address();
                address.SetHost(m_AddressIP);
                address.Port = m_Port;
                m_Client.Create();

                try
                {
                    m_Peer = m_Client.Connect(address);
                    Debug.Log($"Host Connect!");
                    m_IsRunning = true;
                }
                catch (Exception e)
                {
                    Debug.LogError($"Host Connect Failed: {e.Message}");
                    m_IsRunning = false;
                    m_Client.Dispose();
                }
            }
        }
    }
}
