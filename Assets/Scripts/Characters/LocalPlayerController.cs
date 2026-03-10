using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class LocalPlayerController : MonoBehaviour, IPlayerController
{
	private PanelManager panelManager = null;
	private new Camera camera = null;
	private EventSystem eventSystem = null;
	private Player player = null;

	private void Start()
	{
		panelManager = PanelManager.GetInstance();

		player = gameObject.GetComponent<Player>();

		camera = Camera.main;
		eventSystem = EventSystem.current;
	}

	private void Update()
	{
		if(Input.GetMouseButtonDown(1) && !eventSystem.IsPointerOverGameObject())
		{
			Ray ray = camera.ScreenPointToRay(Input.mousePosition);
			RaycastHit hit;
			if(Physics.Raycast(ray, out hit, 10000.0f))
			{
				Tile targetTile = hit.collider.gameObject.GetComponentInParent<Tile>();
				if(targetTile != null)
				{
					player.StartMovement(targetTile);
				}
			}
		}
	}
}
