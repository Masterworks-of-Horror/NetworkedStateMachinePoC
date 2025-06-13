using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NetworkedStateMachine.Demo.Simulation
{
    using NetworkedStateMachine.Framework;

    [System.Serializable]
    public class NetworkMessage
    {
        public float Timestamp;
        public string MessageType;
        public string Content;
        public int SenderId;
        public bool IsServerMessage;
        
        public NetworkMessage(string messageType, string content, int senderId, bool isServerMessage = false)
        {
            Timestamp = Time.time;
            MessageType = messageType;
            Content = content;
            SenderId = senderId;
            IsServerMessage = isServerMessage;
        }
    }

    [System.Serializable]
    public class PendingTransition
    {
        public string FromState;
        public string ToState;
        public float StartTime;
        public float CompletionTime;
        public int RequestingPlayerId;
        public bool IsActive => Time.time < CompletionTime;
        public float Progress => Mathf.Clamp01((Time.time - StartTime) / (CompletionTime - StartTime));
    }

    // Singleton network manager to handle client/server communication
    public class NetworkManager : MonoBehaviour
    {
        public static NetworkManager Instance { get; private set; }
        
        [Header("Network Settings")]
        public float NetworkDelayMin = 0.1f;
        public float NetworkDelayMax = 0.5f;
        public bool SimulatePacketLoss = false;
        [Range(0f, 0.5f)]
        public float PacketLossRate = 0.1f;
        
        private List<NetworkMessage> _networkMessages = new List<NetworkMessage>();
        private List<PendingTransition> _pendingTransitions = new List<PendingTransition>();
        
        public List<NetworkMessage> NetworkMessages => _networkMessages;
        public List<PendingTransition> PendingTransitions => _pendingTransitions;
        
        // Events for communication
        public System.Action<string, string, int> OnServerToClientMessage;
        public System.Action<string, string, int> OnClientToServerMessage;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void SendClientToServer<T, TParams>(T currentState, T targetState, TParams parameters, 
            System.Action<T, TParams> onProcessed, System.Action<string> onNetworkEvent)
            where T : struct, System.Enum 
            where TParams : ITransitionParameters
        {
            AddNetworkMessage("CLIENT_REQUEST", $"Player {parameters.RequestingPlayerId} requests: {currentState} -> {targetState}", 
                parameters.RequestingPlayerId, false);
            
            // Simulate network delay
            float delay = UnityEngine.Random.Range(NetworkDelayMin, NetworkDelayMax);
            
            // Simulate packet loss
            if (SimulatePacketLoss && UnityEngine.Random.value < PacketLossRate)
            {
                AddNetworkMessage("PACKET_LOSS", $"Request dropped: {currentState} -> {targetState}", 
                    parameters.RequestingPlayerId, true);
                onNetworkEvent?.Invoke($"Packet lost! Transition to {targetState} dropped");
                return;
            }
            
            // Track pending transition
            var pendingTransition = new PendingTransition
            {
                FromState = currentState.ToString(),
                ToState = targetState.ToString(),
                StartTime = Time.time,
                CompletionTime = Time.time + delay,
                RequestingPlayerId = parameters.RequestingPlayerId
            };
            _pendingTransitions.Add(pendingTransition);
            
            AddNetworkMessage("NETWORK_DELAY", $"Processing {currentState} -> {targetState} (delay: {delay:F2}s)", 
                parameters.RequestingPlayerId, true);
            onNetworkEvent?.Invoke($"Network request sent: {currentState} -> {targetState} (delay: {delay:F2}s)");
            
            StartCoroutine(ProcessClientRequestAfterDelay(targetState, parameters, delay, onProcessed, onNetworkEvent));
        }
        
        private IEnumerator ProcessClientRequestAfterDelay<T, TParams>(T targetState, TParams parameters, float delay,
            System.Action<T, TParams> onProcessed, System.Action<string> onNetworkEvent)
            where T : struct, System.Enum
            where TParams : ITransitionParameters
        {
            yield return new WaitForSeconds(delay);
            
            // Server receives the request
            AddNetworkMessage("SERVER_RECEIVED", $"Server processing transition to {targetState}", 
                parameters.RequestingPlayerId, true);
            onNetworkEvent?.Invoke($"Server processing transition to {targetState}");
            
            // Clean up pending transitions
            _pendingTransitions.RemoveAll(t => !t.IsActive);
            
            // Notify server to process
            OnClientToServerMessage?.Invoke(targetState.ToString(), parameters.GetType().Name, parameters.RequestingPlayerId);
            
            onProcessed(targetState, parameters);
        }
        
        public void SendServerToClient(string fromState, string toState, int playerId)
        {
            float delay = UnityEngine.Random.Range(NetworkDelayMin, NetworkDelayMax);
            
            AddNetworkMessage("SERVER_BROADCAST", $"Server broadcasting: {fromState} -> {toState}", 0, true);
            
            if (SimulatePacketLoss && UnityEngine.Random.value < PacketLossRate)
            {
                AddNetworkMessage("PACKET_LOSS", $"Server broadcast dropped: {fromState} -> {toState}", 0, true);
                return;
            }
            
            StartCoroutine(SendServerUpdateAfterDelay(fromState, toState, playerId, delay));
        }
        
        private IEnumerator SendServerUpdateAfterDelay(string fromState, string toState, int playerId, float delay)
        {
            yield return new WaitForSeconds(delay);
            
            AddNetworkMessage("CLIENT_RECEIVED", $"Client received: {fromState} -> {toState}", playerId, false);
            OnServerToClientMessage?.Invoke(fromState, toState, playerId);
        }
        
        private void AddNetworkMessage(string messageType, string content, int senderId, bool isServerMessage)
        {
            _networkMessages.Add(new NetworkMessage(messageType, content, senderId, isServerMessage));
            
            // Keep only last 20 messages
            if (_networkMessages.Count > 20)
            {
                _networkMessages.RemoveAt(0);
            }
        }
        
        public void ClearMessages()
        {
            _networkMessages.Clear();
            _pendingTransitions.Clear();
        }
    }

    // Client-side network simulator
    public class ClientNetworkSimulator : INetworkSimulator
    {
        public void ProcessTransition<T, TParams>(T currentState, T targetState, TParams parameters,
            System.Action<T, TParams> onProcessed, System.Action<string> onNetworkEvent)
            where T : struct, System.Enum
            where TParams : ITransitionParameters
        {
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.SendClientToServer(currentState, targetState, parameters, onProcessed, onNetworkEvent);
            }
            else
            {
                // Fallback to direct processing
                onProcessed(targetState, parameters);
            }
        }
    }
}