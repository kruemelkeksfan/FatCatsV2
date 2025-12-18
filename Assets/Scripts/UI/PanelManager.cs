using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PanelManager : MonoBehaviour
{
	public enum PanelObjectType { Tile, Town, ResourceInfo, Inventory, Skills, Menu, Market, Buildings, Construction };

	private static PanelManager instance = null;

	[SerializeField] private RectTransform[] panelPrefabs = { };
	[SerializeField] private RectTransform panelParent = null;
	[SerializeField] private int maxTiledPanelCount = 8;
	[SerializeField] private int maxPanelCount = 24;
	[SerializeField] private RectTransform characterPanel = null;
	[SerializeField] private float panelHeaderBlinkInterval = 0.1f;
	private Dictionary<PanelObjectType, RectTransform> panelPrefabDictionary = null;
	private new Camera camera = null;
	private EventSystem eventSystem = null;
	private RectTransform[] openPanels = null;
	private PanelObject[] openPanelObjects = null;
	private HashSet<int> queuedPanelUpdates = null;
	private WaitForSecondsRealtime waitForPanelHeaderBlink = null;

	public static PanelManager GetInstance()
	{
		return instance;
	}

	private void Awake()
	{
		panelPrefabDictionary = new Dictionary<PanelObjectType, RectTransform>(panelPrefabs.Length);
		PanelObjectType[] panelObjectTypes = (PanelObjectType[])Enum.GetValues(typeof(PanelObjectType));
		for(int i = 0; i < panelPrefabs.Length; ++i)
		{
			panelPrefabDictionary.Add(panelObjectTypes[i], panelPrefabs[i]);
		}

		openPanels = new RectTransform[maxPanelCount];
		openPanelObjects = new PanelObject[maxPanelCount];
		for(int i = 0; i < openPanels.Length; ++i)
		{
			openPanels[i] = null;
			openPanelObjects[i] = null;
		}

		queuedPanelUpdates = new HashSet<int>();

		waitForPanelHeaderBlink = new WaitForSecondsRealtime(panelHeaderBlinkInterval);

		instance = this;
	}

	private void Start()
	{
		camera = Camera.main;
		eventSystem = EventSystem.current;
	}

	private void Update()
	{
		if(Input.GetMouseButtonDown(0) && !eventSystem.IsPointerOverGameObject())
		{
			Ray ray = camera.ScreenPointToRay(Input.mousePosition);
			RaycastHit hit;
			if(Physics.Raycast(ray, out hit, 10000.0f))
			{
				PanelObject panelObject = hit.collider.gameObject.GetComponent<PanelObject>();
				if(panelObject != null)
				{
					OpenPanel(panelObject);
				}
			}
		}

		foreach(int panelId in queuedPanelUpdates)
		{
			openPanelObjects[panelId].UpdatePanel(openPanels[panelId]);
		}
		queuedPanelUpdates.Clear();
	}

	public void OpenPanel(PanelObject panelObject)
	{
		int freePanelPosition = -1;
		for(int i = 0; i < openPanels.Length; ++i)
		{
			if(freePanelPosition < 0 && openPanels[i] == null)
			{
				freePanelPosition = i;
			}
			else if(openPanelObjects[i] == panelObject)
			{
				StartCoroutine(HighlightPanel(openPanels[i]));
				return;
			}
		}
		if(freePanelPosition < 0)
		{
			ClosePanel(openPanels[openPanels.Length - 1]);
			freePanelPosition = openPanels.Length - 1;
		}

		RectTransform panel = GameObject.Instantiate<RectTransform>(panelPrefabDictionary[panelObject.GetPanelObjectType()], Vector2.zero, Quaternion.identity, panelParent);
		panel.anchoredPosition = new Vector2(((freePanelPosition % maxTiledPanelCount) / 2) * panel.sizeDelta.x, -((freePanelPosition % maxTiledPanelCount) % 2) * panel.sizeDelta.y) // Tiled Display
			+ new Vector2(25.0f, -25.0f) * (freePanelPosition / maxTiledPanelCount); // Overflow Displacement
		panel.GetChild(0).GetComponentInChildren<Button>().onClick.AddListener(delegate
		{
			ClosePanel(panel);
		});

		openPanels[freePanelPosition] = panel;
		openPanelObjects[freePanelPosition] = panelObject;

		panelObject.UpdatePanel(panel);
	}

	public void ClosePanel(RectTransform panel)
	{
		for(int i = 0; i < openPanels.Length; ++i)
		{
			if(openPanels[i] == panel)
			{
				openPanels[i] = null;
				openPanelObjects[i] = null;
				break;
			}
		}

		GameObject.Destroy(panel.gameObject);
	}

	public void ClosePanel(PanelObject panelObject)
	{
		RectTransform panel = null;
		for(int i = 0; i < openPanelObjects.Length; ++i)
		{
			if(openPanelObjects[i] == panelObject)
			{
				panel = openPanels[i];
				openPanels[i] = null;
				openPanelObjects[i] = null;
				break;
			}
		}

		if(panel != null)
		{
			GameObject.Destroy(panel.gameObject);
		}
	}

	public void CloseTilePanels()
	{
		for(int i = 0; i < openPanels.Length; ++i)
		{
			if(openPanels[i] != null && openPanelObjects[i] != null && openPanelObjects[i] is Tile)
			{
				ClosePanel(openPanels[i]);
			}
		}
	}

	public void QueuePanelUpdate(PanelObject panelObject)
	{
		for(int i = 0; i < openPanelObjects.Length; ++i)
		{
			if(openPanelObjects[i] == panelObject)
			{
				queuedPanelUpdates.Add(i);
			}
		}
	}

	public void QueueAllPanelUpdate()
	{
		for(int i = 0; i < openPanels.Length; ++i)
		{
			if(openPanels[i] != null && openPanelObjects[i] != null)
			{
				queuedPanelUpdates.Add(i);
			}
		}
	}

	private IEnumerator HighlightPanel(RectTransform panel)
	{
		Image headingBackground = panel.GetChild(0).GetComponent<Image>();
		headingBackground.enabled = false;
		yield return waitForPanelHeaderBlink;
		headingBackground.enabled = true;
	}

	public RectTransform GetCharacterPanel()
	{
		return characterPanel;
	}
}
