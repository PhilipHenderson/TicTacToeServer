using UnityEngine;
using UnityEngine.Assertions;
using Unity.Collections;
using Unity.Networking.Transport;
using System.Text;
using System.IO;
using UnityEditor.MemoryProfiler;

public class NetworkServer : MonoBehaviour
{
    public NetworkDriver networkDriver;
    private NativeList<NetworkConnection> networkConnections;

    NetworkPipeline reliableAndInOrderPipeline;
    NetworkPipeline nonReliableNotInOrderedPipeline;

    const ushort NetworkPort = 9002;

    const int MaxNumberOfClientConnections = 1000;

    void Start()
    {
        networkDriver = NetworkDriver.Create();
        reliableAndInOrderPipeline = networkDriver.CreatePipeline(typeof(FragmentationPipelineStage), typeof(ReliableSequencedPipelineStage));
        nonReliableNotInOrderedPipeline = networkDriver.CreatePipeline(typeof(FragmentationPipelineStage));

        // Public IP address
        //NetworkEndPoint endpoint = NetworkEndPoint.Parse("192.168.2.43", NetworkPort);

        // Home Local Network
        NetworkEndPoint endpoint = NetworkEndPoint.Parse("127.0.0.1", NetworkPort);


        int error = networkDriver.Bind(endpoint);
        if (error != 0)
            Debug.Log("Failed to bind to port " + NetworkPort);
        else
            networkDriver.Listen();

        networkConnections = new NativeList<NetworkConnection>(MaxNumberOfClientConnections, Allocator.Persistent);
    }

    void OnDestroy()
    {
        networkDriver.Dispose();
        networkConnections.Dispose();
    }

    void Update()
    {
        #region Check Input and Send Msg

        if (Input.GetKeyDown(KeyCode.A))
        {
            for (int i = 0; i < networkConnections.Length; i++)
            {
                if (networkConnections[i].IsCreated)
                {
                    SendMessageToClient("Hello client's world, sincerely your network server", networkConnections[i].InternalId);
                }
            }
        }

        #endregion

        networkDriver.ScheduleUpdate().Complete();

        #region Remove Unused Connections

        for (int i = 0; i < networkConnections.Length; i++)
        {
            if (!networkConnections[i].IsCreated)
            {
                networkConnections.RemoveAtSwapBack(i);
                i--;
            }
        }

        #endregion

        #region Accept New Connections

        while (AcceptIncomingConnection())
        {
            Debug.Log("Accepted a client connection");
        }

        #endregion

        #region Manage Network Events

        DataStreamReader streamReader;
        NetworkPipeline pipelineUsedToSendEvent;
        NetworkEvent.Type networkEventType;

        for (int i = 0; i < networkConnections.Length; i++)
        {
            if (!networkConnections[i].IsCreated)
                continue;

            while (PopNetworkEventAndCheckForData(networkConnections[i], out networkEventType, out streamReader, out pipelineUsedToSendEvent))
            {
                if (pipelineUsedToSendEvent == reliableAndInOrderPipeline)
                    Debug.Log("Network event from: reliableAndInOrderPipeline");
                else if (pipelineUsedToSendEvent == nonReliableNotInOrderedPipeline)
                    Debug.Log("Network event from: nonReliableNotInOrderedPipeline");

                switch (networkEventType)
                {
                    case NetworkEvent.Type.Data:
                        int sizeOfDataBuffer = streamReader.ReadInt();
                        NativeArray<byte> buffer = new NativeArray<byte>(sizeOfDataBuffer, Allocator.Persistent);
                        streamReader.ReadBytes(buffer);
                        byte[] byteBuffer = buffer.ToArray();
                        string msg = Encoding.Unicode.GetString(byteBuffer);

                        ProcessReceivedMsg(msg); // For logging purposes
                        ProcessClientMessage(msg, networkConnections[i]); ; // Process the client's message

                        buffer.Dispose();
                        break;

                    case NetworkEvent.Type.Disconnect:
                        Debug.Log("Client has disconnected from server");
                        networkConnections[i] = default(NetworkConnection);
                        break;
                }
            }
        }

        #endregion
    }

    private bool AcceptIncomingConnection()
    {
        NetworkConnection connection = networkDriver.Accept();
        if (connection == default(NetworkConnection))
            return false;

        networkConnections.Add(connection);
        return true;
    }

