using UnityEngine;
using System;


/// 中央事件管理器 - 管理游戏中所有事件的触发和订阅

public static class GameEvents
{
    // 玩家状态相关事件
    public static event Action<PlayerState> OnPlayerStateChanged;
    
    
    // 绳索相关事件
    public static event Action OnRopeShoot;
    public static event Action HookFail;
    public delegate void CanShootRopeEvent(bool canShoot);
    public delegate void SetPlayerBurningEvent(bool isBurning);
    public static event CanShootRopeEvent OnCanShootRopeChanged;
    public static event SetPlayerBurningEvent OnPlayerBurningStateChanged;
    public static event Action<Vector2> OnRopeHooked; // 包含钩点位置
    public static event Action OnRopeReleased;
    public static event Action<float> OnRopeLengthChanged; // 包含新长度
    
    // 碰撞相关事件
    public static event System.Action<int> OnPlayerDamaged;
    
    // 玩家移动相关事件
    public static event Action OnPlayerJump;
    public static event Action<bool> OnPlayerGroundedStateChanged; // true=着地，false=离地
    
    // 定义玩家状态枚举（可以移到单独的文件中）
    public enum PlayerState
    {
        Normal,
        Swinging,
        Frozen,
        Burning,
        Electrified
    }
    
    // 触发事件的方法
    
    // 玩家状态相关
    public static void TriggerPlayerStateChanged(PlayerState newState)
    {
        OnPlayerStateChanged?.Invoke(newState);
    }
    
    // 绳索相关
    public static void TriggerRopeShoot()
    {
        OnRopeShoot?.Invoke();
    }

    public static void TriggerHookFail()
    {
        HookFail?.Invoke();
    }
    
    public static void TriggerRopeHooked(Vector2 hookPosition)
    {
        OnRopeHooked?.Invoke(hookPosition);
    }
    
    public static void TriggerRopeReleased()
    {
        OnRopeReleased?.Invoke();
    }
    
    public static void TriggerRopeLengthChanged(float newLength)
    {
        OnRopeLengthChanged?.Invoke(newLength);
    }

    // 玩家移动相关
    public static void TriggerPlayerJump()
    {
        OnPlayerJump?.Invoke();
    }
    
    public static void TriggerPlayerGroundedStateChanged(bool isGrounded)
    {
        OnPlayerGroundedStateChanged?.Invoke(isGrounded);
    }

    public static void TriggerPlayerDamaged(int damage)
    {
        OnPlayerDamaged?.Invoke(damage);
    }

    public static void TriggerCanShootRopeChanged(bool canShoot)
    {
        OnCanShootRopeChanged?.Invoke(canShoot);
    }
    public static void TriggerSetPlayerBurning(bool isBurning)
    {
        OnPlayerBurningStateChanged?.Invoke(isBurning);
    }
}