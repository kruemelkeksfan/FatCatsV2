using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;

public class CameraController : MonoBehaviour
{
	[SerializeField] private MinMax cameraYRestraints = new MinMax(0.0f, 1000.0f);
	[SerializeField] private float zoomSpeed = 1.0f;
	private new Transform transform = null;

	private void Awake()
	{
		transform = gameObject.GetComponent<Transform>();
	}

	private void Update()
	{
		if(!EventSystem.current.IsPointerOverGameObject())
		{
			transform.Translate(Vector3.forward * Input.mouseScrollDelta.y * transform.position.y * zoomSpeed, Space.Self);

			if(transform.position.y < cameraYRestraints.min)
			{
				transform.position = new Vector3(transform.position.x, cameraYRestraints.min, transform.parent.position.z - cameraYRestraints.min);
			}
			else if(transform.position.y > cameraYRestraints.max)
			{
				transform.position = new Vector3(transform.position.x, cameraYRestraints.max, transform.parent.position.z - cameraYRestraints.max);
			}
		}
	}
}
