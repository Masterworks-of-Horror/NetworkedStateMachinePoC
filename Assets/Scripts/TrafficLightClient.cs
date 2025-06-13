using System.Linq;
using UnityEngine;
using NetworkedStateMachine.Framework;
using NetworkedStateMachine.Demo;
using NetworkedStateMachine.Demo.Simulation;


public class TrafficLightClient : MonoBehaviour
{
    [Header("Client Configuration")]
    private NetworkedStateMachine<TrafficLightState, TrafficLightContext> _requestStateMachine; // Only for requests
    private TrafficLightContext _requestContext;
    private ClientNetworkSimulator _networkSimulator;
    
    // Simple event-based display state (no state machine needed!)
    private TrafficLightState _currentDisplayState = TrafficLightState.Red;
    private TrafficLightContext _displayContext;
    private float _displayStateTimer = 0f;
    
    [Header("Client Visualization")]
    public GameObject RedLight;
    public GameObject YellowLight;
    public GameObject GreenLight;
    
    [Header("Client Info")]
    public int PlayerId = 1;
    public TrafficLightState CurrentState; // What client displays
    public TrafficLightState ServerState;  // Last known server state
    public float StateTimer;
    public bool EmergencyMode;
    public string LastEvent;
    public int PendingRequests;
    public bool IsConnectedToServer;
    
    [Header("Client Controls")]
    [Space]
    public bool RequestEmergency;
    public bool RequestManualOverride;
    public TrafficLightState RequestedState = TrafficLightState.Yellow;

    void Start()
    {
        Debug.Log($"[CLIENT {PlayerId}] Starting client...");
        CreateClientVisuals();
        InitializeClient();
        SubscribeToNetworkEvents();
        Debug.Log($"[CLIENT {PlayerId}] Client ready!");
    }
    
    void CreateClientVisuals()
    {
        Vector3 basePos = transform.position;
        
        if (RedLight == null)
        {
            RedLight = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            RedLight.transform.position = basePos + Vector3.up * 1.5f;
            RedLight.transform.localScale = Vector3.one * 0.8f;
            RedLight.name = "Client Red Light";
            
            // Add a label
            var label = new GameObject("Client Label");
            label.transform.position = basePos + Vector3.up * 3f;
            var textMesh = label.AddComponent<TextMesh>();
            textMesh.text = $"CLIENT {PlayerId}";
            textMesh.fontSize = 16;
            textMesh.color = Color.cyan;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            label.transform.SetParent(transform);
        }
        
        if (YellowLight == null)
        {
            YellowLight = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            YellowLight.transform.position = basePos;
            YellowLight.transform.localScale = Vector3.one * 0.8f;
            YellowLight.name = "Client Yellow Light";
        }
        
        if (GreenLight == null)
        {
            GreenLight = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            GreenLight.transform.position = basePos + Vector3.down * 1.5f;
            GreenLight.transform.localScale = Vector3.one * 0.8f;
            GreenLight.name = "Client Green Light";
        }
    }
    
    void InitializeClient()
    {
        // Request context - for sending requests
        _requestContext = new TrafficLightContext();
        _networkSimulator = new ClientNetworkSimulator();
        
        // Display context - simple context for display state
        _displayContext = new TrafficLightContext();
        
        // Request state machine - ONLY for sending requests to server
        _requestStateMachine = new NetworkedStateMachine<TrafficLightState, TrafficLightContext>(
            TrafficLightState.Red, _requestContext, _networkSimulator);
        
        // No need for complex state machine events - requests are just requests
        _requestStateMachine.OnNetworkEvent += OnRequestNetworkEvent;
        
        IsConnectedToServer = NetworkManager.Instance != null;
    }
    
