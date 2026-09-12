using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.Mono;
using HarmonyLib;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if MERCHANT_CHOICE
using Plugin = SephiriaMerchantChoice.Plugin;
#else
using Plugin = SephiriaShopFavorites.Plugin;
#endif
namespace SephiriaSharedFavorites
{
#if SHOP_FAVORITES
    [HarmonyPatch(typeof(UI_NewInventoryIcon), "UpdateIcon")]
#endif
    internal static class FavoriteSupport
    {
        private const string BadgeObjectName = "RunQoL_FavoriteBadge";

        private static void Postfix(UI_NewInventoryIcon __instance)
        {
            try
            {
                if (__instance == null || __instance.GetComponentInParent<UI_ShopPanel>() == null)
                {
                    return;
                }

                GameObject badge = GetOrCreateBadge(__instance.transform);
                NewItemOwnInstance item = __instance.Item;
                bool favorite = item != null
                    && item.Entity != null
                    && item.Entity.type == EItemType.Charm
                    && IsFavoriteInActiveSetup(item.EntityID);
                badge.SetActive(favorite);
            }
            catch (Exception exception)
            {
                Plugin.LogSource.LogWarning("Failed to refresh a shop favorite heart: " + exception.Message);
            }
        }

        internal static void RefreshShopPanel(UI_ShopPanel panel, string reason)
        {
            if (panel == null || panel.shopInventoryIconList == null)
            {
                return;
            }

            int itemCount = 0;
            int charmCount = 0;
            int favoriteCount = 0;
            for (int i = 0; i < panel.shopInventoryIconList.Count; i++)
            {
                UI_NewInventoryIcon icon = panel.shopInventoryIconList[i];
                if (icon == null)
                {
                    continue;
                }

                GameObject badge = GetOrCreateBadge(icon.transform);
                NewItemOwnInstance item = icon.Item;
                bool favorite = false;
                if (item != null && item.Entity != null)
                {
                    itemCount++;
                    if (item.Entity.type == EItemType.Charm)
                    {
                        charmCount++;
                        favorite = IsFavoriteInActiveSetup(item.EntityID);
                        if (favorite)
                        {
                            favoriteCount++;
                        }
                    }
                }
                badge.SetActive(favorite && icon.gameObject.activeSelf);
                badge.transform.SetAsLastSibling();
            }

            int selectedSlot = SaveManager.Current == null ? -1 : SaveManager.Current.GetInt("Preset_SelectedSlot", 0);
            int configuredCount = CountConfiguredFavorites(selectedSlot);
            Plugin.LogSource.LogInfo("Shop favorite refresh (" + reason + "): shopIcons="
                + panel.shopInventoryIconList.Count + ", items=" + itemCount + ", charms=" + charmCount
                + ", selectedPreset=" + selectedSlot + ", configuredFavorites=" + configuredCount
                + ", matchedFavorites=" + favoriteCount + ".");

            if (reason == "open")
            {
                Plugin.LogSource.LogInfo("Shop charm identities: " + DescribeShopCharms(panel));
                Plugin.LogSource.LogInfo("Configured favorite identities: " + DescribeConfiguredFavorites(selectedSlot));
            }
        }

        internal static GameObject GetOrCreateBadge(Transform parent)
        {
            Transform existing = parent.Find(BadgeObjectName);
            if (existing != null)
            {
                return existing.gameObject;
            }

            GameObject badge = TryCreateNativeFavoriteImage(parent);
            if (badge == null)
            {
                badge = CreateTextFavoriteIcon(parent);
            }

            badge.name = BadgeObjectName;
            PositionBadge(badge.transform as RectTransform);
            badge.transform.SetAsLastSibling();
            return badge;
        }

