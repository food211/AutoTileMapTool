using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TilemapTools
{
    [CreateAssetMenu(menuName = "Tiles/CreateAutoTerrainTileRuleConfig")]
    public class AutoTerrainTileRuleConfiger : ScriptableObject
    {
        // 表示空白格的瓦片（可选）
        public TileBase emptyMarkerTile;
        
        // 空白瓦片的透明度 (0-1范围)
        [Range(0f, 1f)]
        public float emptyTileAlpha = 1f;

        // 规则定义用的Tilemap Prefab
        public GameObject inputRulesPrefab;    // 输入规则的Tilemap Prefab
        public GameObject outputRulesPrefab;   // 输出规则的Tilemap Prefab

        // 缓存的Tilemap引用
        [NonSerialized] private Tilemap _cachedInputTilemap;
        [NonSerialized] private Tilemap _cachedOutputTilemap;

        // 获取输入规则Tilemap
        public Tilemap GetInputRulesTilemap()
        {
            if (_cachedInputTilemap == null && inputRulesPrefab != null)
            {
                _cachedInputTilemap = inputRulesPrefab.GetComponentInChildren<Tilemap>();
            }
            return _cachedInputTilemap;
        }

        // 获取输出规则Tilemap
        public Tilemap GetOutputRulesTilemap()
        {
            if (_cachedOutputTilemap == null && outputRulesPrefab != null)
            {
                _cachedOutputTilemap = outputRulesPrefab.GetComponentInChildren<Tilemap>();
            }
            return _cachedOutputTilemap;
        }

        [Serializable]
        public class TerrainRule
        {
            // 规则的名称
            public string ruleName;

            // 规则的边界（在规则Tilemap中的位置）
            public BoundsInt bounds;

            // 优先级 - 数字越大优先级越高
            public int priority = 0;
        }

        // 所有定义的规则
        public List<TerrainRule> rules = new List<TerrainRule>();

        // 清除缓存的Tilemap引用，确保在规则Prefab更改时重新获取
        public void ClearCache()
        {
            _cachedInputTilemap = null;
            _cachedOutputTilemap = null;
        }

        // 在OnEnable时重置缓存
        private void OnEnable()
        {
            ClearCache();
        }

        // 在OnValidate时重置缓存，确保在Inspector中修改后能正确更新
        private void OnValidate()
        {
            ClearCache();
            UpdateEmptyTileAlpha();
        }
        
        // 更新空白瓦片的透明度
        public void UpdateEmptyTileAlpha()
        {
            if (emptyMarkerTile != null)
            {
                // 如果emptyMarkerTile是Tile类型，我们可以直接修改其颜色
                if (emptyMarkerTile is Tile tile)
                {
                    Color tileColor = tile.color;
                    tileColor.a = emptyTileAlpha;
                    tile.color = tileColor;
                }
            }
        }
        
        // 设置空白瓦片的透明度
        public void SetEmptyTileAlpha(float alpha)
        {
            emptyTileAlpha = Mathf.Clamp01(alpha);
            UpdateEmptyTileAlpha();
        }
    }
}