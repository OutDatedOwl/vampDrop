using UnityEngine;
using System.Collections.Generic;

namespace Vampire.Helpers
{
    using DropPuzzle;
    
    /// <summary>
    /// Shop system for purchasing goblin and ghoul helpers
    /// Can be integrated into existing shop or used as standalone merchant
    /// </summary>
    public class HelperShop : MonoBehaviour
    {
        [Header("Shop Configuration")]
        public string merchantName = "Granak the Helper Broker";
        public bool autoShowUI = true;
        
        [Header("Helper Prices")]
        public int goblinPrice = 50;
        public int ghoulPrice = 150;
        
        [Header("Stock & Availability")]
        public int maxGoblins = 10;
        public int maxGhouls = 5;
        public bool requireTutorialComplete = true;
        
        [Header("Upgrade Prices")]
        public int efficiencyUpgradeCost = 100;
        public int capacityUpgradeCost = 200;
        public int specialAbilityCost = 300;
        
        [Header("References")]
        public GameObject shopUI;
        public Transform shopUIParent;
        
        // Events
        public System.Action<DropPuzzle.HelperType> OnHelperPurchased;
        public System.Action<string> OnUpgradePurchased;
        
        private DropPuzzle.PlayerDataManager playerData => DropPuzzle.PlayerDataManager.Instance;
        private bool shopOpen = false;
        
        #region Unity Events
        