    private bool PopNetworkEventAndCheckForData(NetworkConnection networkConnection, out NetworkEvent.Type networkEventType, out DataStreamReader streamReader, out NetworkPipeline pipelineUsedToSendEvent)
    {
        networkEventType = networkConnection.PopEvent(networkDriver, out streamReader, out pipelineUsedToSendEvent);

        if (networkEventType == NetworkEvent.Type.Empty)
            return false;
        return true;
    }

    private void ProcessReceivedMsg(string msg)
    {
        Debug.Log("Msg received = " + msg);
    }

    public void SendMessageToClient(string msg, int connectionID)
    {
        foreach (var connection in networkConnections)
        {
            if (connection.InternalId == connectionID)
            {
                byte[] msgAsByteArray = Encoding.Unicode.GetBytes(msg);
                using (NativeArray<byte> buffer = new NativeArray<byte>(msgAsByteArray, Allocator.Persistent))
                {
                    DataStreamWriter streamWriter;
                    networkDriver.BeginSend(reliableAndInOrderPipeline, connection, out streamWriter);
                    streamWriter.WriteInt(buffer.Length);
                    streamWriter.WriteBytes(buffer);
                    networkDriver.EndSend(streamWriter);
                }
                break;
            }
        }
    }

    // Example method in NetworkServer to notify clients of a state change
    public void NotifyStateChangeToClients(string newState)
    {
        string stateMessage = "StateChange:" + newState;
        for (int i = 0; i < networkConnections.Length; i++)
        {
            if (networkConnections[i].IsCreated)
            {
                SendMessageToClient(stateMessage, networkConnections[i].InternalId);
            }
        }
    }

    private void ProcessClientMessage(string message, NetworkConnection connection)
    {
        Debug.Log("Processing Client Message");
        string[] messageParts = message.Split(',');

        if (messageParts.Length < 3)
        {
            // Handle error: message format is incorrect
            return;
        }

        int signifier = int.Parse(messageParts[0]);
        string username = messageParts[1];
        string password = messageParts[2];
        int connectionID = connection.InternalId; // Get the connection ID

        switch (signifier)
        {
            case 1:
                CreateAccount(username, password, connectionID);
                break;
            case 2:
                PerformLogin(username, password, connectionID);
                break;
            default:
                // Handle unknown signifier
                break;
        }

        if (message.StartsWith("StateChangedSuccessfully:"))
        {
            string changedState = message.Substring("StateChangedSuccessfully:".Length);
            // Handle the acknowledgment of state change
            // For example, updating some server-side logic
        }
    }

    private void CreateAccount(string username, string password, int connectionID)
    {
        string accountsFile = "Accounts.txt"; // Consider a more secure storage method
        bool accountExists = false;

        // Check if account already exists
        if (File.Exists(accountsFile))
        {
            using (StreamReader reader = new StreamReader(accountsFile))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string[] parts = line.Split(',');
                    if (parts[0] == username)
                    {
                        accountExists = true;
                        break;
                    }
                }
            }
        }

        // Create if does not exist
        if (!accountExists)
        {
            using (StreamWriter writer = new StreamWriter(accountsFile, true))
            {
                writer.WriteLine($"{username},{password}"); // Insecure: consider hashing the password
            }
            Debug.Log("Account created successfully.");
            SendMessageToClient("CreateAccountSuccess", connectionID);
        }
        else
        {
            Debug.Log("Account creation failed: Username already exists.");
            SendMessageToClient("CreateAccountFail", connectionID);
        }
    }
    private void PerformLogin(string username, string password, int connectionID)
    {
        string accountsFile = "Accounts.txt"; // The same file where accounts are stored
        bool loginSuccess = false;

        if (File.Exists(accountsFile))
        {
            using (StreamReader reader = new StreamReader(accountsFile))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string[] parts = line.Split(',');
                    if (parts[0] == username && parts[1] == password) // Insecure: consider hashing the password
                    {
                        loginSuccess = true;
                        break;
                    }
                }
            }
        }

        if (loginSuccess)
        {
            Debug.Log("Login successful. ID: " + connectionID);
            SendMessageToClient("StateChange:MainMenu", connectionID);
        }

        else
        {
            Debug.Log("Login failed: Username or password is incorrect.");
            SendMessageToClient("LoginFail", connectionID);
        }
    }


}
