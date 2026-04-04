using UnityEngine;
using System;

namespace EscapeED
{
    public enum GameState { Init, MainMenu, Playing, Solving, Success, Failure }

    public class GameStateManager : MonoBehaviour
    {
        public static GameStateManager Instance { get; private set; }

        public static event Action<GameState> OnStateChanged;

        private GameState currentState;
        public GameState CurrentState => currentState;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                Debug.Log("[GameStateManager] Awake - Singleton Instance Ready");
            }
            else
            {
                Destroy(gameObject);
            }
        }

        void Start()
        {
            UpdateState(GameState.Init);
        }

        public void UpdateState(GameState newState)
        {
            if (currentState == newState) return;

            currentState = newState;
            Debug.Log($"Game State Changed to: {newState}");
            
            OnStateChanged?.Invoke(newState);
            
            HandleStateUpdate(newState);
        }

        private void HandleStateUpdate(GameState state)
        {
            switch (state)
            {
                case GameState.Init:
                    // TODO: load persistent data, initialise services
                    break;
                case GameState.MainMenu:
                    // TODO: show main menu UI
                    break;
                case GameState.Playing:
                    // TODO: start level timer, enable input
                    break;
                case GameState.Solving:
                    // TODO: play solve animation, disable input
                    break;
                case GameState.Success:
                    // TODO: show success screen, unlock next level
                    break;
                case GameState.Failure:
                    // TODO: show failure screen, offer retry
                    break;
            }
        }
    }
}
