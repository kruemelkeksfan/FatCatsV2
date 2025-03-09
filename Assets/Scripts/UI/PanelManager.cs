using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PanelManager : MonoBehaviour
{
	public enum PanelObjectType { Tile, Town, ResourceInfo, Inventory, Skills, Menu, Market, Buildings };

	private static PanelManager instance = null;

	[SerializeField] private RectTransform[] panelPrefabs = { };
	[SerializeField] private RectTransform panelParent = null;
	[SerializeField] private int maxTiledPanelCount = 8;
	[SerializeField] private int maxPanelCount = 24;
	[SerializeField] private RectTransform characterPanel = null;
	private Dictionary<PanelObjectType, RectTransform> panelPrefabDictionary = null;
	private new Camera camera = null;
	private EventSystem eventSystem = null;
	private RectTransform[] openPanels = null;
	private PanelObject[] openPanelObjects = null;
	private HashSet<PanelObject> queuedPanelUpdates = null;

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

		queuedPanelUpdates = new HashSet<PanelObject>();

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

		foreach(PanelObject panelObject in queuedPanelUpdates)
		{
			panelObject.UpdateAllPanels();
		}
		queuedPanelUpdates.Clear();
	}

	public void OpenPanel(PanelObject panelObject)
	{
		int freePanelPosition = -1;
		for(int i = 0; i < openPanels.Length; ++i)
		{
			if(openPanels[i] == null)
			{
				freePanelPosition = i;
				break;
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
				openPanelObjects[i].ClosePanel(panel);

				openPanels[i] = null;
				openPanelObjects[i] = null;
				break;
			}
		}

		GameObject.Destroy(panel.gameObject);
	}

	public void QueuePanelUpdate(PanelObject panelObject)
	{
		queuedPanelUpdates.Add(panelObject);
	}

	public void QueueAllPanelUpdate()
	{
		for(int i = 0; i < openPanels.Length; ++i)
		{
			if(openPanels[i] != null && openPanelObjects[i] != null)
			{
				queuedPanelUpdates.Add(openPanelObjects[i]);
			}
		}
	}

	public RectTransform GetCharacterPanel()
	{
		return characterPanel;
	}
}
