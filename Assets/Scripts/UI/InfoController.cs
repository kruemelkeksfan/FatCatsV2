﻿using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InfoController : MonoBehaviour
{
	private static InfoController instance = null;

	[SerializeField] private TMP_Text messageLog = null;
	[SerializeField] private AudioClip warningAudio = null;
	[SerializeField] private Transform confirmationPanel = null;
	[SerializeField] private TMP_Text confirmationPanelText = null;
	[SerializeField] private Button confirmationPanelConfirmButton = null;
	[SerializeField] private AudioController audioController = null;
	[SerializeField] private int maxMessageCount = 10;
	private LinkedList<string> messages = null;
	private StringBuilder textBuilder = null;
	private TimeController timeController = null;

	public static InfoController GetInstance()
	{
		return instance;
	}

	private void Awake()
	{
		Thread.CurrentThread.CurrentCulture = CultureInfo.CreateSpecificCulture("en-GB"); // Decimal Points FTW!!elf!

		messages = new LinkedList<string>();
		textBuilder = new StringBuilder();

		instance = this;
	}

	private void Start()
	{
		timeController = TimeController.GetInstance();
	}

	public void ActivateConfirmationPanel(string text, UnityEngine.Events.UnityAction confirmationAction)
	{
		confirmationPanelText.text = text;

		confirmationPanelConfirmButton.onClick.RemoveAllListeners();
		confirmationPanelConfirmButton.onClick.AddListener(confirmationAction);
		confirmationPanelConfirmButton.onClick.AddListener(delegate
		{
			DeactivateConfirmationPanel();
		});

		confirmationPanel.gameObject.SetActive(true);
		confirmationPanel.SetAsLastSibling();
	}

	public void DeactivateConfirmationPanel()
	{
		confirmationPanel.gameObject.SetActive(false);
	}

	public void AddMessage(string newMessage, bool warning, bool pause)
	{
		if(warning)
		{
			messages.AddLast("<color=yellow>[" + timeController.BuildTimeString() + "]\n" + newMessage + "</color>\n");

			// TODO: Add AudioController and SFX
			// audioController.PlayAudio(warningAudio, null);
		}
		else
		{
			messages.AddLast("[" + timeController.BuildTimeString() + "]\n" + newMessage + "\n");
		}

		while(messages.Count > maxMessageCount)
		{
			messages.RemoveFirst();
		}

		textBuilder.Clear();
		LinkedListNode<string> currentMessage = messages.Last;
		bool first = true;
		while(currentMessage != null)
		{
			if(first)
			{
				textBuilder.AppendLine("<color=white>" + currentMessage.Value + "</color=white>");
				first = false;
			}
			else
			{
				textBuilder.AppendLine(currentMessage.Value);
			}
			currentMessage = currentMessage.Previous;
		}
		messageLog.text = textBuilder.ToString();

		if(pause)
		{
			timeController.SetTimeScale(0);
		}
	}
}
