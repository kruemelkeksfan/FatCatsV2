using System;
using System.Collections;
using UnityEngine;

public class AIPlayerController : MonoBehaviour, IPlayerController
{
    [SerializeField] private float commandsPerMinute = 6.0f;
    private WaitForSeconds aiUpdateInterval = null;
    private bool alive = true;
    private Tuple<string, string, int> state = null;

    private void Start()
    {
        aiUpdateInterval = new WaitForSeconds(60.0f / commandsPerMinute);
    }

    private IEnumerator UpdateAi()
    {
        // Shuffle Execution Times of different AI Players so that they do not update all at once
        yield return new WaitForSeconds(UnityEngine.Random.Range(0.0f, (60.0f / commandsPerMinute)));

        while(alive)
        {
            if(state.Item1 == "Idle")
            {
                // Find next Building Project

                // Calculate missing Resources
                    // Search Tile
                    // Go to Tile

                    // Start Construction

                // Set State
            }
            else if(state.Item1 == "Collect")
            {
                // Check Location
                    // Start Collection

                    // Continue travelling to Tile
               
                // Check Good Amount in Inventory
                    // Return to Town

                    // Continue Collection
            }
            else if(state.Item1 == "Return")
            {
                // Check Location
                    // Set State to "Idle"

                    // Continue travelling to Tile
            }

            // Hire Workers
            // Sell Surplus
            

            yield return aiUpdateInterval;
        }
    }

    public void Kill()
    {
        alive = false;
    }
}
