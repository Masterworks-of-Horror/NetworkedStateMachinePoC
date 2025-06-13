using NetworkedStateMachine.Framework;
using NetworkedStateMachine.Demo;
using NetworkedStateMachine.Demo.Simulation;
using UnityEngine;

public class TrafficLightServer : MonoBehaviour
{
    [Header("Server Configuration")]
    private NetworkedStateMachine<TrafficLightState, TrafficLightContext> _stateMachine;
    private TrafficLightContext _context;
    
    [Header("Server Visualization")]
    public GameObject RedLight;
    public GameObject YellowLight;
    public GameObject GreenLight;
    
    [Header("Traffic Light Settings")]
    public float RedDuration = 3f;
    public float YellowDuration = 1f;
    public float GreenDuration = 4f;
    
    [Header("Server State Info")]
    public TrafficLightState CurrentState;
    public float StateTimer;
    public bool EmergencyMode;
    public string LastEvent;
    
    [Header("Server Controls")]
    [Space]
    public bool TriggerEmergency;
    public bool ManualOverride;
    public TrafficLightState ManualTargetState = TrafficLightState.Yellow;

    void Start()
    {
        CreateServerVisuals();
        InitializeServer();
        SubscribeToNetworkEvents();
    }
    
    void CreateServerVisuals()
    {
        Vector3 basePos = transform.position;
        
        if (RedLight == null)
        {
            RedLight = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            RedLight.transform.position = basePos + Vector3.up * 1.5f;
            RedLight.transform.localScale = Vector3.one * 1.2f;
            RedLight.name = "Server Red Light";
            
            // Add a label
            var label = new GameObject("Server Label");
            label.transform.position = basePos + Vector3.up * 3f;
            var textMesh = label.AddComponent<TextMesh>();
            textMesh.text = "SERVER";
            textMesh.fontSize = 20;
            textMesh.color = Color.white;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            label.transform.SetParent(transform);
        }
        
        if (YellowLight == null)
        {
            YellowLight = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            YellowLight.transform.position = basePos;
            YellowLight.transform.localScale = Vector3.one * 1.2f;
            YellowLight.name = "Server Yellow Light";
        }
        
        if (GreenLight == null)
        {
            GreenLight = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            GreenLight.transform.position = basePos + Vector3.down * 1.5f;
            GreenLight.transform.localScale = Vector3.one * 1.2f;
            GreenLight.name = "Server Green Light";
        }
    }
    
    void InitializeServer()
    {
        _context = new TrafficLightContext
        {
            RedDuration = RedDuration,
            YellowDuration = YellowDuration,
            GreenDuration = GreenDuration
        };
        
        // Server uses direct networking (authoritative)
        _stateMachine = new NetworkedStateMachine<TrafficLightState, TrafficLightContext>(
            TrafficLightState.Red, _context, new DirectNetworkHandler());
        
        _stateMachine.OnStateChanged += OnServerStateChanged;
        _stateMachine.OnNetworkEvent += OnServerNetworkEvent;
    }
    
    void SubscribeToNetworkEvents()
    {
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnClientToServerMessage += HandleClientRequest;
        }
    }
    
    void HandleClientRequest(string targetState, string parameterType, int playerId)
    {
        Debug.Log($"[SERVER] Received client request from Player {playerId}: -> {targetState}");
        
        // Server validates and processes the request
        if (System.Enum.TryParse<TrafficLightState>(targetState, out var state))
        {
            // For demo purposes, server accepts most requests
            // In real implementation, this would have proper validation
            ProcessClientTransitionRequest(state, playerId, parameterType);
        }
    }
    
    void ProcessClientTransitionRequest(TrafficLightState targetState, int playerId, string parameterType)
    {
        // Create appropriate parameters based on type
        if (parameterType.Contains("Emergency"))
        {
            var emergencyParams = new EmergencyParameters
            {
                IsEmergencyVehicle = true,
                EmergencyType = "Remote Emergency"
            };
            _stateMachine.TryTransition(targetState, playerId, emergencyParams);
        }
        else if (parameterType.Contains("Manual"))
        {
            var manualParams = new ManualControlParameters
            {
                ForceOverride = true,
                Reason = "Remote Manual Control"
            };
            _stateMachine.TryTransition(targetState, playerId, manualParams);
        }
        else
        {
            _stateMachine.TryTransition(targetState, playerId);
        }
    }

    void Update()
    {
        UpdateSettings();
        HandleServerControls();
        
        _stateMachine.Update(Time.deltaTime);
        HandleAutoTransitions();
        
        UpdateInspectorInfo();
        UpdateVisuals();
    }
    
    void UpdateSettings()
    {
        _context.RedDuration = RedDuration;
        _context.YellowDuration = YellowDuration;
        _context.GreenDuration = GreenDuration;
    }
    
    void HandleServerControls()
    {
        if (TriggerEmergency)
        {
            TriggerEmergency = false;
            var emergencyParams = new EmergencyParameters
            {
                IsEmergencyVehicle = true,
                EmergencyType = "Server Emergency"
            };
            _stateMachine.TryTransition(TrafficLightState.Green, 0, emergencyParams);
            _context.EmergencyMode = true;
        }
        
        if (ManualOverride)
        {
            ManualOverride = false;
            var manualParams = new ManualControlParameters
            {
                ForceOverride = true,
                Reason = "Server Manual Control"
            };
            _stateMachine.TryTransition(ManualTargetState, 0, manualParams);
        }
    }
    
    void HandleAutoTransitions()
    {
        var currentState = _stateMachine.CurrentState;
        
        switch (currentState)
        {
            case TrafficLightState.Red:
                if (_context.CurrentStateTime >= _context.RedDuration)
                    _stateMachine.TryTransition(TrafficLightState.Green, 0);
                break;
                
            case TrafficLightState.Green:
                if (_context.CurrentStateTime >= _context.GreenDuration)
                    _stateMachine.TryTransition(TrafficLightState.Yellow, 0);
                break;
                
            case TrafficLightState.Yellow:
                if (_context.CurrentStateTime >= _context.YellowDuration)
                    _stateMachine.TryTransition(TrafficLightState.Red, 0);
                break;
        }
    }
    
    void UpdateInspectorInfo()
    {
        CurrentState = _stateMachine.CurrentState;
        StateTimer = _context.CurrentStateTime;
        EmergencyMode = _context.EmergencyMode;
    }
    
    void OnServerStateChanged(TrafficLightState from, TrafficLightState to)
    {
        Debug.Log($"[SERVER] State changed: {from} -> {to}");
        
        // Broadcast to clients
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.SendServerToClient(from.ToString(), to.ToString(), 0);
        }
        
        if (to == TrafficLightState.Red && _context.EmergencyMode)
        {
            _context.EmergencyMode = false;
        }
    }
    
    void OnServerNetworkEvent(string eventMessage)
    {
        LastEvent = eventMessage;
        Debug.Log($"[SERVER] {eventMessage}");
    }
    
    void UpdateVisuals()
    {
        SetLightColor(RedLight, Color.gray);
        SetLightColor(YellowLight, Color.gray);
        SetLightColor(GreenLight, Color.gray);
        
        switch (_stateMachine.CurrentState)
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
        
        if (EmergencyMode && _stateMachine.CurrentState == TrafficLightState.Green)
        {
            float flash = Mathf.Sin(Time.time * 8f) * 0.5f + 0.5f;
            SetLightColor(GreenLight, Color.Lerp(Color.green, Color.white, flash));
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
}