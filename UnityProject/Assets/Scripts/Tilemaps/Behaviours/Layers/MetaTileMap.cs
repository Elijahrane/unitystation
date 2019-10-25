﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[ExecuteInEditMode]
public class MetaTileMap : MonoBehaviour
{
	/// <summary>
	/// Use this dictionary only if performance isn't critical, otherwise try using arrays below
	/// </summary>
	public Dictionary<LayerType, Layer> Layers { get; private set; }

	//Using arrays for iteration speed
	public LayerType[] LayersKeys { get; private set; }
	public Layer[] LayersValues { get; private set; }
	public Layer ObjectLayer { get; private set; }

	/// <summary>
	/// Array of only layers that can ever contain solid stuff
	/// </summary>
	public Layer[] SolidLayersValues { get; private set; }
	/// <summary>
	/// Layers that contain TilemapDamage
	/// </summary>
	public Layer[] DamageableLayers { get; private set; }

	private void OnEnable()
	{
		Layers = new Dictionary<LayerType, Layer>();
		var layersKeys = new List<LayerType>();
		var layersValues = new List<Layer>();
		var solidLayersValues = new List<Layer>();
		var damageableLayersValues = new List<Layer>();

		foreach (Layer layer in GetComponentsInChildren<Layer>(true))
		{
			var type = layer.LayerType;
			Layers[type] = layer;
			layersKeys.Add(type);
			layersValues.Add(layer);
			if (type != LayerType.Effects
			    && type != LayerType.None)
			{
				solidLayersValues.Add(layer);
			}

			if ( layer.GetComponent<TilemapDamage>() )
			{
				damageableLayersValues.Add( layer );
			}

			if ( layer.LayerType == LayerType.Objects )
			{
				ObjectLayer = layer;
			}
		}

		LayersKeys = layersKeys.ToArray();
		LayersValues = layersValues.ToArray();
		SolidLayersValues = solidLayersValues.ToArray();
		DamageableLayers = damageableLayersValues.ToArray();
	}

	/// <summary>
	/// Apply damage to damageable layers, top to bottom.
	/// If tile gets destroyed, remaining damage is applied to the layer below
	/// </summary>
	public void ApplyDamage(Vector3Int cellPos, float damage, Vector3Int worldPos, ref float resistance)
	{
		foreach ( var damageableLayer in DamageableLayers )
		{
			if ( damage <= 0f )
			{
				return;
			}
			resistance += damageableLayer.TilemapDamage.Integrity( cellPos );
			damage = damageableLayer.TilemapDamage.ApplyDamage( cellPos, damage, worldPos );
		}
	}

	public bool IsPassableAt(Vector3Int position, bool isServer)
	{
		return IsPassableAt(position, position, isServer);
	}

	public bool IsPassableAt(Vector3Int origin, Vector3Int to, bool isServer,
		CollisionType collisionType = CollisionType.Player, bool inclPlayers = true, GameObject context = null)
	{
		Vector3Int toX = new Vector3Int(to.x, origin.y, origin.z);
		Vector3Int toY = new Vector3Int(origin.x, to.y, origin.z);

		return _IsPassableAt(origin, toX, isServer, collisionType, inclPlayers, context) &&
		       _IsPassableAt(toX, to, isServer, collisionType, inclPlayers, context) ||
		       _IsPassableAt(origin, toY, isServer, collisionType, inclPlayers, context) &&
		       _IsPassableAt(toY, to, isServer, collisionType, inclPlayers, context);
	}

	private bool _IsPassableAt(Vector3Int origin, Vector3Int to, bool isServer,
		CollisionType collisionType = CollisionType.Player, bool inclPlayers = true, GameObject context = null)
	{
		for (var i = 0; i < SolidLayersValues.Length; i++)
		{
			// Skip floor & base collisions if this is not a shuttle
			if (collisionType != CollisionType.Shuttle &&
			    (SolidLayersValues[i].LayerType == LayerType.Floors ||
			     SolidLayersValues[i].LayerType == LayerType.Base))
			{
				continue;
			}

			if (!SolidLayersValues[i].IsPassableAt(origin, to, isServer, collisionType: collisionType,
				inclPlayers: inclPlayers, context: context))
			{
				return false;
			}
		}

		return true;
	}

	public bool IsAtmosPassableAt(Vector3Int position, bool isServer)
	{
		return IsAtmosPassableAt(position, position, isServer);
	}

	public bool IsAtmosPassableAt(Vector3Int origin, Vector3Int to, bool isServer)
	{
		Vector3Int toX = new Vector3Int(to.x, origin.y, origin.z);
		Vector3Int toY = new Vector3Int(origin.x, to.y, origin.z);

		return _IsAtmosPassableAt(origin, toX, isServer) && _IsAtmosPassableAt(toX, to, isServer) ||
		       _IsAtmosPassableAt(origin, toY, isServer) && _IsAtmosPassableAt(toY, to, isServer);
	}

