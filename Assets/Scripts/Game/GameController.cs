using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameController : MonoBehaviour
{
	private static GameController instance = null;
	private static string deathMessage = null;

	private TimeController timeController = null;
	private bool killScene = false;
	private bool sceneDead = false;

	public static GameController GetInstance()
	{
		return instance;
	}

	private void Awake()
	{
		instance = this;
	}

	private void Start()
	{
		timeController = TimeController.GetInstance();
	}

	private void Update()
	{
		if(killScene && !sceneDead)
		{
			sceneDead = true;
			SceneManager.sceneLoaded += DisplayDeathMessage;
			SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex, LoadSceneMode.Single);
		}
	}

	private void DisplayDeathMessage(Scene scene, LoadSceneMode mode)
	{
		if(deathMessage != null)
		{
			InfoController.GetInstance().AddMessage(deathMessage, false, true);
			deathMessage = null;
		}
	}

	public void RestartConfirmation()
	{
		InfoController.GetInstance().ActivateConfirmationPanel("Do you want to restart the Game?", delegate
		{
			Restart();
		});
	}

	public void Restart(string message = null)
	{
		if(!string.IsNullOrEmpty(message))
		{
			deathMessage = message;
		}

		killScene = true;
	}

	public void Quit()
	{
		InfoController.GetInstance().ActivateConfirmationPanel("Do you want to quit the Game?", delegate
		{
			Application.Quit();
		});
	}
}
