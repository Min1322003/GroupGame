# NPC System Setup Guide

This guide explains how to set up the new NPC system that spawns enemies outside the play circle, walks toward the player, and explodes into 8 projectiles.

## Components

### 1. **NPC.cs**
The individual NPC enemy that:
- Walks toward the player at a configurable speed
- Explodes when reaching a certain distance
- Spawns 8 projectiles in a circular pattern
- Uses the existing Projectile system for explosions

### 2. **NPCSpawner.cs**
The spawner manager that:
- Spawns NPCs periodically outside the boundary circle
- Limits the number of active NPCs
- Tracks NPC lifecycle

## Setup Instructions

### Step 1: Create NPC Prefab

1. **Create a new GameObject** in your scene and name it `NPC_Enemy`
2. **Add Components:**
   - Rigidbody2D (not kinematic)
   - CircleCollider2D (set as trigger)
   - Sprite Renderer (assign a sprite for the enemy)
   - NetworkObject
   - NetworkTransform (if needed for client syncing)
   - **NPC.cs** script

3. **Configure Rigidbody2D:**
   - Body Type: Dynamic
   - Gravity Scale: 0
   - Constraints: Freeze Rotation Z
   - Collision Detection: Continuous

4. **Configure CircleCollider2D:**
   - Is Trigger: ✓ (checked)
   - Radius: ~0.25

5. **Configure NPC.cs in Inspector:**
   - Move Speed: 3 (adjust as needed)
   - Explosion Distance: 1.5 (distance from player to trigger explosion)
   - Projectile Count: 8
   - Projectile Speed: 10
   - Projectile Damage: 5
   - Projectile Lifetime: 3
   - **Projectile Prefab: Assign your bullet prefab** (the same one used by the pistol)

6. **Save as Prefab:** Drag the NPC_Enemy GameObject into your Assets/Resources or Assets folder to create a prefab

### Step 2: Setup Spawner

1. **Create a new GameObject** in your scene and name it `NPC_Spawner`
2. **Add Components:**
   - NetworkObject
   - **NPCSpawner.cs** script

3. **Configure NPCSpawner.cs in Inspector:**
   - NPC Prefab: Assign the NPC prefab you created
   - Spawn Interval: 2 (seconds between spawns)
   - Max NPCs Active: 10 (adjust difficulty)
   - **Boundary Radius: Match your player's boundaryRadius** (from PlayerController)
   - Spawn Distance From Boundary: 1 (distance outside circle to spawn)

### Step 3: Wire Up Player Reference

The NPCSpawner automatically finds the player in the scene using `FindObjectOfType<PlayerController>()`. Make sure:
- Your player has the **PlayerController.cs** script attached
- The player's **NetworkObject** is spawned on the network

## Gameplay Features

### Enemy Behavior
- NPCs spawn randomly outside the play circle
- They walk toward the player
- When within `explosionDistance`, they explode into 8 projectiles
- Projectiles shoot outward in all directions (circle pattern)

### Player Interaction
- When player gets hit by NPC projectiles, they take damage and get knockback
- Same effect as bullet projectiles (can be configured to match exactly)
- Accumulated damage increases knockback force

### Difficulty Tuning

**Easy Mode:**
- Spawn Interval: 3-4 seconds
- Max NPCs: 5
- Explosion Distance: 2.5
- Projectile Damage: 3

**Normal Mode (Current):**
- Spawn Interval: 2 seconds
- Max NPCs: 10
- Explosion Distance: 1.5
- Projectile Damage: 5

**Hard Mode:**
- Spawn Interval: 1 second
- Max NPCs: 15
- Explosion Distance: 1
- Projectile Damage: 8

## Troubleshooting

### NPCs not spawning
- Check that NPCSpawner is set to **IsServer** compatible
- Verify NPC Prefab is assigned
- Check Console for errors about missing projectilePrefab

### NPCs not moving
- Ensure Rigidbody2D is **not kinematic** and gravity scale is 0
- Check that player has NetworkObject and is spawned

### Projectiles not spawning on explosion
- Verify projectilePrefab is assigned to NPC.cs
- Ensure projectilePrefab has NetworkObject component
- Check that projectilePrefab has Projectile.cs script

### Explosion not triggering
- Check explosionDistance value (should be reasonable for player speed)
- Verify CircleCollider2D is set as trigger
- Confirm NPC Rigidbody2D is Dynamic

## Network Considerations

- **NPCSpawner runs only on Server**
- **NPC movement is server-authoritative**
- **Projectile spawning from explosion uses ServerRpc**
- All clients see synchronized NPC movement and explosions
- Works in both single-player and multiplayer modes

## Future Enhancements

You could extend this system with:
- Different NPC types with different behaviors
- Health system for NPCs (can be destroyed before explosion)
- Special explosion effects (particles, sounds)
- Waves of NPCs with increasing difficulty
- NPC targeting other NPCs
- Path-following instead of straight-line movement