	private bool _IsAtmosPassableAt(Vector3Int origin, Vector3Int to, bool isServer)
	{
		for (var i = 0; i < LayersValues.Length; i++)
		{
			if (!LayersValues[i].IsAtmosPassableAt(origin, to, isServer))
			{
				return false;
			}
		}

		return true;
	}

	public bool IsSpaceAt(Vector3Int position, bool isServer)
	{
		for (var i = 0; i < LayersValues.Length; i++)
		{
			if (!LayersValues[i].IsSpaceAt(position, isServer))
			{
				return false;
			}
		}

		return true;
	}

	public bool IsTileTypeAt(Vector3Int position, bool isServer, TileType tileType)
	{
		for (var i = 0; i < LayersValues.Length; i++)
		{
			LayerTile tile = LayersValues[i].GetTile(position);
			if (tile != null && tile.TileType == tileType)
			{
				return true;
			}
		}

		return false;
	}

	public void SetTile(Vector3Int position, LayerTile tile, Matrix4x4 transformMatrix)
	{
		Layers[tile.LayerType].SetTile(position, tile, transformMatrix);
	}

	public void SetTile(Vector3Int position, LayerTile tile)
	{
		Layers[tile.LayerType].SetTile(position, tile, Matrix4x4.identity);
	}

	/// <summary>
	/// Gets the tile with the specified layer type at the specified world position
	/// </summary>
	/// <param name="worldPosition">world position to check</param>
	/// <param name="layerType"></param>
	/// <returns></returns>
	public LayerTile GetTileAtWorldPos(Vector3 worldPosition, LayerType layerType)
	{
		return GetTileAtWorldPos(worldPosition.RoundToInt(), layerType);
	}
	/// <summary>
	/// Gets the tile with the specified layer type at the specified world position
	/// </summary>
	/// <param name="worldPosition">world position to check</param>
	/// <param name="layerType"></param>
	/// <returns></returns>
	public LayerTile GetTileAtWorldPos(Vector3Int worldPosition, LayerType layerType)
	{
		var cellPos = WorldToCell(worldPosition);
		return GetTile(cellPos, layerType);
	}

	/// <summary>
	/// Gets the tile with the specified layer type at the specified cell position
	/// </summary>
	/// <param name="cellPosition">cell position within the tilemap to get the tile of. NOT the same
	/// as world position.</param>
	/// <param name="layerType"></param>
	/// <returns></returns>
	public LayerTile GetTile(Vector3Int cellPosition, LayerType layerType)
	{
		Layer layer = null;
		Layers.TryGetValue(layerType, out layer);
		return layer ? Layers[layerType].GetTile(cellPosition) : null;
	}

	/// <summary>
	/// Gets the topmost tile at the specified cell position
	/// </summary>
	/// <param name="cellPosition">cell position within the tilemap to get the tile of. NOT the same
	/// as world position.</param>
	/// <returns></returns>
	public LayerTile GetTile(Vector3Int cellPosition)
	{
		for (var i = 0; i < LayersValues.Length; i++)
		{
			LayerTile tile = LayersValues[i].GetTile(cellPosition);
			if (tile != null)
			{
				return tile;
			}
		}

		return null;
	}

	/// <summary>
	/// Checks if tile is empty of objects (only solid by default)
	/// </summary>
	/// <param name="includingPassable">If true, checks for non-solid items, too</param>
	public bool IsEmptyAt( Vector3Int position, bool isServer, bool includingPassable = false )
	{
		for (var index = 0; index < LayersKeys.Length; index++)
		{
			LayerType layer = LayersKeys[index];
			if (layer != LayerType.Objects && HasTile(position, layer, isServer))
			{
				return false;
			}

			if (layer == LayerType.Objects)
			{
				foreach (RegisterTile o in isServer
					? ((ObjectLayer) LayersValues[index]).ServerObjects.Get(position)
					: ((ObjectLayer) LayersValues[index]).ClientObjects.Get(position))
				{
					if (!o.IsPassable(isServer) || includingPassable)
					{
						return false;
					}
				}
			}
		}

		return true;
	}

	public bool IsNoGravityAt(Vector3Int position, bool isServer)
	{
		for (var i = 0; i < LayersKeys.Length; i++)
		{
			LayerType layer = LayersKeys[i];
			if (layer != LayerType.Objects && HasTile(position, layer, isServer))
			{
				return false;
			}

			if (layer == LayerType.Objects)
			{
				foreach (RegisterTile o in isServer
					? ((ObjectLayer) LayersValues[i]).ServerObjects.Get(position)
					: ((ObjectLayer) LayersValues[i]).ClientObjects.Get(position))
				{
					if (o is RegisterObject)
					{
						PushPull pushPull = o.GetComponent<PushPull>();
						if (!pushPull)
						{
							return o.IsPassable(isServer);
						}

						if (pushPull.isNotPushable)
						{
							return false;
						}
					}
				}
			}
		}

		return true;
	}