    void SubscribeToNetworkEvents()
    {
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnServerToClientMessage += HandleServerUpdate;
            Debug.Log($"[CLIENT {PlayerId}] Subscribed to server updates");
        }
        else
        {
            Debug.LogError($"[CLIENT {PlayerId}] NetworkManager not found!");
        }
    }
    
    void HandleServerUpdate(string fromState, string toState, int playerId)
    {
        Debug.Log($"[CLIENT {PlayerId}] Server update: {fromState} -> {toState}");
        
        if (System.Enum.TryParse<TrafficLightState>(toState, out var newState))
        {
            ServerState = newState;
            SetDisplayState(newState);
        }
    }
    
    void SetDisplayState(TrafficLightState newState)
    {
        if (_currentDisplayState != newState)
        {
            var oldState = _currentDisplayState;
            _currentDisplayState = newState;
            _displayStateTimer = 0f; // Reset timer
            
            // Update context based on state
            _displayContext.ResetTimer();
            
            // Handle emergency mode
            if (newState == TrafficLightState.Green && oldState == TrafficLightState.Red)
            {
                _displayContext.EmergencyMode = true;
                Debug.Log($"[CLIENT {PlayerId}] Emergency mode activated");
            }
            else if (newState == TrafficLightState.Red && _displayContext.EmergencyMode)
            {
                _displayContext.EmergencyMode = false;
                Debug.Log($"[CLIENT {PlayerId}] Emergency mode deactivated");
            }
            
            Debug.Log($"[CLIENT {PlayerId}] Display state: {oldState} -> {newState}");
        }
    }

    void Update()
    {
        CheckServerConnection();
        HandleClientControls();
        
        // Update request state machine (for tracking pending requests)
        _requestStateMachine.Update(Time.deltaTime);
        
        // Update display timer (simple!)
        _displayStateTimer += Time.deltaTime;
        _displayContext.UpdateTimer(Time.deltaTime);
        
        UpdateInspectorInfo();
        UpdateVisuals();
    }
    
    void CheckServerConnection()
    {
        IsConnectedToServer = NetworkManager.Instance != null;
    }
    
    void HandleClientControls()
    {
        if (!IsConnectedToServer)
        {
            Debug.LogWarning($"[CLIENT {PlayerId}] No NetworkManager - cannot send requests");
            return;
        }
        
        if (RequestEmergency)
        {
            RequestEmergency = false;
            Debug.Log($"[CLIENT {PlayerId}] Requesting emergency override");
            
            var emergencyParams = new EmergencyParameters
            {
                IsEmergencyVehicle = true,
                EmergencyType = "Client Emergency Request"
            };
            
            // Send request - this goes through network simulation
            _requestStateMachine.TryTransition(TrafficLightState.Green, PlayerId, emergencyParams);
        }
        
        if (RequestManualOverride)
        {
            RequestManualOverride = false;
            Debug.Log($"[CLIENT {PlayerId}] Requesting manual override to {RequestedState}");
            
            var manualParams = new ManualControlParameters
            {
                ForceOverride = true,
                Reason = "Client Manual Request"
            };
            
            // Send request - this goes through network simulation  
            _requestStateMachine.TryTransition(RequestedState, PlayerId, manualParams);
        }
    }
    
    void UpdateInspectorInfo()
    {
        // SIMPLE: Just copy the display state values
        CurrentState = _currentDisplayState;
        StateTimer = _displayStateTimer;
        EmergencyMode = _displayContext.EmergencyMode;
        
        if (NetworkManager.Instance != null)
        {
            PendingRequests = NetworkManager.Instance.PendingTransitions
                .Where(t => t.IsActive && t.RequestingPlayerId == PlayerId)
                .Count();
        }
    }
    
    void OnRequestNetworkEvent(string eventMessage)
    {
        LastEvent = eventMessage;
        Debug.Log($"[CLIENT {PlayerId}] Request: {eventMessage}");
    }
    
    void UpdateVisuals()
    {
        // SIMPLE: Update visuals based on display state
        SetLightColor(RedLight, Color.gray);
        SetLightColor(YellowLight, Color.gray);
        SetLightColor(GreenLight, Color.gray);
        
        switch (_currentDisplayState)
        {
            case TrafficLightState.Red:
                SetLightColor(RedLight, Color.red);
                break;
            case TrafficLightState.Yellow:
                SetLightColor(YellowLight, Color.yellow);
                break;
            case TrafficLightState.Green:
                SetLightColor(GreenLight, Color.green);
                break;
        }
        
        // Show pending requests with pulsing
        if (PendingRequests > 0)
        {
            float pulse = Mathf.Sin(Time.time * 8f) * 0.3f + 0.7f;
            var lights = new[] { RedLight, YellowLight, GreenLight };
            foreach (var light in lights)
            {
                var renderer = light.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var color = renderer.material.color;
                    renderer.material.color = new Color(color.r, color.g, color.b, pulse);
                }
            }
        }
        
        // Emergency flashing
        if (EmergencyMode && _currentDisplayState == TrafficLightState.Green)
        {
            float flash = Mathf.Sin(Time.time * 8f) * 0.5f + 0.5f;
            SetLightColor(GreenLight, Color.Lerp(Color.green, Color.cyan, flash));
        }
        
        // Connection status
        if (!IsConnectedToServer)
        {
            float disconnectedFlash = Mathf.Sin(Time.time * 4f) * 0.5f + 0.5f;
            var allLights = new[] { RedLight, YellowLight, GreenLight };
            foreach (var light in allLights)
            {
                var renderer = light.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = Color.Lerp(renderer.material.color, Color.red, disconnectedFlash * 0.3f);
                }
            }
        }
    }
    
    void SetLightColor(GameObject lightObj, Color color)
    {
        if (lightObj != null)
        {
            var renderer = lightObj.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = color;
            }
        }
    }
    
    void OnDrawGizmos()
    {
        if (Application.isPlaying)
        {
#if UNITY_EDITOR
            Vector3 textPos = transform.position + Vector3.up * 4.5f;
            
            UnityEditor.Handles.color = IsConnectedToServer ? Color.green : Color.red;
            UnityEditor.Handles.Label(textPos,
                $"CLIENT {PlayerId}\n" +
                $"Display: {_currentDisplayState}\n" +
                $"Server: {ServerState}\n" +
                $"Pending: {PendingRequests}\n" +
                $"Connected: {IsConnectedToServer}");
#endif
        }
    }
}