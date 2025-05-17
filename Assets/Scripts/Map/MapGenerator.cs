using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class MapGenerator
{
	public static Map GenerateMap(int seed, int mapWidth, int mapHeight, float tileSize, float sightlineEyeHeight,
		Transform[] terrainPrefabs, Transform[] terrainHeightThresholds, Material[] terrainMaterials,
		float smoothness, float steepness,
		Transform[] forestPrefabs, float forestThreshold,
		float resourceSmoothness,
		Town[] townPrefabs, int maxTownCount,
		bool generateResources,
		Transform terrainParent,
		Transform exitMarkerPrefab,
		TilePool tilePool,
		float offsetX = 0, float offsetZ = 0)
	{
		// Seed Generator
		Random.InitState(seed);

		Tile[,] tiles = new Tile[mapWidth, mapHeight];
		List<Town> towns = new List<Town>();
		for(int z = 0; z < mapHeight; ++z)
		{
			for(int x = 0; x < mapWidth; ++x)
			{
				// Generate Terrain
				// Add 50000.0f, because negative Coordinates for Perlin Maps produce sad Results
				Vector3 position = new Vector3(x * tileSize - (z % 2 == 0 ? tileSize * 0.5f : 0.0f), 0.0f, z * tileSize);
				float height = Mathf.Clamp01(Mathf.PerlinNoise(((seed % 100000) + 50000.0f + position.x) / smoothness, (100000.0f + position.z) / smoothness)) * steepness;
				position += Vector3.up * height;
				Transform tileTransform = tilePool.GetTile(terrainParent, new Vector3(offsetX, 0.0f, offsetZ) + position);

				// Generate Forests
				float forestyness = Mathf.Clamp01(Mathf.PerlinNoise((seed % 100000) + (50000.0f + position.x) / resourceSmoothness, (200000.0f + position.z) / resourceSmoothness));
				bool forest = false;
				if(forestPrefabs.Length > 0)
				{
					if(forestyness > forestThreshold)
					{
						Transform forestTransform = GameObject.Instantiate<Transform>(forestPrefabs[0], new Vector3(offsetX + position.x, height, offsetZ + position.z), Quaternion.Euler(0.0f, Random.Range(0, 4) * 90.0f, 0.0f), tileTransform);
						forest = true;
					}
				}

				// Generate Resources
				float[] resources = { 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f };
				if(generateResources)
				{
					resources = new float[] {
					Mathf.Clamp01(forestyness),																															// Wood
					Mathf.Clamp01(forestyness),																															// Berries
					Mathf.Clamp01(forestyness),																															// Twigs
					Mathf.Clamp01(1.0f - forestyness),																													// Herbs
					Mathf.Clamp01(1.0f - forestyness),																													// Flax
					Mathf.Clamp01(1.0f),																																// Stone
					Mathf.Clamp01(Mathf.PerlinNoise((seed % 100000) + (50000.0f + position.x) / resourceSmoothness, (300000.0f + position.z) / resourceSmoothness)),	// Copper Ore
					Mathf.Clamp01(Mathf.PerlinNoise((seed % 100000) + (50000.0f + position.x) / resourceSmoothness, (400000.0f + position.z) / resourceSmoothness)),	// Iron Ore
					Mathf.Clamp01(Mathf.PerlinNoise((seed % 100000) + (50000.0f + position.x) / resourceSmoothness, (500000.0f + position.z) / resourceSmoothness)),	// Gold Ore
					Mathf.Clamp01(Mathf.PerlinNoise((seed % 100000) + (50000.0f + position.x) / resourceSmoothness, (600000.0f + position.z) / resourceSmoothness))		// Coal
					};
				}

				tiles[x, z] = tileTransform.GetComponent<Tile>();
				tiles[x, z].InitData(new Vector2Int(x, z), "Plains", height, forest, resources);

				if(exitMarkerPrefab != null)
				{
					if(z == 0)
					{
						GameObject.Instantiate<Transform>(exitMarkerPrefab, new Vector3(offsetX + position.x, height + tileSize * 0.5f, offsetZ + position.z), Quaternion.Euler(0.0f, 180.0f, 0.0f), tileTransform);
					}
					else if(z == mapHeight - 1)
					{
						GameObject.Instantiate<Transform>(exitMarkerPrefab, new Vector3(offsetX + position.x, height + tileSize * 0.5f, offsetZ + position.z), Quaternion.Euler(0.0f, 0.0f, 0.0f), tileTransform);
					}
					else if(x == 0)
					{
						GameObject.Instantiate<Transform>(exitMarkerPrefab, new Vector3(offsetX + position.x, height + tileSize * 0.5f, offsetZ + position.z), Quaternion.Euler(0.0f, -90.0f, 0.0f), tileTransform);
					}
					else if(x == mapWidth - 1)
					{
						GameObject.Instantiate<Transform>(exitMarkerPrefab, new Vector3(offsetX + position.x, height + tileSize * 0.5f, offsetZ + position.z), Quaternion.Euler(0.0f, 90.0f, 0.0f), tileTransform);
					}
				}
			}
		}

		// Generate Towns
		for(int i = 0; i < maxTownCount; ++i)
		{
			Transform tileTransform = terrainParent.GetChild(Random.Range(0, terrainParent.childCount - 1));
			if(tileTransform.GetComponentInChildren<Town>() == null)
			{
				Town town = GameObject.Instantiate<Town>(townPrefabs[0], new Vector3(tileTransform.position.x, tileTransform.position.y, tileTransform.position.z), Quaternion.Euler(0.0f, Random.Range(0, 4) * 90.0f, 0.0f), tileTransform);

				Tile tile = tileTransform.GetComponent<Tile>();
				if(tile.IsForest())
				{
					tile.SetForest(false);
					GameObject.Destroy(tileTransform.GetChild(2).gameObject);
				}
				tile.SetTown(town);

				towns.Add(town);
			}
		}

		// GENERATE MAP MESH

		GameObject mapModel = new GameObject("Map");

		// Each Core Hexagon has 4 Tris.
		//
		//                       North
		//                        ## 
		//                     ######## 
		//                  ####North #### 
		//    North West ####____________#### North East
		//               ##   \____        ##
		//               ## West   \__East ##
		//               ##  __________\_  ##
		//    South West ####            #### South East
		//                  ####South ####
		//                     ########
		//                        ##
		//                       South
		//
		// Each Inter Space has 3 Edges a 2 Tris and 2 Corner Tris.
		// Here is an Example of a 2x3 Map. Uneven Rows (-) are assigned different Edges and Corners than even Rows (+).
		// Unassigned Edges (?) need to be filled after the regular Inter Spaces.
		//
		//          ##                          ##                  	 
		//       ########                    ########                        
		//    ####      ####              ####      ####                      
		// ####            ####--------####            ####               
		// ##                ##      / ##                ##               
		// ##  Left Upper +  ## Edge + ## Right Upper +  ##               
		// ##                ## /      ##                ##                          
		// ####            ####--------####            ####                          
		//    ####      ####   \Corner/|  ####      ####   \                       
		//       ########       \ +  /  |    ########       \                       
		//          ##___Edge +__\  /  Edge +   ##__Edge ?___\           
		//            \           ##     |     /  \           ##         
		//             \       ########   |   / +  \       ########                             
		//              \   ####      #### | /Corner\   ####      ####   
		//               ####            ####--------####            ####
		//               ## Left Lower +   ##      / ## Right Lower +  ##
		//               ##                ## Edge - ##                ##
		//               ## Left Upper -   ## /      ## Left Lower -   ## 
		//               ####            ####--------####            #### 
		//              /|  ####      ####   \Corner/|  ####      ####    
		//             /  |    ########       \ -  /  |    ########       
		//            /    |      ##___Edge -__\  /  Edge ?   ##         
		//          ##   Edge -  /  \           ##      |    /                         
		//       ########    |  / -  \       ########    |  /                            
		//    ####      ####  |/Corner\   ####      ####  |/    		 
		// ####            ####--------####            ####    		     
		// ##                ##      / ##                ##			     
		// ##  Left Lower -  ## Edge ? ## Right Lower -  ##			     
		// ##                ## /      ##                ##			     
		// ####            ####--------####            ####			     
		//    ####      ####              ####      ####				 
		//       ########                    ########					 
		//          ##                          ##					     

		// Source: https://de.wikipedia.org/wiki/Sechseck#Formeln
		float innerRadius = tileSize * 0.5f * 0.8f;
		float outerRadius = innerRadius / 0.8660254f;
		float halfOuterRadius = outerRadius * 0.5f;

		int northIndex = 0;
		int northEastIndex = 1;
		int southEastIndex = 2;
		int southIndex = 3;
		int southWestIndex = 4;
		int northWestIndex = 5;

		Vector3[,,] tileCorners = new Vector3[mapWidth, mapHeight, 6];

		List<Vector3> vertices = new List<Vector3>();
		List<Vector3> normals = new List<Vector3>();
		List<int> triangles = new List<int>();

		// Core Tiles
		for(int z = 0; z < mapHeight; ++z)
		{
			for(int x = 0; x < mapWidth; ++x)
			{
				Vector3 position = new Vector3(x * tileSize - (z % 2 == 0 ? tileSize * 0.5f : 0.0f), 0.0f, z * tileSize);
				float height = tiles[x, z].GetHeight();

				// TODO: Slight randomization in horizontal Plane, e.g. random innerRadius between * 0.8f and * 0.9f for each Vertex
				tileCorners[x, z, northIndex] = new Vector3(offsetX + position.x + 0.0f, height, offsetZ + position.z - outerRadius);
				tileCorners[x, z, northEastIndex] = new Vector3(offsetX + position.x + innerRadius, height, offsetZ + position.z - halfOuterRadius);
				tileCorners[x, z, southEastIndex] = new Vector3(offsetX + position.x + innerRadius, height, offsetZ + position.z + halfOuterRadius);
				tileCorners[x, z, southIndex] = new Vector3(offsetX + position.x + 0.0f, height, offsetZ + position.z + outerRadius);
				tileCorners[x, z, southWestIndex] = new Vector3(offsetX + position.x - innerRadius, height, offsetZ + position.z + halfOuterRadius);
				tileCorners[x, z, northWestIndex] = new Vector3(offsetX + position.x - innerRadius, height, offsetZ + position.z - halfOuterRadius);

				AddTriangle(vertices, normals, triangles, tileCorners[x, z, northIndex], tileCorners[x, z, northWestIndex], tileCorners[x, z, northEastIndex], Vector3.up);     // North
				AddTriangle(vertices, normals, triangles, tileCorners[x, z, northWestIndex], tileCorners[x, z, southEastIndex], tileCorners[x, z, northEastIndex], Vector3.up); // North-East
				AddTriangle(vertices, normals, triangles, tileCorners[x, z, northWestIndex], tileCorners[x, z, southWestIndex], tileCorners[x, z, southEastIndex], Vector3.up); // South-West
				AddTriangle(vertices, normals, triangles, tileCorners[x, z, southWestIndex], tileCorners[x, z, southIndex], tileCorners[x, z, southEastIndex], Vector3.up);     // South
			}
		}
		// Tile Connections
		for(int z = 0; z < mapHeight - 1; ++z)
		{
			for(int x = 0; x < mapWidth - 1; ++x)
			{
				// Upper Edge
				AddTriangle(vertices, normals, triangles, tileCorners[x, z, northEastIndex], tileCorners[x, z, southEastIndex], tileCorners[x + 1, z, northWestIndex]);
				AddTriangle(vertices, normals, triangles, tileCorners[x + 1, z, northWestIndex], tileCorners[x, z, southEastIndex], tileCorners[x + 1, z, southWestIndex]);

				if(z % 2 == 0)
				{
					// Lower Right Edge
					AddTriangle(vertices, normals, triangles, tileCorners[x + 1, z, southWestIndex], tileCorners[x, z + 1, northEastIndex], tileCorners[x + 1, z, southIndex]);
					AddTriangle(vertices, normals, triangles, tileCorners[x + 1, z, southWestIndex], tileCorners[x, z + 1, northIndex], tileCorners[x, z + 1, northEastIndex]);

					// Lower Left Edge
					AddTriangle(vertices, normals, triangles, tileCorners[x, z, southEastIndex], tileCorners[x, z, southIndex], tileCorners[x, z + 1, northIndex]);
					AddTriangle(vertices, normals, triangles, tileCorners[x, z, southIndex], tileCorners[x, z + 1, northWestIndex], tileCorners[x, z + 1, northIndex]);

					// Upper Left Corner
					AddTriangle(vertices, normals, triangles, tileCorners[x, z, southEastIndex], tileCorners[x, z + 1, northIndex], tileCorners[x + 1, z, southWestIndex]);

					// Lower Right Corner
					AddTriangle(vertices, normals, triangles, tileCorners[x + 1, z, southIndex], tileCorners[x, z + 1, northEastIndex], tileCorners[x + 1, z + 1, northWestIndex]);
				}
				else
				{
					// Lower Left Edge
					AddTriangle(vertices, normals, triangles, tileCorners[x, z, southWestIndex], tileCorners[x, z + 1, northIndex], tileCorners[x, z + 1, northEastIndex]);
					AddTriangle(vertices, normals, triangles, tileCorners[x, z, southWestIndex], tileCorners[x, z + 1, northEastIndex], tileCorners[x, z, southIndex]);

					// Middle Edge
					AddTriangle(vertices, normals, triangles, tileCorners[x, z, southEastIndex], tileCorners[x, z, southIndex], tileCorners[x + 1, z + 1, northIndex]);
					AddTriangle(vertices, normals, triangles, tileCorners[x, z, southIndex], tileCorners[x + 1, z + 1, northWestIndex], tileCorners[x + 1, z + 1, northIndex]);

					// Upper Right Corner
					AddTriangle(vertices, normals, triangles, tileCorners[x, z, southEastIndex], tileCorners[x + 1, z + 1, northIndex], tileCorners[x + 1, z, southWestIndex]);

					// Lower Left Corner
					AddTriangle(vertices, normals, triangles, tileCorners[x, z, southIndex], tileCorners[x, z + 1, northEastIndex], tileCorners[x + 1, z + 1, northWestIndex]);
				}
			}
		}
			     
		// Map Edges
		int mapEdgeX = mapWidth - 1;
		int mapEdgeZ = mapHeight - 1;
		for(int z = 0; z < mapHeight - 1; ++z)
		{
			if(z % 2 == 0)
			{
				AddTriangle(vertices, normals, triangles, tileCorners[mapEdgeX, z, southEastIndex], tileCorners[mapEdgeX, z, southIndex], tileCorners[mapEdgeX, z + 1, northIndex]);
				AddTriangle(vertices, normals, triangles, tileCorners[mapEdgeX, z, southIndex], tileCorners[mapEdgeX, z + 1, northWestIndex], tileCorners[mapEdgeX, z + 1, northIndex]);
			}
			else
			{
				AddTriangle(vertices, normals, triangles, tileCorners[mapEdgeX, z, southWestIndex], tileCorners[mapEdgeX, z + 1, northIndex], tileCorners[mapEdgeX, z + 1, northEastIndex]);
				AddTriangle(vertices, normals, triangles, tileCorners[mapEdgeX, z, southWestIndex], tileCorners[mapEdgeX, z + 1, northEastIndex], tileCorners[mapEdgeX, z, southIndex]);
			}
		}
		for(int x = 0; x < mapWidth - 1; ++x)
		{
			AddTriangle(vertices, normals, triangles, tileCorners[x, mapEdgeZ, northEastIndex], tileCorners[x, mapEdgeZ, southEastIndex], tileCorners[x + 1, mapEdgeZ, northWestIndex]);
			AddTriangle(vertices, normals, triangles, tileCorners[x, mapEdgeZ, southEastIndex], tileCorners[x + 1, mapEdgeZ, southWestIndex], tileCorners[x + 1, mapEdgeZ, northWestIndex]);
		}

		Mesh mesh = new Mesh();
		mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // Necessary for large Maps, because 16 bits do not provide enough Vertex Indices (16 bit = 64k)
		mesh.Clear();
		mesh.vertices = vertices.ToArray();
		mesh.normals = normals.ToArray();
		mesh.triangles = triangles.ToArray();
		mesh.RecalculateBounds();
		mapModel.AddComponent<MeshFilter>().mesh = mesh;
		mapModel.AddComponent<MeshRenderer>().material = terrainMaterials[0];

		return new Map(mapModel, tiles, towns, tileSize, sightlineEyeHeight);
	}

	private static void AddTriangle(List<Vector3> vertices, List<Vector3> normals, List<int> triangles, Vector3 vertex1, Vector3 vertex2, Vector3 vertex3, Vector3? normal = null)
	{
		if(normal == null)
		{
			normal = Vector3.Cross(vertex2 - vertex1, vertex3 - vertex1);
		}

		int firstVertexIndex = vertices.Count;

		vertices.Add(vertex1);
		vertices.Add(vertex2);
		vertices.Add(vertex3);

		for(int i = 0; i < 3; ++i)
		{
			normals.Add(normal.Value);
		}

		for(int i = 0; i < 3; ++i)
		{
			triangles.Add(firstVertexIndex + i);
		}
	}
}
