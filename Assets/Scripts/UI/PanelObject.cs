using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PanelObject : MonoBehaviour
{
	[SerializeField] private PanelManager.PanelObjectType panelObjectType = PanelManager.PanelObjectType.Tile;
	protected HashSet<RectTransform> openPanels = null;
	protected PanelManager panelManager = null;

	protected virtual void Start()
	{
		openPanels = new HashSet<RectTransform>(1);

		panelManager = PanelManager.GetInstance();
	}

	public virtual void UpdatePanel(RectTransform panel, bool add = true)
	{
		if(add && panel.gameObject.activeSelf)
		{
			openPanels.Add(panel);
		}
	}

	public void UpdateAllPanels()
	{
		foreach(RectTransform panel in openPanels)
		{
			UpdatePanel(panel, false);
		}
	}

	protected Inventory EnsurePlayerPresence()
	{
		Inventory[] inventories = transform.parent.GetComponentsInChildren<Inventory>();	// Get Component in Siblings
		foreach(Inventory inventory in inventories)
		{
			if(inventory.IsLocalPlayerInventory())
			{
				return inventory;
			}
		}

		foreach(RectTransform panel in openPanels)
		{
			panelManager.ClosePanel(panel);
		}

		return null;
	}

	public void ClosePanel(RectTransform panel)
	{
		openPanels.Remove(panel);
	}

	public PanelManager.PanelObjectType GetPanelObjectType()
	{
		return panelObjectType;
	}
}
