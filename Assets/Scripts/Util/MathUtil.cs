using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class MathUtil
{
	private class TilePathData
	{
		public Vector2Int position;
		public Tile tile;
		public float estimatedPathCost;
		public TilePathData visitedFrom;
	}

	public const float EPSILON = 0.0001f;

	private static int daysPerYear = -1;

	public static List<Tile> FindPath(Map map, Tile startTile, Tile targetTile)
	{
		float minimumMovementCost = map.GetTile(Vector2Int.zero).CalculateBestMovementCost();

		Vector2Int startPosition = startTile.GetPosition();
		Vector2Int targetPosition = targetTile.GetPosition();

		LinkedList<TilePathData> openList = new LinkedList<TilePathData>();
		HashSet<Vector2Int> closedList = new HashSet<Vector2Int>();

		TilePathData startNode = new TilePathData();
		startNode.position = startPosition;
		startNode.tile = startTile;
		startNode.estimatedPathCost = CalculateHeuristicToTarget(startPosition, targetPosition, minimumMovementCost);
		startNode.visitedFrom = null;
		openList.AddFirst(startNode);

		while(openList.Count > 0)
		{
			// Buffer current Node
			TilePathData currentTilePathData = openList.First.Value;

			// Did we already reach the Target?
			if(currentTilePathData.position == targetPosition)
			{
				List<Tile> path = new List<Tile>();
				do
				{
					path.Add(currentTilePathData.tile);
					currentTilePathData = currentTilePathData.visitedFrom;
				}
				while(currentTilePathData != null);
				path.Reverse();

				return path;
			}

			// Find new Neighbours
			Vector2Int[] neighbourDirections = GetHexNeighbourDirections(currentTilePathData.position.y % 2 == 0);
			foreach(Vector2Int direction in neighbourDirections)
			{
				Vector2Int neighbourPosition = currentTilePathData.position + direction;

				if(closedList.Contains(neighbourPosition))
				{
					continue;
				}

				Tile neighbourTile = map.GetTile(neighbourPosition);
				if(neighbourTile == null)
				{
					continue;
				}

				TilePathData neighbourTilePathData = new TilePathData();
				neighbourTilePathData.position = neighbourPosition;
				neighbourTilePathData.tile = neighbourTile;
				neighbourTilePathData.estimatedPathCost = currentTilePathData.estimatedPathCost
					+ neighbourTile.CalculateMovementCost(currentTilePathData.tile)
					+ CalculateHeuristicToTarget(neighbourPosition, targetPosition, minimumMovementCost);
				neighbourTilePathData.visitedFrom = currentTilePathData;

				// Insert new Node in correct Position in openList
				LinkedListNode<TilePathData> openListNode = openList.First;
				bool inserted = false;
				bool redundantPath = false;
				do
				{
					if(!inserted && openListNode.Value.estimatedPathCost > neighbourTilePathData.estimatedPathCost)
					{
						openList.AddBefore(openListNode, neighbourTilePathData);
						inserted = true;
					}

					// Does the Neighbour already exist in openList?
					if(openListNode.Value.position == neighbourTilePathData.position)
					{
						// If new found Path is better, remove the old Node
						if(inserted)
						{
							openList.Remove(openListNode);
						}
						// If the new found Path is worse, keep the old one unchanged
						else
						{
							redundantPath = true;
							break;
						}
					}

					openListNode = openListNode.Next;
				}
				while(openListNode != null);

				if(!inserted && !redundantPath)
				{
					openList.AddLast(neighbourTilePathData);
				}
			}

			// Add searched Tile to closedList and remove it from openList
			closedList.Add(currentTilePathData.position);
			openList.RemoveFirst();
		}

		return null;
	}

	public static Vector2Int[] GetHexNeighbourDirections(bool isOddRow)
	{
		Vector2Int[] neighbourDirections = { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down, Vector2Int.zero, Vector2Int.zero };
		if(isOddRow)
		{
			neighbourDirections[4] = Vector2Int.up + Vector2Int.left;
			neighbourDirections[5] = Vector2Int.down + Vector2Int.left;
		}
		else
		{
			neighbourDirections[4] = Vector2Int.up + Vector2Int.right;
			neighbourDirections[5] = Vector2Int.down + Vector2Int.right;
		}

		return neighbourDirections;
	}

	private static float CalculateHeuristicToTarget(Vector2Int start, Vector2Int target, float minimumMovementCost)
	{
		// Return Taxicab Distance
		return (Mathf.Abs(target.x - start.x) + Mathf.Abs(target.y - start.y)) * minimumMovementCost;
	}

	public static string GetTimespanString(float days)
	{
		if(daysPerYear <= 0)
		{
			daysPerYear = TimeController.GetInstance().GetDaysPerYear();
		}

		if(days == 1.0f)
		{
			return days + " day";
		}
		else if(days >= daysPerYear * 10)
		{
			return Mathf.RoundToInt(days / daysPerYear) + " years";
		}
		else if(days >= daysPerYear)
		{
			return (days / daysPerYear).ToString("F1") + " years";
		}
		else
		{
			int roundedDays = Mathf.RoundToInt(days);
			return (Mathf.Approximately(days, roundedDays) ? roundedDays : days.ToString("F1")) + " days";
		}
	}

	// Source: https://stackoverflow.com/questions/7040289/converting-integers-to-roman-numerals
	public static string GetRomanNumber(int number)
	{
		if((number < 0) || (number >= 40))
		{
			return "Invalid Number";
		}
		if(number < 1)
		{
			return string.Empty;
		}
		if(number >= 10)
		{
			return "X" + GetRomanNumber(number - 10);
		}
		if(number >= 9)
		{
			return "IX" + GetRomanNumber(number - 9);
		}
		if(number >= 5)
		{
			return "V" + GetRomanNumber(number - 5);
		}
		if(number >= 4)
		{
			return "IV" + GetRomanNumber(number - 4);
		}
		if(number >= 1)
		{
			return "I" + GetRomanNumber(number - 1);
		}
		return "Impossible Number";
	}
}
