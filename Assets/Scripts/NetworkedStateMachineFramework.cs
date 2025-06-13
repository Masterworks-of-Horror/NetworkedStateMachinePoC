using System;
using System.Collections.Generic;
using System.Reflection;

namespace NetworkedStateMachine.Framework
{
    public interface ITransitionParameters 
    {
        int RequestingPlayerId { get; set; }
    }

    public struct NoParameters : ITransitionParameters
    {
        public int RequestingPlayerId { get; set; }
    }

    public interface IState<T, TContext> where T : struct, System.Enum
    {
        T StateType { get; }
        void OnEnter(TContext context);
        void OnUpdate(TContext context, float deltaTime);
        void OnExit(TContext context);
        bool CanTransitionTo(T targetState, TContext context, ITransitionParameters parameters);
    }

    public abstract class NetworkedState<T, TContext> : IState<T, TContext> where T : struct, System.Enum
    {
        public abstract T StateType { get; }
        public virtual void OnEnter(TContext context) { }
        public virtual void OnUpdate(TContext context, float deltaTime) { }
        public virtual void OnExit(TContext context) { }
        public abstract bool CanTransitionTo(T targetState, TContext context, ITransitionParameters parameters);
    }

    public interface INetworkSimulator
    {
        void ProcessTransition<T, TParams>(T currentState, T targetState, TParams parameters, 
            System.Action<T, TParams> onProcessed, System.Action<string> onNetworkEvent) 
            where T : struct, System.Enum 
            where TParams : ITransitionParameters;
    }

    public class DirectNetworkHandler : INetworkSimulator
    {
        public void ProcessTransition<T, TParams>(T currentState, T targetState, TParams parameters, 
            System.Action<T, TParams> onProcessed, System.Action<string> onNetworkEvent) 
            where T : struct, System.Enum 
            where TParams : ITransitionParameters
        {
            onNetworkEvent?.Invoke($"Direct transition: {currentState} -> {targetState}");
            onProcessed(targetState, parameters);
        }
    }

    public static class StateFactory<T, TContext> where T : struct, System.Enum
    {
        private static readonly Dictionary<T, IState<T, TContext>> _stateInstances;
        
        static StateFactory()
        {
            _stateInstances = new Dictionary<T, IState<T, TContext>>();
            
            var stateType = typeof(IState<T, TContext>);
            var assembly = Assembly.GetExecutingAssembly();
            
            foreach (var type in assembly.GetTypes())
            {
                if (stateType.IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                {
                    var instance = (IState<T, TContext>)Activator.CreateInstance(type);
                    _stateInstances[instance.StateType] = instance;
                }
            }
            
            // Validate all enum values have implementations
            foreach (T enumValue in System.Enum.GetValues(typeof(T)))
            {
                if (!_stateInstances.ContainsKey(enumValue))
                {
                    throw new System.InvalidOperationException(
                        $"No state implementation found for {enumValue}");
                }
            }
        }
        
        public static IState<T, TContext> GetState(T stateType) => _stateInstances[stateType];
    }

    // Main State Machine 
    public class NetworkedStateMachine<T, TContext> where T : struct, System.Enum
    {
        private T _currentState;
        private TContext _context;
        private INetworkSimulator _networkSimulator;
        
        public T CurrentState => _currentState;
        public event System.Action<T, T> OnStateChanged;
        public event System.Action<string> OnNetworkEvent;

        public NetworkedStateMachine(T initialState, TContext context, INetworkSimulator networkSimulator = null)
        {
            _currentState = initialState;
            _context = context;
            _networkSimulator = networkSimulator ?? new DirectNetworkHandler();
            
            // Validate all states exist at startup
            _ = StateFactory<T, TContext>.GetState(initialState);
        }

        // Transition with no parameters
        public void TryTransition(T targetState, int requestingPlayerId)
        {
            var parameters = new NoParameters { RequestingPlayerId = requestingPlayerId };
            ProcessTransitionRequest(targetState, parameters);
        }
        
        // Transition with typed parameters
        public void TryTransition<TParams>(T targetState, int requestingPlayerId, TParams parameters) 
            where TParams : ITransitionParameters
        {
            parameters.RequestingPlayerId = requestingPlayerId;
            ProcessTransitionRequest(targetState, parameters);
        }
        
        private void ProcessTransitionRequest<TParams>(T targetState, TParams parameters)
            where TParams : ITransitionParameters
        {
            _networkSimulator.ProcessTransition(_currentState, targetState, parameters, 
                ValidateAndExecuteTransition, OnNetworkEvent);
        }
        
        private void ValidateAndExecuteTransition<TParams>(T targetState, TParams parameters)
            where TParams : ITransitionParameters
        {
            var currentStateObj = StateFactory<T, TContext>.GetState(_currentState);
            if (!currentStateObj.CanTransitionTo(targetState, _context, parameters))
            {
                OnNetworkEvent?.Invoke($"Transition rejected: {_currentState} -> {targetState}");
                return;
            }
                
            ExecuteTransition(targetState);
        }
        
        private void ExecuteTransition(T targetState)
        {
            StateFactory<T, TContext>.GetState(_currentState).OnExit(_context);
            var previousState = _currentState;
            _currentState = targetState;
            StateFactory<T, TContext>.GetState(_currentState).OnEnter(_context);
            
            BroadcastStateChange(previousState, _currentState);
            OnStateChanged?.Invoke(previousState, _currentState);
        }
        
        // Update current state
        public void Update(float deltaTime)
        {
            StateFactory<T, TContext>.GetState(_currentState).OnUpdate(_context, deltaTime);
        }
        
        private void BroadcastStateChange(T fromState, T toState)
        {
            OnNetworkEvent?.Invoke($"State changed: {fromState} -> {toState}");
        }
    }
}