	public bool IsEmptyAt(GameObject[] context, Vector3Int position, bool isServer)
	{
		for (var i1 = 0; i1 < LayersKeys.Length; i1++)
		{
			LayerType layer = LayersKeys[i1];
			if (layer != LayerType.Objects && HasTile(position, layer, isServer))
			{
				return false;
			}

			if (layer == LayerType.Objects)
			{
				foreach (RegisterTile o in isServer
					? ((ObjectLayer) LayersValues[i1]).ServerObjects.Get(position)
					: ((ObjectLayer) LayersValues[i1]).ClientObjects.Get(position))
				{
					if (!o.IsPassable(isServer))
					{
						bool isExcluded = false;
						for (var index = 0; index < context.Length; index++)
						{
							if (o.gameObject == context[index])
							{
								isExcluded = true;
								break;
							}
						}

						if (!isExcluded)
						{
							return false;
						}
					}
				}
			}
		}

		return true;
	}

	/// <summary>
	/// Cheap method to check if there's a tile
	/// </summary>
	/// <param name="position"></param>
	/// <returns></returns>
	public bool HasTile(Vector3Int position, bool isServer)
	{
		for (var i = 0; i < LayersValues.Length; i++)
		{
			if (LayersValues[i].HasTile(position, isServer))
			{
				return true;
			}
		}
		return false;
	}
	public bool HasTile(Vector3Int position, LayerType layerType, bool isServer)
	{
		//protection against nonexistent layers
		for ( var i = 0; i < LayersKeys.Length; i++ )
		{
			if ( layerType == LayersKeys[i] )
			{
				return Layers[layerType].HasTile( position, isServer );
			}
		}

		return false;
	}

	public void RemoveTile(Vector3Int position, LayerType refLayer)
	{
		for (var i = 0; i < LayersValues.Length; i++)
		{
			Layer layer = LayersValues[i];
			if (layer.LayerType < refLayer &&
			    !(refLayer == LayerType.Objects &&
			      layer.LayerType == LayerType.Floors) &&
			    refLayer != LayerType.Grills)
			{
				layer.RemoveTile(position);
			}
		}
	}

	public void RemoveTile(Vector3Int position, LayerType refLayer, bool removeAll = false)
	{
		Layers[refLayer].RemoveTile(position, removeAll);
	}

	public void ClearAllTiles()
	{
		for (var i = 0; i < LayersValues.Length; i++)
		{
			LayersValues[i].ClearAllTiles();
		}
	}

	public Vector3 LocalToWorld( Vector3 localPos ) => LayersValues[0].LocalToWorld( localPos );
	public Vector3 CellToWorld( Vector3Int cellPos ) => LayersValues[0].CellToWorld( cellPos );
	public Vector3 WorldToLocal( Vector3 worldPos ) => LayersValues[0].WorldToLocal( worldPos );

	public BoundsInt GetWorldBounds()
	{
		var bounds = GetBounds();
		//???
		var min = CellToWorld( bounds.min ).RoundToInt();
		var max = CellToWorld( bounds.max ).RoundToInt();
		return new BoundsInt(min, max - min);
	}

	public BoundsInt GetBounds()
	{
		Vector3Int minPosition = Vector3Int.one * int.MaxValue;
		Vector3Int maxPosition = Vector3Int.one * int.MinValue;

		for (var i = 0; i < LayersValues.Length; i++)
		{
			BoundsInt layerBounds = LayersValues[i].Bounds;

			minPosition = Vector3Int.Min(layerBounds.min, minPosition);
			maxPosition = Vector3Int.Max(layerBounds.max, maxPosition);
		}

		return new BoundsInt(minPosition, maxPosition - minPosition);
	}

	public Vector3Int WorldToCell(Vector3 worldPosition)
	{
		return LayersValues[0].WorldToCell(worldPosition);
	}


#if UNITY_EDITOR
	public void SetPreviewTile(Vector3Int position, LayerTile tile, Matrix4x4 transformMatrix)
	{
		for (var i = 0; i < LayersValues.Length; i++)
		{
			Layer layer = LayersValues[i];
			if (layer.LayerType < tile.LayerType)
			{
				Layers[layer.LayerType].SetPreviewTile(position, LayerTile.EmptyTile, Matrix4x4.identity);
			}
		}

		if (!Layers.ContainsKey(tile.LayerType))
		{
			Logger.LogErrorFormat($"LAYER TYPE: {0} not found!", Category.TileMaps, tile.LayerType);
			return;
		}

		Layers[tile.LayerType].SetPreviewTile(position, tile, transformMatrix);
	}

	public void ClearPreview()
	{
		for (var i = 0; i < LayersValues.Length; i++)
		{
			LayersValues[i].ClearPreview();
		}
	}
#endif
}