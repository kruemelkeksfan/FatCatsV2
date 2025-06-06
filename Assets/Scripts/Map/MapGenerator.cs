using System.Collections.Generic;
using UnityEngine;

public static class MapGenerator
{
	public static Map GenerateMap(int seed, int mapWidth, int mapHeight, float tileSize, bool encounterMap, float sightlineEyeHeight,
		Transform[] terrainPrefabs, Transform[] terrainHeightThresholds, Material[] terrainMaterials,
		float smoothness, float steepness,
		float forestThreshold, float resourceSmoothness, float oreDepositChance,
		Town[] townPrefabs, int maxTownCount,
		Transform terrainParent,
		TilePool tilePool,
		float offsetX = 0, float offsetZ = 0)
	{
		// GENERATE DATA AND RESOURCES

		// Seed Generator
		Random.InitState(seed);

		Tile[,] tiles = new Tile[mapWidth, mapHeight];
		float[,,] tileResources = null;
		List<Town> towns = null;
		Tile mapTile = null;
		Resource[] mapTileResources = null;
		float[] depositChances = null;
		if(!encounterMap)
		{
			tileResources = new float[mapWidth, mapHeight, 10];
			towns = new List<Town>();
		}
		else
		{
			mapTile = Player.GetInstance().GetCurrentTile();
			mapTileResources = mapTile.GetResourceTypes();

			int encounterMapArea = mapWidth * mapHeight;
			depositChances = new float[mapTileResources.Length];
			for(int i = 0; i < mapTileResources.Length; ++i)
			{
				// Average Deposit Size ~= Max Deposit Size * 0.5
				// Total Resource Amount ~= Average Deposit Size * Deposit Chance * Number of Tiles
				// Deposit Chance ~= Total Resource Amount / ((Max Deposit Size * 0.5) * Number of Tiles)
				depositChances[i] = mapTile.GetResourceAmount(mapTileResources[i]) / ((mapTileResources[i].maxDepositSize * 0.5f) * encounterMapArea);
				if(depositChances[i] > 1.0f)
				{
					Debug.LogWarning((depositChances[i] * 100.0f) + "% Deposit Chance for " + mapTileResources[i].goodName + " on ne Encounter Tile!");
				}
			}
		}

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

				// Rotate Resource Parent
				tileTransform.GetChild(2).Rotate(0.0f, Random.Range(0.0f, 360.0f), 0.0f);

				// Generate Forests
				float forestyness = Mathf.Clamp01(Mathf.PerlinNoise((seed % 100000) + (50000.0f + position.x) / resourceSmoothness, (200000.0f + position.z) / resourceSmoothness));
				bool forest = false;
				if(forestyness > forestThreshold)
				{
					forest = true;
				}

				// Save Data
				tiles[x, z] = tileTransform.GetComponent<Tile>();
				tiles[x, z].InitData(new Vector2Int(x, z), "Plains", height, forest, (encounterMap ? mapTile : null));

				// Generate Resources
				if(!encounterMap)
				{
					tileResources[x, z, 0] = Mathf.Clamp01(forestyness);                                                         // Wood
					tileResources[x, z, 1] = Mathf.Clamp01(forestyness);                                                         // Berries
					tileResources[x, z, 2] = Mathf.Clamp01(forestyness);                                                         // Twigs
					tileResources[x, z, 3] = Mathf.Clamp01(1.0f - forestyness);                                                  // Herbs
					tileResources[x, z, 4] = Mathf.Clamp01(1.0f - forestyness);                                                  // Flax
					tileResources[x, z, 5] = Mathf.Clamp01((Random.value < oreDepositChance) ? Random.Range(0.0f, 1.0f) : 0.0f); // Stone
					tileResources[x, z, 6] = Mathf.Clamp01((Random.value < oreDepositChance) ? Random.Range(0.0f, 1.0f) : 0.0f); // Copper Ore
					tileResources[x, z, 7] = Mathf.Clamp01((Random.value < oreDepositChance) ? Random.Range(0.0f, 1.0f) : 0.0f); // Iron Ore
					tileResources[x, z, 8] = Mathf.Clamp01((Random.value < oreDepositChance) ? Random.Range(0.0f, 1.0f) : 0.0f); // Gold Ore
					tileResources[x, z, 9] = Mathf.Clamp01((Random.value < oreDepositChance) ? Random.Range(0.0f, 1.0f) : 0.0f); // Coal
				}
				else
				{
					int[] encounterTileResources = new int[mapTileResources.Length];
					for(int i = 0; i < mapTileResources.Length; ++i)
					{
						// + 1, because Random.Range is max-exclusive
						encounterTileResources[i] = (Random.value < depositChances[i]) ? Random.Range(0, mapTileResources[i].maxDepositSize + 1) : 0;
					}
					tiles[x, z].InitEncounterMapResources(encounterTileResources);

					tiles[x, z].SetFogOfWar(Tile.FogOfWar.Invisible);
				}
			}
		}

		if(!encounterMap)
		{
			// Smooth out Ore Deposits over Neighbours after all Deposits have been placed
			for(int z = 0; z < mapHeight; ++z)
			{
				for(int x = 0; x < mapWidth; ++x)
				{
					float[] maxOres = new float[4];
					foreach(Vector2Int neighbourDirection in MathUtil.GetHexNeighbourDirections(z % 2 == 0))
					{
						for(int i = 0; i < maxOres.Length; ++i)
						{
							int neighbourX = x + neighbourDirection.x;
							int neighbourZ = z + neighbourDirection.y;
							if(neighbourX >= 0 && neighbourX < mapWidth
								&& neighbourZ >= 0 && neighbourZ < mapHeight
								&& tileResources[neighbourX, neighbourZ, 6 + i] > maxOres[i]
								&& Random.value < 0.5f)                                         // Static Random Factor to get more irregular looking Deposits
							{
								maxOres[i] = tileResources[neighbourX, neighbourZ, 6 + i];
							}
						}
					}

					tiles[x, z].InitResources(new float[]
						{
						tileResources[x, z, 0],																							   // Wood
						tileResources[x, z, 1],																							   // Berries
						tileResources[x, z, 2],																							   // Twigs
						tileResources[x, z, 3],																							   // Herbs
						tileResources[x, z, 4],																							   // Flax
						((maxOres[0] > 0.0f && tileResources[x, z, 6] <= 0.0f) ? Random.Range(0.0f, maxOres[0]) : tileResources[x, z, 5]), // Stone
						((maxOres[0] > 0.0f && tileResources[x, z, 6] <= 0.0f) ? Random.Range(0.0f, maxOres[0]) : tileResources[x, z, 6]), // Copper Ore
						((maxOres[1] > 0.0f && tileResources[x, z, 7] <= 0.0f) ? Random.Range(0.0f, maxOres[1]) : tileResources[x, z, 7]), // Iron Ore
						((maxOres[2] > 0.0f && tileResources[x, z, 8] <= 0.0f) ? Random.Range(0.0f, maxOres[2]) : tileResources[x, z, 8]), // Gold Ore
						((maxOres[3] > 0.0f && tileResources[x, z, 9] <= 0.0f) ? Random.Range(0.0f, maxOres[3]) : tileResources[x, z, 9])  // Coal
						});

					tiles[x, z].SetFogOfWar(Tile.FogOfWar.Invisible);
				}
			}

			// Generate Towns
			for(int i = 0; i < maxTownCount; ++i)
			{
				Transform tileTransform = terrainParent.GetChild(Random.Range(0, terrainParent.childCount - 1));
				if(tileTransform.GetComponentInChildren<Town>() == null)
				{
					// Disable Resource Parent
					tileTransform.GetChild(2).gameObject.SetActive(false);

					// Instantiate Building Prefab
					Town town = GameObject.Instantiate<Town>(townPrefabs[0], new Vector3(tileTransform.position.x, tileTransform.position.y, tileTransform.position.z), Quaternion.Euler(0.0f, Random.Range(0, 4) * 90.0f, 0.0f), tileTransform);
					town.gameObject.SetActive(false); // Set Town inactive, because Tiles are invisible by default

					// Remove Forest
					Tile tile = tileTransform.GetComponent<Tile>();
					if(tile.IsForest())
					{
						tile.SetForest(false);
					}
					tile.SetTown(town);

					// Add to Town List
					towns.Add(town);
				}
			}
		}

		// GENERATE MAP MESH

		GameObject mapModel = new GameObject("Map");
		mapModel.GetComponent<Transform>().parent = terrainParent;

		// Each Core Hexagon has 4 Tris.
		//
		//                    North
		//                     ## 
		//                  ######## 
		//               ####North #### 
		// North West ####____________#### North East
		//            ##   \____        ##
		//            ## West   \__East ##
		//            ##  __________\_  ##
		// South West ####            #### South East
		//               ####South ####
		//                  ########
		//                     ##
		//                    South
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

		// TODO: Add Colors when different Biomes get implemented: https://stackoverflow.com/questions/66116331/unity3d-how-to-add-textures-to-a-mesh

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