        internal static bool IsFavoriteInActiveSetup(int itemId)
        {
            int selectedSlot = SaveManager.Current == null ? -1 : SaveManager.Current.GetInt("Preset_SelectedSlot", 0);
            if (IsStoredFavorite(itemId, selectedSlot))
            {
                return true;
            }

            ItemEntity candidate = ItemDatabase.FindItemById(itemId);
            if (candidate == null)
            {
                return false;
            }

            int[] allIds = ItemDatabase.GetAllItemID();
            for (int i = 0; i < allIds.Length; i++)
            {
                int favoriteId = allIds[i];
                if (!IsStoredFavorite(favoriteId, selectedSlot))
                {
                    continue;
                }

                ItemEntity favorite = ItemDatabase.FindItemById(favoriteId);
                if (IsSameArtifactIdentity(candidate, favorite))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsStoredFavorite(int itemId, int selectedSlot)
        {
            if (SaveManager.Current == null)
            {
                return false;
            }
            if (UI_DimensionPocketPanel.IsItemFavorite(itemId))
            {
                return true;
            }
            return selectedSlot >= 0
                && SaveManager.Current.GetBool("Preset_" + selectedSlot + "_Item_Favorite_" + itemId, false);
        }

        private static bool IsSameArtifactIdentity(ItemEntity left, ItemEntity right)
        {
            if (left == null || right == null || left.type != EItemType.Charm || right.type != EItemType.Charm)
            {
                return false;
            }
            if (left.id == right.id)
            {
                return true;
            }
            if (left.aName != null && right.aName != null
                && !string.IsNullOrEmpty(left.aName.key)
                && string.Equals(left.aName.key, right.aName.key, StringComparison.Ordinal))
            {
                return true;
            }
            if (left.resourcePrefab != null && right.resourcePrefab != null
                && (left.resourcePrefab == right.resourcePrefab
                    || string.Equals(left.resourcePrefab.name, right.resourcePrefab.name, StringComparison.Ordinal)))
            {
                return true;
            }
            return !string.IsNullOrEmpty(left.Name)
                && string.Equals(left.Name, right.Name, StringComparison.Ordinal);
        }

        private static int CountConfiguredFavorites(int selectedSlot)
        {
            if (SaveManager.Current == null)
            {
                return 0;
            }

            int count = 0;
            int[] allIds = ItemDatabase.GetAllItemID();
            for (int i = 0; i < allIds.Length; i++)
            {
                int itemId = allIds[i];
                if (IsStoredFavorite(itemId, selectedSlot))
                {
                    count++;
                }
            }
            return count;
        }

        private static string DescribeShopCharms(UI_ShopPanel panel)
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < panel.shopInventoryIconList.Count; i++)
            {
                UI_NewInventoryIcon icon = panel.shopInventoryIconList[i];
                NewItemOwnInstance item = icon == null ? null : icon.Item;
                if (item == null || item.Entity == null || item.Entity.type != EItemType.Charm)
                {
                    continue;
                }
                if (builder.Length > 0)
                {
                    builder.Append(" | ");
                }
                builder.Append(item.EntityID).Append(':').Append(item.Entity.Name);
                if (IsFavoriteInActiveSetup(item.EntityID))
                {
                    builder.Append("[MATCH]");
                }
            }
            return builder.Length == 0 ? "(none)" : builder.ToString();
        }

        private static string DescribeConfiguredFavorites(int selectedSlot)
        {
            StringBuilder builder = new StringBuilder();
            int[] allIds = ItemDatabase.GetAllItemID();
            for (int i = 0; i < allIds.Length; i++)
            {
                int itemId = allIds[i];
                if (!IsStoredFavorite(itemId, selectedSlot))
                {
                    continue;
                }
                ItemEntity item = ItemDatabase.FindItemById(itemId);
                if (item == null || item.type != EItemType.Charm)
                {
                    continue;
                }
                if (builder.Length > 0)
                {
                    builder.Append(" | ");
                }
                builder.Append(itemId).Append(':').Append(item.Name);
            }
            return builder.Length == 0 ? "(none)" : builder.ToString();
        }

        private static GameObject TryCreateNativeFavoriteImage(Transform parent)
        {
            UI_ItemIcon[] itemIcons = Resources.FindObjectsOfTypeAll<UI_ItemIcon>();
            for (int i = 0; i < itemIcons.Length; i++)
            {
                UI_ItemIcon candidate = itemIcons[i];
                if (candidate != null && candidate.favoriteIcon != null)
                {
                    Image sourceImage = candidate.favoriteIcon.GetComponentInChildren<Image>(true);
                    if (sourceImage == null || sourceImage.sprite == null)
                    {
                        continue;
                    }

                    GameObject badge = new GameObject(BadgeObjectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    badge.transform.SetParent(parent, false);
                    Image image = badge.GetComponent<Image>();
                    image.sprite = sourceImage.sprite;
                    image.color = sourceImage.color;
                    image.preserveAspect = true;
                    image.raycastTarget = false;
                    badge.SetActive(false);
                    return badge;
                }
            }

            return null;
        }

        private static GameObject CreateTextFavoriteIcon(Transform parent)
        {
            GameObject badge = new GameObject(BadgeObjectName, typeof(RectTransform), typeof(CanvasRenderer));
            badge.transform.SetParent(parent, false);
            TextMeshProUGUI text = badge.AddComponent<TextMeshProUGUI>();
            text.text = "♥";
            text.fontSize = 24f;
            text.fontStyle = FontStyles.Bold;
            text.color = new Color(1f, 0.28f, 0.48f, 1f);
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return badge;
        }

        private static void PositionBadge(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(-10f, -10f);
            rect.sizeDelta = new Vector2(30f, 30f);
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
        }
    }

}

