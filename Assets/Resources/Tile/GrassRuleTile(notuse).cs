using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "Tiles/GrassRuleTile")]
public class GrassRuleTile : RuleTile<GrassRuleTile.Neighbor> {
    public bool isStone;

    public class Neighbor : RuleTile.TilingRule.Neighbor {
        public const int IsStone = 3;
        public const int IsNotStone = 4;
    }

    public override bool RuleMatch(int neighbor, TileBase tile) {
        switch (neighbor) {
            case Neighbor.IsStone:
                // 检查相邻的tile是否是石头类型
                return tile is GrassRuleTile stoneCheck && stoneCheck.isStone;
            case Neighbor.IsNotStone:
                // 如果相邻的tile不是GrassRuleTile或者不是石头，则返回true
                return !(tile is GrassRuleTile notStoneCheck && notStoneCheck.isStone);
        }
        return base.RuleMatch(neighbor, tile);
    }
}