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
                    break;
                case GameState.MainMenu:
                    break;
                case GameState.Playing:
                    break;
                case GameState.Solving:
                    break;
                case GameState.Success:
                    break;
                case GameState.Failure:
                    break;
            }
        }
    }
}
