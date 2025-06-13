namespace NetworkedStateMachine.Demo
{
    using NetworkedStateMachine.Framework;

    public enum TrafficLightState
    {
        Red,
        Yellow,
        Green
    }

    public class TrafficLightContext
    {
        public float RedDuration = 5f;
        public float YellowDuration = 2f;
        public float GreenDuration = 8f;
        public float CurrentStateTime = 0f;
        public bool EmergencyMode = false;
        public bool ManualControl = false;
        public int TrafficCount = 0;
        
        public void ResetTimer() => CurrentStateTime = 0f;
        public void UpdateTimer(float deltaTime) => CurrentStateTime += deltaTime;
    }

    // Emergency override parameters
    public struct EmergencyParameters : ITransitionParameters
    {
        public int RequestingPlayerId { get; set; }
        public bool IsEmergencyVehicle { get; set; }
        public string EmergencyType { get; set; }
    }

    // Manual control parameters
    public struct ManualControlParameters : ITransitionParameters
    {
        public int RequestingPlayerId { get; set; }
        public bool ForceOverride { get; set; }
        public string Reason { get; set; }
    }

    // State Implementations
    public class RedLightState : NetworkedState<TrafficLightState, TrafficLightContext>
    {
        public override TrafficLightState StateType => TrafficLightState.Red;
        
        public override void OnEnter(TrafficLightContext context)
        {
            context.ResetTimer();
        }
        
        public override void OnUpdate(TrafficLightContext context, float deltaTime)
        {
            context.UpdateTimer(deltaTime);
        }
        
        public override bool CanTransitionTo(TrafficLightState targetState, TrafficLightContext context, ITransitionParameters parameters)
        {
            // Emergency override
            if (parameters is EmergencyParameters emergency && emergency.IsEmergencyVehicle)
                return targetState == TrafficLightState.Green;
            
            // Manual override
            if (parameters is ManualControlParameters manual && manual.ForceOverride)
                return true;
            
            // Normal timing
            if (targetState == TrafficLightState.Green)
                return context.CurrentStateTime >= context.RedDuration;
                
            return false;
        }
    }

    public class YellowLightState : NetworkedState<TrafficLightState, TrafficLightContext>
    {
        public override TrafficLightState StateType => TrafficLightState.Yellow;
        
        public override void OnEnter(TrafficLightContext context)
        {
            context.ResetTimer();
        }
        
        public override void OnUpdate(TrafficLightContext context, float deltaTime)
        {
            context.UpdateTimer(deltaTime);
        }
        
        public override bool CanTransitionTo(TrafficLightState targetState, TrafficLightContext context, ITransitionParameters parameters)
        {
            // Yellow always goes to red after timeout
            return targetState == TrafficLightState.Red && 
                   context.CurrentStateTime >= context.YellowDuration;
        }
    }

    public class GreenLightState : NetworkedState<TrafficLightState, TrafficLightContext>
    {
        public override TrafficLightState StateType => TrafficLightState.Green;
        
        public override void OnEnter(TrafficLightContext context)
        {
            context.ResetTimer();
        }
        
        public override void OnUpdate(TrafficLightContext context, float deltaTime)
        {
            context.UpdateTimer(deltaTime);
        }
        
        public override bool CanTransitionTo(TrafficLightState targetState, TrafficLightContext context, ITransitionParameters parameters)
        {
            // Emergency can force back to red
            if (parameters is EmergencyParameters emergency && emergency.IsEmergencyVehicle)
                return targetState == TrafficLightState.Red;
            
            // Normal progression to yellow
            if (targetState == TrafficLightState.Yellow)
                return context.CurrentStateTime >= context.GreenDuration;
                
            return false;
        }
    }
}