        private void Start()
        {
            if (shopUI != null)
            {
                shopUI.SetActive(false);
            }
            
            // Debug.Log($"[HelperShop] {merchantName} is ready for business!");
        }
        
        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player") && autoShowUI)
            {
                OpenShop();
            }
        }
        
        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player") && autoShowUI)
            {
                CloseShop();
            }
        }
        
        private void Update()
        {
            if (shopOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                CloseShop();
            }
        }
        
        #endregion
        
        #region Shop Management
        
        public void OpenShop()
        {
            if (!IsShopAvailable())
            {
                ShowUnavailableMessage();
                return;
            }
            
            shopOpen = true;
            
            if (shopUI != null)
            {
                shopUI.SetActive(true);
            }
            
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            
            // Debug.Log($"[HelperShop] Opened {merchantName}'s shop");
        }
        
        public void CloseShop()
        {
            if (!shopOpen) return;
            
            shopOpen = false;
            
            if (shopUI != null)
            {
                shopUI.SetActive(false);
            }
            
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            
            // Debug.Log($"[HelperShop] Closed {merchantName}'s shop");
        }
        
        public bool IsShopAvailable()
        {
            if (playerData == null) return false;
            
            if (requireTutorialComplete && !playerData.TutorialCompleted)
            {
                return false;
            }
            
            return true;
        }
        
        private void ShowUnavailableMessage()
        {
            if (requireTutorialComplete && !playerData.TutorialCompleted)
            {
                // Debug.Log($"[HelperShop] {merchantName}: 'Complete the tutorial first, then come back!'");
            }
        }
        
        #endregion
        
        #region Helper Purchases
        
        public bool PurchaseGoblin()
        {
            return PurchaseHelper(DropPuzzle.HelperType.Goblin, goblinPrice);
        }
        
        public bool PurchaseGhoul()
        {
            return PurchaseHelper(DropPuzzle.HelperType.Ghoul, ghoulPrice);
        }
        
        private bool PurchaseHelper(DropPuzzle.HelperType type, int price)
        {
            if (playerData == null)
            {
                // Debug.LogError("[HelperShop] PlayerData not available");
                return false;
            }
            
            if (playerData.TotalCurrency < price)
            {
                // Debug.Log($"[HelperShop] Not enough currency! Need {price}, have {playerData.TotalCurrency}");
                ShowInsufficientFundsMessage(type, price);
                return false;
            }
            
            if (!CanPurchaseHelper(type))
            {
                // Debug.Log($"[HelperShop] Cannot purchase more {type}s (stock limit reached)");
                return false;
            }
            
            if (playerData.SpendCurrency(price, $"Purchase {type} helper"))
            {
                if (type == DropPuzzle.HelperType.Goblin)
                {
                    playerData.Helpers.ownedGoblins++;
                }
                else if (type == DropPuzzle.HelperType.Ghoul)
                {
                    playerData.Helpers.ownedGhouls++;
                }
                
                playerData.SavePlayerData();
                
                OnHelperPurchased?.Invoke(type);
                
                // Debug.Log($"[HelperShop] Purchased {type} helper! Owned: Goblins={playerData.Helpers.ownedGoblins}, Ghouls={playerData.Helpers.ownedGhouls}");
                return true;
            }
            
            // Debug.LogError($"[HelperShop] Failed to spend currency for {type} purchase");
            return false;
        }
        
        public bool CanPurchaseHelper(DropPuzzle.HelperType type)
        {
            if (playerData == null) return false;
            
            switch (type)
            {
                case DropPuzzle.HelperType.Goblin:
                    return maxGoblins < 0 || playerData.Helpers.ownedGoblins < maxGoblins;
                    
                case DropPuzzle.HelperType.Ghoul:
                    return maxGhouls < 0 || playerData.Helpers.ownedGhouls < maxGhouls;
                    
                default:
                    return false;
            }
        }
        
        public int GetRemainingStock(DropPuzzle.HelperType type)
        {
            if (playerData == null) return 0;
            
            switch (type)
            {
                case DropPuzzle.HelperType.Goblin:
                    return maxGoblins < 0 ? 999 : Mathf.Max(0, maxGoblins - playerData.Helpers.ownedGoblins);
                    
                case DropPuzzle.HelperType.Ghoul:
                    return maxGhouls < 0 ? 999 : Mathf.Max(0, maxGhouls - playerData.Helpers.ownedGhouls);
                    
                default:
                    return 0;
            }
        }
        
        private void ShowInsufficientFundsMessage(DropPuzzle.HelperType type, int price)
        {
            int needed = price - playerData.TotalCurrency;
            // Debug.Log($"[HelperShop] {merchantName}: 'You need {needed} more currency to buy a {type} helper!'");
        }
        
        #endregion
        
        #region Upgrade Purchases
        
        public bool PurchaseEfficiencyUpgrade()
        {
            if (playerData == null || !CanAffordUpgrade(efficiencyUpgradeCost))
                return false;
                
            if (playerData.SpendCurrency(efficiencyUpgradeCost, "Helper efficiency upgrade"))
            {
                playerData.Helpers.ricePerSecond += 0.2f;
                playerData.SavePlayerData();
                
                OnUpgradePurchased?.Invoke("efficiency");
                
                // Debug.Log($"[HelperShop] Purchased efficiency upgrade! New rate: {playerData.Helpers.ricePerSecond:F1} rice/second");
                return true;
            }
            
            return false;
        }
        
        public bool PurchaseCapacityUpgrade()
        {
            if (playerData == null || !CanAffordUpgrade(capacityUpgradeCost))
                return false;
                
            if (playerData.SpendCurrency(capacityUpgradeCost, "Helper capacity upgrade"))
            {
                playerData.Helpers.helperCapacityBonus += 1;
                playerData.SavePlayerData();
                
                OnUpgradePurchased?.Invoke("capacity");
                
                // Debug.Log($"[HelperShop] Purchased capacity upgrade! Max helpers: {playerData.Helpers.GetMaxHelpers()}");
                return true;
            }
            
            return false;
        }
        
        public bool PurchaseSpecialAbility(string abilityType)
        {
            if (playerData == null || !CanAffordUpgrade(specialAbilityCost))
                return false;
                
            if (playerData.SpendCurrency(specialAbilityCost, $"Special ability: {abilityType}"))
            {
                switch (abilityType.ToLower())
                {
                    case "auto_scavenge":
                        playerData.Helpers.hasAutoScavenge = true;
                        // Debug.Log("[HelperShop] Unlocked Auto-Scavenge! Helpers work when offline.");
                        break;
                        
                    case "helper_storage":
                        playerData.Helpers.hasHelperStorage = true;
                        // Debug.Log("[HelperShop] Unlocked Helper Storage! Helpers can store rice before returning.");
                        break;
                        
                    case "call_helpers":
                        playerData.Helpers.canCallHelpers = true;
                        // Debug.Log("[HelperShop] Unlocked Call Helpers! Summon all helpers to your location.");
                        break;
                        
                    default:
                        // Debug.LogWarning($"[HelperShop] Unknown ability type: {abilityType}");
                        return false;
                }
                
                playerData.SavePlayerData();
                OnUpgradePurchased?.Invoke(abilityType);
                return true;
            }
            
            return false;
        }
        
        private bool CanAffordUpgrade(int cost)
        {
            return playerData.TotalCurrency >= cost;
        }
        
        #endregion
        
        #region UI & Information
        
        public HelperShopInfo GetShopInfo()
        {
            var info = new HelperShopInfo();
            
            if (playerData != null)
            {
                info.playerCurrency = playerData.TotalCurrency;
                info.ownedGoblins = playerData.Helpers.ownedGoblins;
                info.ownedGhouls = playerData.Helpers.ownedGhouls;
                info.maxHelpers = playerData.Helpers.GetMaxHelpers();
                info.deployedHelpers = playerData.Helpers.GetDeployedHelperCount();
                info.currentEfficiency = playerData.Helpers.ricePerSecond;
            }
            
            info.goblinPrice = goblinPrice;
            info.ghoulPrice = ghoulPrice;
            info.goblinStock = GetRemainingStock(HelperType.Goblin);
            info.ghoulStock = GetRemainingStock(HelperType.Ghoul);
            info.canPurchaseGoblin = CanPurchaseHelper(HelperType.Goblin);
            info.canPurchaseGhoul = CanPurchaseHelper(HelperType.Ghoul);
            
            info.efficiencyUpgradeCost = efficiencyUpgradeCost;
            info.capacityUpgradeCost = capacityUpgradeCost;
            info.specialAbilityCost = specialAbilityCost;
            
            return info;
        }
        
        public void DEBUG_ShowShopStatus()
        {
            var info = GetShopInfo();
            
            // Debug.Log($"=== {merchantName} Shop Status ===");
            // Debug.Log($"Player Currency: {info.playerCurrency}");
            // Debug.Log($"Owned Helpers: {info.ownedGoblins} Goblins, {info.ownedGhouls} Ghouls");
            // Debug.Log($"Deployed: {info.deployedHelpers}/{info.maxHelpers}");
            // Debug.Log($"Efficiency: {info.currentEfficiency:F1} rice/second");
            // Debug.Log($"Goblin Price: {info.goblinPrice} (Stock: {info.goblinStock})");
            // Debug.Log($"Ghoul Price: {info.ghoulPrice} (Stock: {info.ghoulStock})");
            // Debug.Log("==============================");
        }
        
        #endregion
        
        private void OnDrawGizmos()
        {
            var collider = GetComponent<Collider>();
            if (collider != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.matrix = transform.localToWorldMatrix;
                
                if (collider is BoxCollider box)
                {
                    Gizmos.DrawWireCube(box.center, box.size);
                }
                else if (collider is SphereCollider sphere)
                {
                    Gizmos.DrawWireSphere(sphere.center, sphere.radius);
                }
            }
        }
    }
    
    [System.Serializable]
    public class HelperShopInfo
    {
        public int playerCurrency;
        public int ownedGoblins;
        public int ownedGhouls;
        public int maxHelpers;
        public int deployedHelpers;
        public float currentEfficiency;
        
        public int goblinPrice;
        public int ghoulPrice;
        public int goblinStock;
        public int ghoulStock;
        public bool canPurchaseGoblin;
        public bool canPurchaseGhoul;
        
        public int efficiencyUpgradeCost;
        public int capacityUpgradeCost;
        public int specialAbilityCost;
    }
}