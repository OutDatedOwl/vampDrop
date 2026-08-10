using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// Adds basic visual representation to spawned rice entities
    /// This runs once after spawning to add renderers
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class RiceVisualizationSystem : SystemBase
    {
        protected override void OnCreate()
        {
            Debug.Log("[RiceVisualizationSystem] Created - will add visuals to rice entities");
        }

        protected override void OnUpdate()
        {
            // This is a temporary approach - normally you'd use hybrid renderer
            // For now, just count and report
        }
    }
    
    /// <summary>
    /// MonoBehaviour to display ECS entity counts on screen
    /// </summary>
    public class EntityCountDisplay : MonoBehaviour
    {
        private GUIStyle _style;
        private EntityQuery _riceQuery;
        private EntityQuery _playerQuery;
        private bool _queriesCreated;

        private void Start()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;
            _riceQuery = world.EntityManager.CreateEntityQuery(
                Unity.Entities.ComponentType.ReadOnly<Rice.RiceEntity>(),
                Unity.Entities.ComponentType.Exclude<Rice.RiceHidden>());
            _playerQuery = world.EntityManager.CreateEntityQuery(typeof(Player.PlayerData));
            _queriesCreated = true;
        }

        private void OnDestroy()
        {
            if (_queriesCreated)
            {
                _riceQuery.Dispose();
                _playerQuery.Dispose();
            }
        }

        void OnGUI()
        {
            if (!_queriesCreated) return;

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label);
                _style.fontSize = 24;
                _style.normal.textColor = Color.white;
                _style.alignment = TextAnchor.UpperLeft;
            }

            int riceCount = _riceQuery.CalculateEntityCount();

            int collectedCount = 0;
            int playerCount = _playerQuery.CalculateEntityCount();
            var world = World.DefaultGameObjectInjectionWorld;
            if (world != null && playerCount >= 1)
            {
                var players = _playerQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
                if (players.Length > 0 && world.EntityManager.Exists(players[0]))
                    collectedCount = world.EntityManager.GetComponentData<Player.PlayerData>(players[0]).RiceCollected;
                players.Dispose();
            }

            GUI.Label(new Rect(10, 10, 500, 30), $"Rice in World: {riceCount}", _style);
            GUI.Label(new Rect(10, 40, 500, 30), $"Rice Collected: {collectedCount}", _style);
        }
    }
}
