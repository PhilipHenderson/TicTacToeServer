using UnityEngine;

public class GameStateManager : MonoBehaviour
{
    public enum GameState
    {
        Login,
        MainMenu,
        Lobby,
        InGame,
        Win,
        Lose
    }

    private GameState currentState;

    void Start()
    {
        // Initialize the game state, for example, start with the Login state
        currentState = GameState.Login;
        Debug.Log("State: " + currentState);
    }

    public void SetState(GameState newState) {
        currentState = newState;
        HandleStateChange();
    }

    public GameState GetState() { return currentState; }

    private void HandleStateChange()
    {
        Debug.Log("State: " + currentState);

        switch (currentState)
        {
            case GameState.Login:
                // Handle login state
                // Display login state UI
                break;
            case GameState.InGame:
                // Handle in-game state
                // Display Game state UI
                break;
            case GameState.Win:
                // Handle win state
                // Display Win state UI
                break;
            case GameState.Lose:
                // Handle lose state
                // Display Lose state UI
                break;
        }
    }

    // Example method to call when login is successful
    public void OnLoginSuccess()
    {
        SetState(GameState.InGame);
    }

    // Example method to call when login fails
    public void OnLoginFail()
    {
        // You can add logic here if you need to handle failed login specifically
        Debug.Log("Login Failed");
    }

    // Call this method when you want to notify clients about a state change
    public void NotifyClientsOfStateChange()
    {
        string stateMessage = "StateChange:" + currentState.ToString();
        // Logic to send this message to all clients
        // You'll need to access the network driver and connections to send messages
    }
}



/*
 * ToDo:
 * 
 *  GAMESTATE
 *      1. Able to send the client the game state changes
 *          a. via debug.log
 *                  - the client should see the state change in the debug.log
 *          b. actually change the state for the client
 *                  - change client UI(done on client side)
 *                  - send back a "Success" or "Fail" msg on "Change of State" back to the server side
 *                      1.via debug.log
 *                      2.via stream reader/writer
 *          c. Check if gamestate change was successfull
 *                  - check to make sure the server side recieved a successfull or failed gamestate change

 */