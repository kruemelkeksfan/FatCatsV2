using UnityEngine;

public class PanelObject : MonoBehaviour
{
	[SerializeField] private PanelManager.PanelObjectType panelObjectType = PanelManager.PanelObjectType.Tile;
	protected PanelManager panelManager = null;

	protected virtual void Start()
	{
		panelManager = PanelManager.GetInstance();
	}

	public virtual void UpdatePanel(RectTransform panel)
	{
		if(!panel.gameObject.activeSelf)
		{
			return;
		}
	}

	protected Inventory EnsurePlayerPresence()
	{
		// Get Component in Siblings universally, even when PanelObjects are on different Hierarchy Layers below Tile
		Inventory[] inventories = transform.GetComponentInParent<Tile>().GetComponentsInChildren<Inventory>();
		foreach(Inventory inventory in inventories)
		{
			if(inventory.IsLocalPlayerInventory())
			{
				return inventory;
			}
		}

		panelManager.ClosePanel(this);

		return null;
	}

	public PanelManager.PanelObjectType GetPanelObjectType()
	{
		return panelObjectType;
	}
}
