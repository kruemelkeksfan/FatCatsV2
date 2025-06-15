using UnityEngine;
using UnityEngine.UI;

public class ResourceFilterController : MonoBehaviour
{
	[SerializeField] private Color activeFilterButtonColor = new Color();
	private new Transform transform = null;
	private Player player = null;
	private string currentResourceFilter = string.Empty;
	private Color inactiveFilterButtonColor = new Color();

	private void Start()
	{
		transform = GetComponent<Transform>();
		player = Player.GetInstance();

		inactiveFilterButtonColor = transform.GetChild(0).GetComponent<Image>().color;
	}

	public void ToggleFilterPanel()
	{
		gameObject.SetActive(!gameObject.activeSelf);
	}

	public void SetResourceFilter(string resourceFilter)
	{
		if(resourceFilter != currentResourceFilter)
		{
			currentResourceFilter = resourceFilter;
		}
		else
		{
			currentResourceFilter = string.Empty;
		}
		
		player.GetCurrentMap().UpdateResourceFilter(currentResourceFilter);
	}

	public void SetButtonActive(int buttonIndex)
	{
		for(int i = 0; i < transform.childCount; ++i)
		{
			transform.GetChild(i).GetComponent<Image>().color = inactiveFilterButtonColor;
		}

		if(currentResourceFilter != string.Empty)
		{
			transform.GetChild(buttonIndex).GetComponent<Image>().color = activeFilterButtonColor;
		}
	}
}
