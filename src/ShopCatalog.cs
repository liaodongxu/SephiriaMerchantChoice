using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Configuration;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

using SephiriaSharedFavorites;

namespace SephiriaMerchantChoice
{
    // The catalog only selects the contents of native replenishment slots. Purchases,
    // payment, inventory checks and the purchased flag remain owned by the game.
    internal static class ShopCatalog
    {
        internal static ConfigEntry<bool> Enabled;
        internal static bool Active { get { return Enabled != null && Enabled.Value; } }
        internal static readonly MethodInfo RefreshNative = AccessTools.Method(typeof(UI_ShopPanel), "UpdateReplenishmentIcon");

        internal static void Message(string text)
        {
            UIManager.Instance.GetElement<UI_SystemMessage>().Open(text, 3f);
        }

        internal static int Bought(UnitAI_NewBasic shop, EItemType type)
        {
            int count = 0;
            foreach (UnitAI_NewBasic.ReplenishmentItem slot in shop.replenishments)
            {
                ItemEntity item = slot == null ? null : ItemDatabase.FindItemById(slot.entityID);
                if (slot != null && slot.purchased && item != null && item.type == type) count++;
            }
            return count;
        }

        internal static int Remaining(UnitAI_NewBasic shop, EItemType type)
        {
            return Math.Max(0, (type == EItemType.Charm ? 2 : 1) - Bought(shop, type));
        }

        internal static bool Eligible(ItemEntity item, PlayerAvatar buyer, UnitAI_NewBasic shop)
        {
            if (item == null || buyer == null || buyer.spawner == null || buyer.Inventory == null) return false;
            // Rerolls can select Common through Legend, never Eternal/debug rewards.
            if (item.rarity < EItemRarity.Common || item.rarity > EItemRarity.Legend) return false;
            if (item.type == EItemType.StoneTablet)
                return buyer.GetCustomStat(ECustomStat.TABLET) > 0 && buyer.spawner.unlockedStoneTablets.Contains(item.id);
            if (item.type != EItemType.Charm || item.cannotBeReward || !buyer.spawner.unlockedCharms.Contains(item.id)) return false;
            if (item.isDual)
            {
                foreach (string category in item.categories)
                {
                    ComboEffectBase combo = buyer.Inventory.FindComboEffect(category);
                    if (combo == null || !combo.isEnabled || combo.lastAppliedComboEffectCount <= 0) return false;
                }
            }
            Charm_Basic charm = item.resourcePrefab == null ? null : item.resourcePrefab.GetComponent<Charm_Basic>();
            if (charm == null || buyer.Inventory.GetItemDropWeight(item) <= 0) return false;
            sbyte x, y, quantity;
            if (charm.isUniqueEffect)
            {
                foreach (ItemEntity connected in charm.connectedUniqueItems)
                    if (buyer.Inventory.HasItem(connected, out x, out y, out quantity)) return false;
            }
            if (charm.isWeaponRelatedCharm)
            {
                WeaponControllerSimple weapon = buyer.GetComponent<WeaponControllerSimple>();
                if (weapon == null || weapon.currentWeapon == null || charm.relatedWeapon != weapon.currentWeapon.weaponType) return false;
            }
            return (shop.CurrentSelling == null || !shop.CurrentSelling.HasItem(item, out x, out y, out quantity))
                && !buyer.Inventory.HasItem(item, out x, out y, out quantity);
        }

        internal static List<ItemEntity> GetItems(UI_ShopPanel panel)
        {
            List<ItemEntity> items = new List<ItemEntity>();
            PlayerAvatar buyer = panel.BuyerCharacter as PlayerAvatar;
            UnitAI_NewBasic shop = panel.ShopCharacter.GetComponent<UnitAI_NewBasic>();
            foreach (int id in ItemDatabase.GetAllItemID())
            {
                ItemEntity item = ItemDatabase.FindItemById(id);
                if (Eligible(item, buyer, shop)) items.Add(item);
            }
            return items;
        }

        internal static bool CanBuy(UnitAvatar buyer, UnitAI_NewBasic shop, int idx)
        {
            if (!Active || shop == null || idx < 0 || idx >= shop.replenishments.Count) return true;
            UnitAI_NewBasic.ReplenishmentItem slot = shop.replenishments[idx];
            ItemEntity item = slot == null ? null : ItemDatabase.FindItemById(slot.entityID);
            if (item == null || slot.purchased) return true; // Native validation handles these.
            if ((item.type == EItemType.Charm || item.type == EItemType.StoneTablet) && Remaining(shop, item.type) == 0)
            {
                Message(item.type == EItemType.Charm ? "这位商人的 2 件神器补货名额已用完。" : "这位商人的 1 件石板补货名额已用完。");
                return false;
            }
            NewItemOwnInstance voucher;
            int cost = ItemDatabase.GetItemBuyPrice(item, shop.Avatar.GetCustomStat(ECustomStat.Negotiation), buyer.GetCustomStat(ECustomStat.Negotiation));
            if (buyer.Money < cost && (buyer.Inventory == null || !buyer.Inventory.TryGetTradeVoucher(out voucher)))
            {
                Message("金币不足，无法购买。");
                return false;
            }
            return true;
        }

        internal static void Open(UI_ShopPanel panel)
        {
            if (!panel.IsOpened || panel.ShopCharacter == null || GameCamera.Instance.Observer != panel.BuyerCharacter) return;
            if (UIManager.Instance.GetElement<UI_NewItemPicker>().CurrentAny
                || UIManager.Instance.GetElement<UI_NewItemPicker_Controller>().CurrentAny) return;
            UnitAI_NewBasic shop = panel.ShopCharacter.GetComponent<UnitAI_NewBasic>();
            if (shop == null || panel.BuyerCharacter.GetCustomStatUnsafe("REPLENISHMENT") <= 0) return;
            List<ItemEntity> items = GetItems(panel);
            if (items.Count == 0) { Message("当前没有符合补货条件的已解锁商品。"); return; }
            CatalogWindow window = panel.GetComponent<CatalogWindow>();
            if (window == null) window = panel.gameObject.AddComponent<CatalogWindow>();
            // Construct the UI before charging. Merely closing/reopening it never resets slots.
            window.Prepare(panel, items);
            if (shop.replenishmentTryCount == 0)
            {
                PlayerLocalDataStorage storage = panel.BuyerCharacter.GetComponent<PlayerLocalDataStorage>();
                if (storage == null || storage.GetSapphire() < 2) { Message("首次打开自选补货需要 2 颗蓝宝石。"); return; }
                storage.sapphireUseInRun += 2;
                shop.replenishmentTryCount = 1;
            }
            window.Show();
            panel.replenishmentCostText.text = "0";
            Plugin.LogSource.LogInfo("Merchant catalog opened: eligible=" + items.Count + ", remaining="
                + Remaining(shop, EItemType.Charm) + "/" + Remaining(shop, EItemType.StoneTablet) + ".");
        }
    }

    [HarmonyPatch(typeof(UI_ShopPanel), "DoReplenishment")]
    internal static class CatalogOpenPatch
    {
        private static bool Prefix(UI_ShopPanel __instance)
        {
            if (!ShopCatalog.Active) return true;
            try { ShopCatalog.Open(__instance); }
            catch (Exception ex)
            {
                Plugin.LogSource.LogError("Merchant catalog could not open: " + ex);
                ShopCatalog.Message("自选补货打开失败，请查看日志。");
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(UI_ShopPanel), "UpdateReplenishmentCost")]
    internal static class CatalogCostPatch
    {
        private static void Postfix(UI_ShopPanel __instance)
        {
            if (!ShopCatalog.Active || __instance.ShopCharacter == null) return;
            UnitAI_NewBasic shop = __instance.ShopCharacter.GetComponent<UnitAI_NewBasic>();
            if (shop != null && shop.replenishmentTryCount > 0) __instance.replenishmentCostText.text = "0";
        }
    }

    // Recheck at the actual transaction, after the confirmation dialog. Native purchased
    // flags enforce the cap for all entry paths, including right-click and the sub-bag.
    [HarmonyPatch]
    internal static class CatalogPurchaseLimitPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(UnitAvatar), "BuyReplenishmentFromShop");
            yield return AccessTools.Method(typeof(UnitAvatar), "BuyReplenishmentFromShopToSubBag");
        }
        private static bool Prefix(UnitAvatar __instance, UnitAI_NewBasic shop, int idx)
        {
            return ShopCatalog.CanBuy(__instance, shop, idx);
        }
    }

    [HarmonyPatch(typeof(UI_ShopPanel), "OnClosed")]
    internal static class CatalogShopClosedPatch
    {
        private static void Postfix(UI_ShopPanel __instance)
        {
            CatalogWindow window = __instance.GetComponent<CatalogWindow>();
            if (window != null) window.Hide();
        }
    }

    public sealed class CatalogWindow : MonoBehaviour
    {
        private const int PageSize = 6;
        private UI_ShopPanel panel;
        private GameObject root;
        private RectTransform frame;
        private TMP_FontAsset font;
        private TMP_InputField search;
        private TextMeshProUGUI status, pages, artifactQuota, tabletQuota, emptyState;
        private Sprite surfaceSprite, buttonSprite;
        private Material fontMaterial;
        private Color surfaceTint = Color.white;
        private readonly Color ink = new Color(0.94f, 0.91f, 0.85f);
        private readonly Color muted = new Color(0.68f, 0.65f, 0.69f);
        private readonly Color gold = new Color(1f, 0.81f, 0.40f);
        private int lastMoney = int.MinValue;
        private float nextPriceRefresh;
        private Button previous, next, favoritesButton, artifactsButton, tabletsButton, allButton;
        private List<ItemEntity> items = new List<ItemEntity>();
        private readonly List<ItemEntity> filtered = new List<ItemEntity>();
        private readonly HashSet<int> favorites = new HashSet<int>();
        private readonly List<GameObject> rows = new List<GameObject>();
        private int page, category;
        private bool onlyFavorites, awaitingDialog;
        private float reopenAt;

        internal void Prepare(UI_ShopPanel owner, List<ItemEntity> available)
        {
            panel = owner;
            items = available;
            favorites.Clear();
            foreach (ItemEntity item in items)
                if (item.type == EItemType.Charm && FavoriteSupport.IsFavoriteInActiveSetup(item.id)) favorites.Add(item.id);
            items.Sort(delegate(ItemEntity a, ItemEntity b)
            {
                int result = favorites.Contains(b.id).CompareTo(favorites.Contains(a.id));
                if (result == 0) result = b.rarity.CompareTo(a.rarity);
                if (result == 0) result = string.Compare(a.Name, b.Name, StringComparison.CurrentCulture);
                return result == 0 ? a.id.CompareTo(b.id) : result;
            });
            if (root == null) Build();
            Filter();
        }

        internal void Show()
        {
            root.SetActive(true);
            root.transform.SetAsLastSibling();
            Fit();
            RefreshRows();
        }

        internal void Hide()
        {
            awaitingDialog = false;
            ClearTooltips();
            if (root != null) root.SetActive(false);
        }

        private void Update()
        {
            if (panel == null || !panel.IsOpened || panel.ShopCharacter == null) { Hide(); return; }
            if (awaitingDialog && Time.unscaledTime >= reopenAt && !UIManager.Instance.GetElement<UI_MessageBoxHolder>().IsOpened)
            {
                awaitingDialog = false;
                Prepare(panel, ShopCatalog.GetItems(panel));
                Show();
            }
            if (root != null && root.activeSelf)
            {
                Fit();
                UnitAI_NewBasic shop = panel.ShopCharacter.GetComponent<UnitAI_NewBasic>();
                artifactQuota.text = ShopCatalog.Remaining(shop, EItemType.Charm) + " <size=16>/ 2 件</size>";
                tabletQuota.text = ShopCatalog.Remaining(shop, EItemType.StoneTablet) + " <size=16>/ 1 件</size>";
                status.text = panel.BuyerCharacter.Money.ToString("N0");
                // Keep affordability/negotiation/voucher presentation current without
                // rebuilding rows every frame or changing the native transaction.
                if (lastMoney != panel.BuyerCharacter.Money || Time.unscaledTime >= nextPriceRefresh)
                {
                    foreach (GameObject row in rows)
                        if (row != null) row.GetComponent<CatalogRowState>().Refresh(panel, shop);
                    lastMoney = panel.BuyerCharacter.Money;
                    nextPriceRefresh = Time.unscaledTime + 0.5f;
                }
            }
        }

        private void Fit()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            Camera camera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            RectTransform parent = (RectTransform)root.transform.parent;
            Vector2 lower, upper;
            // Position against the actual screen, not the shop's centered/scaled rect.
            // Leave the rest of the screen available to the native backpack UI.
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, new Vector2(12f, 12f), camera, out lower)
                || !RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,
                    new Vector2(Screen.width * (3f / 7f) - 12f, Screen.height - 12f), camera, out upper)) return;
            RectTransform overlay = (RectTransform)root.transform;
            Vector2 available = upper - lower;
            float fit = Mathf.Min(available.x / 580f, available.y / 800f);
            // The raycast/background rectangle must match the fitted content, not
            // the larger 3/7 bounding box. Anchor the actual panel at the top left.
            overlay.sizeDelta = new Vector2(580f, 800f) * fit;
            overlay.anchoredPosition = new Vector2(lower.x, upper.y - overlay.sizeDelta.y) - parent.rect.min;
            frame.localScale = Vector3.one * fit;
        }

        private void Build()
        {
            font = panel.replenishmentCostText.font;
            fontMaterial = panel.replenishmentCostText.fontSharedMaterial;
            ReadNativeSkin();
            root = new GameObject("RunQoL_MerchantCatalog", typeof(RectTransform), typeof(Image));
            root.SetActive(false);
            root.transform.SetParent(panel.transform, false);
            RectTransform overlay = (RectTransform)root.transform;
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.zero;
            overlay.pivot = Vector2.zero;
            root.GetComponent<Image>().color = new Color(0.055f, 0.04f, 0.065f, 0.98f);
            frame = Rect("CatalogFrame", root.transform, 0, 0, 580, 800);
            // The original large shop sprite contains internal wooden rails.
            // It is not a generic nine-slice border, even though it has borders.
            // Use the clean native button perimeter instead, including for cards.
            Surface(frame, buttonSprite, Color.white, false);
            Text(frame, "商人自选商品", 24, 24, 380, 40, 27);
            MakeButton(frame, "返回商店", 428, 26, 128, 38, Hide);
            artifactQuota = Summary("神器名额", 24, false);
            tabletQuota = Summary("石板名额", 204, false);
            status = Summary("持有金币", 384, true);
            allButton = MakeButton(frame, "全部", 24, 164, 84, 38, delegate { category = 0; page = 0; Filter(); });
            artifactsButton = MakeButton(frame, "神器", 116, 164, 84, 38, delegate { category = 1; page = 0; Filter(); });
            tabletsButton = MakeButton(frame, "石板", 208, 164, 84, 38, delegate { category = 2; page = 0; Filter(); });
            favoritesButton = MakeButton(frame, "只看偏好", 304, 164, 252, 38, delegate { onlyFavorites = !onlyFavorites; page = 0; Filter(); });
            RectTransform inputRect = Rect("Search", frame, 24, 212, 532, 42);
            Image inputBackground = Surface(inputRect, buttonSprite, new Color(0.72f, 0.67f, 0.74f), true);
            search = inputRect.gameObject.AddComponent<TMP_InputField>();
            search.targetGraphic = inputBackground;
            RectTransform viewport = Rect("Text Area", inputRect, 12, 5, 436, 32);
            viewport.gameObject.AddComponent<RectMask2D>();
            TextMeshProUGUI inputText = Text(viewport, "", 0, 0, 436, 32, 18);
            inputText.richText = false;
            search.textViewport = viewport;
            search.textComponent = inputText;
            search.placeholder = Text(viewport, "输入商品名称…", 0, 0, 436, 32, 18);
            search.placeholder.color = muted;
            search.characterLimit = 80;
            search.lineType = TMP_InputField.LineType.SingleLine;
            search.onValueChanged.AddListener(delegate(string value) { page = 0; Filter(); });
            MakeButton(inputRect, "清空", 460, 5, 66, 32, delegate { search.text = ""; });
            emptyState = Text(frame, "暂无符合条件的商品", 44, 414, 492, 110, 21);
            emptyState.textWrappingMode = TextWrappingModes.Normal;
            emptyState.alignment = TextAlignmentOptions.Center;
            emptyState.color = muted;
            previous = MakeButton(frame, "上一页", 24, 734, 124, 42, delegate { page--; RefreshRows(); });
            next = MakeButton(frame, "下一页", 432, 734, 124, 42, delegate { page++; RefreshRows(); });
            pages = Text(frame, "", 156, 734, 268, 42, 16);
            pages.alignment = TextAlignmentOptions.Center;
        }

        private TextMeshProUGUI Summary(string label, float x, bool currency)
        {
            RectTransform box = Rect(label, frame, x, 82, 172, 66);
            Surface(box, buttonSprite, new Color(0.84f, 0.80f, 0.86f), false);
            Text(box, label, 12, 6, 148, 22, 15).color = muted;
            TextMeshProUGUI value = Text(box, "", 12, 29, 148, 30, 25);
            value.color = currency ? gold : ink;
            value.enableAutoSizing = true;
            value.fontSizeMin = 14;
            value.fontSizeMax = 25;
            return value;
        }

        private void ReadNativeSkin()
        {
            // Copy visual assets only, never merchant button listeners or animators.
            // Only nine-sliced sprites qualify for rectangular stretching.
            float largest = 0f;
            foreach (Image image in panel.GetComponentsInChildren<Image>(true))
            {
                if (image.sprite == null || image.sprite.border.sqrMagnitude == 0 || image.type != Image.Type.Sliced) continue;
                Rect rect = image.rectTransform.rect;
                if (rect.width < 180 || rect.height < 100) continue;
                float area = rect.width * rect.height;
                if (area > largest)
                {
                    largest = area;
                    surfaceSprite = image.sprite;
                    surfaceTint = image.color;
                    surfaceTint.a = 1;
                }
            }
            Button native = panel.replenishmentButton == null ? null : panel.replenishmentButton.GetComponentInChildren<Button>(true);
            Image nativeImage = native == null ? null : native.targetGraphic as Image;
            if (nativeImage != null && nativeImage.sprite != null && nativeImage.sprite.border.sqrMagnitude > 0)
                buttonSprite = nativeImage.sprite;
            // Do not fall back to the decorative shop sprite: it has internal rails.
            Plugin.LogSource.LogInfo("Catalog native skin: frame=" + (surfaceSprite == null ? "fallback" : surfaceSprite.name)
                + ", button=" + (buttonSprite == null ? "fallback" : buttonSprite.name));
        }

        private Image Surface(RectTransform target, Sprite sprite, Color color, bool raycast)
        {
            Image image = target.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = sprite != null && sprite.border.sqrMagnitude > 0 ? Image.Type.Sliced : Image.Type.Simple;
            image.color = sprite == null ? new Color(0.17f * color.r, 0.12f * color.g, 0.19f * color.b, 1) : color;
            image.raycastTarget = raycast;
            return image;
        }

        private void Filter()
        {
            filtered.Clear();
            string query = search == null ? "" : search.text.Trim();
            foreach (ItemEntity item in items)
            {
                if (category == 1 && item.type != EItemType.Charm) continue;
                if (category == 2 && item.type != EItemType.StoneTablet) continue;
                if (onlyFavorites && !favorites.Contains(item.id)) continue;
                if (query.Length > 0 && item.Name.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) < 0) continue;
                filtered.Add(item);
            }
            RefreshRows();
        }

        private void ClearTooltips()
        {
            foreach (GameObject row in rows)
                if (row != null) row.GetComponentInChildren<CatalogItemTooltip>(true).StopShowing();
        }

        private void RefreshRows()
        {
            if (root == null) return;
            ClearTooltips();
            foreach (GameObject row in rows) { row.SetActive(false); UnityEngine.Object.Destroy(row); }
            rows.Clear();
            int pageCount = Math.Max(1, (filtered.Count + PageSize - 1) / PageSize);
            page = Math.Max(0, Math.Min(page, pageCount - 1));
            pages.text = (page + 1) + " / " + pageCount + " 页 · " + filtered.Count + " 件";
            emptyState.gameObject.SetActive(filtered.Count == 0);
            previous.interactable = page > 0;
            next.interactable = page + 1 < pageCount;
            SetTab(allButton, category == 0);
            SetTab(artifactsButton, category == 1);
            SetTab(tabletsButton, category == 2);
            SetTab(favoritesButton, onlyFavorites);
            UnitAI_NewBasic shop = panel.ShopCharacter.GetComponent<UnitAI_NewBasic>();
            for (int index = page * PageSize; index < Math.Min(filtered.Count, (page + 1) * PageSize); index++)
            {
                ItemEntity item = filtered[index];
                int local = index - page * PageSize;
                RectTransform row = Rect("Item_" + item.id, frame, 24, 272 + local * 72, 532, 68);
                Image rowImage = Surface(row, buttonSprite, new Color(0.80f, 0.75f, 0.83f), true);
                row.gameObject.AddComponent<RectMask2D>();
                // Dedicated hover region stops before the purchase button (x=422).
                // Do not attach tooltip enter/exit handlers to the button's ancestor.
                RectTransform hoverRegion = Rect("ItemDetailsHover", row, 0, 0, 412, 68);
                Image hoverTarget = hoverRegion.gameObject.AddComponent<Image>();
                hoverTarget.color = Color.clear;
                hoverTarget.raycastTarget = true;
                CatalogItemTooltip tooltip = hoverRegion.gameObject.AddComponent<CatalogItemTooltip>();
                tooltip.Item = item;
                tooltip.Panel = panel;
                rows.Add(row.gameObject);
                UI_ReplenishmentIcon template = panel.replenishmentIconPrefab;
                Sprite slotSprite = template == null ? null : item.rarity == EItemRarity.Legend ? template.legendBGSprite
                    : item.rarity == EItemRarity.Rare ? template.rareBGSprite
                    : item.rarity == EItemRarity.Uncommon ? template.uncommonBGSprite : template.commonBGSprite;
                Image slot = Surface(Rect("NativeRaritySlot", row, 10, 8, 52, 52), slotSprite, Color.white, false);
                slot.preserveAspect = true;
                Image icon = Rect("Icon", row, 14, 12, 44, 44).gameObject.AddComponent<Image>();
                icon.sprite = item.icon;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                if (item.type == EItemType.StoneTablet && template != null)
                    icon.material = OptionsBinding.Instance.Options.GetInt("ColorblindMode", 0) == 0 ? template.tabletMaterial : template.tabletMaterial_Colorblind;
                TextMeshProUGUI name = Text(row, item.Name, 76, 7, 336, 29, 20);
                name.color = ItemDatabase.GetColorViaItemRarity(item.rarity);
                name.enableAutoSizing = true;
                name.fontSizeMin = 15;
                name.fontSizeMax = 20;
                name.overflowMode = TextOverflowModes.Ellipsis;
                Text(row, item.type == EItemType.Charm ? "神器" : "石板", 76, 37, 50, 23, 15).color = muted;
                TextMeshProUGUI priceText = Text(row, "", 130, 37, 280, 23, 16);
                Button buy = MakeButton(row, "选购", 422, 17, 98, 36, delegate { Choose(item); });
                CatalogRowState rowState = row.gameObject.AddComponent<CatalogRowState>();
                rowState.Item = item;
                rowState.Price = priceText;
                rowState.Buy = buy;
                rowState.Background = rowImage;
                rowState.RestColor = rowImage.color;
                rowState.Refresh(panel, shop);
                if (favorites.Contains(item.id))
                {
                    GameObject heart = FavoriteSupport.GetOrCreateBadge(icon.transform);
                    heart.SetActive(true);
                }
            }
        }

        private void Choose(ItemEntity item)
        {
            if (awaitingDialog || UIManager.Instance.GetElement<UI_MessageBoxHolder>().IsOpened) return;
            UnitAI_NewBasic shop = panel.ShopCharacter.GetComponent<UnitAI_NewBasic>();
            if (ShopCatalog.Remaining(shop, item.type) == 0) { RefreshRows(); return; }
            if (!ShopCatalog.Eligible(item, panel.BuyerCharacter as PlayerAvatar, shop))
            {
                ShopCatalog.Message("这件商品现在不符合补货条件。");
                Prepare(panel, ShopCatalog.GetItems(panel));
                return;
            }
            int slotIndex = -1;
            for (int i = 0; i < shop.replenishments.Count; i++)
                if (shop.replenishments[i] != null && !shop.replenishments[i].purchased) { slotIndex = i; break; }
            if (slotIndex < 0)
            {
                if (shop.replenishments.Count >= 3) { ShopCatalog.Message("这位商人的补货名额已用完。"); return; }
                slotIndex = shop.replenishments.Count;
                shop.replenishments.Add(new UnitAI_NewBasic.ReplenishmentItem(item.id));
            }
            else shop.replenishments[slotIndex].entityID = item.id;
            ClearTooltips();
            root.SetActive(false);
            ShopCatalog.RefreshNative.Invoke(panel, null);
            panel.BuyReplenishmentItem(shop, slotIndex, -1, -1);
            awaitingDialog = true;
            reopenAt = Time.unscaledTime + 0.2f;
        }

        private void SetTab(Button button, bool active)
        {
            button.GetComponentInChildren<TextMeshProUGUI>().color = active ? gold : muted;
            button.GetComponent<Image>().color = active ? new Color(1f, 0.89f, 0.73f) : new Color(0.70f, 0.65f, 0.74f);
        }

        private static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)obj.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        private TextMeshProUGUI Text(Transform parent, string value, float x, float y, float width, float height, float size)
        {
            TextMeshProUGUI text = Rect("Label", parent, x, y, width, height).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            if (fontMaterial != null) text.fontSharedMaterial = fontMaterial;
            text.fontSize = size;
            text.color = ink;
            text.text = value;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        private Button MakeButton(Transform parent, string title, float x, float y, float width, float height, Action action)
        {
            RectTransform rect = Rect(title, parent, x, y, width, height);
            Image image = Surface(rect, buttonSprite, Color.white, true);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.88f, 0.83f, 0.91f);
            colors.highlightedColor = new Color(1f, 0.95f, 0.77f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(0.68f, 0.57f, 0.70f);
            colors.disabledColor = new Color(0.40f, 0.38f, 0.43f, 0.85f);
            colors.fadeDuration = 0.06f;
            button.colors = colors;
            button.onClick.AddListener(delegate { action(); });
            TextMeshProUGUI label = Text(rect, title, 4, 0, width - 8, height, 17);
            label.alignment = TextAlignmentOptions.Center;
            return button;
        }
    }

    // Visual state only. Native affordability and slot limits are still rechecked
    // by the existing transaction patch, including after confirmation dialogs.
    public sealed class CatalogRowState : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        internal ItemEntity Item;
        internal TextMeshProUGUI Price;
        internal Button Buy;
        internal Image Background;
        internal Color RestColor;
        internal void Refresh(UI_ShopPanel panel, UnitAI_NewBasic shop)
        {
            int price = ItemDatabase.GetItemBuyPrice(Item, shop.Avatar.GetCustomStat(ECustomStat.Negotiation), panel.BuyerCharacter.GetCustomStat(ECustomStat.Negotiation));
            NewItemOwnInstance voucher;
            bool hasVoucher = panel.Buyer != null && panel.Buyer.TryGetTradeVoucher(out voucher);
            bool quota = ShopCatalog.Remaining(shop, Item.type) > 0;
            bool affordable = hasVoucher || panel.BuyerCharacter.Money >= price;
            Price.text = hasVoucher ? "交易券可用 / " + price.ToString("N0") + " 金币" : price.ToString("N0") + " 金币";
            Price.color = affordable ? new Color(1f, 0.81f, 0.40f) : new Color(1f, 0.48f, 0.43f);
            Buy.interactable = quota && affordable;
            TextMeshProUGUI label = Buy.GetComponentInChildren<TextMeshProUGUI>();
            label.text = !quota ? "名额已满" : !affordable ? "金币不足" : "选购";
            label.color = Buy.interactable ? new Color(0.94f, 0.91f, 0.85f) : new Color(0.59f, 0.56f, 0.61f);
        }
        public void OnPointerEnter(PointerEventData data) { Background.color = Color.Lerp(RestColor, Color.white, 0.45f); }
        public void OnPointerExit(PointerEventData data) { Background.color = RestColor; }
    }

    public sealed class CatalogItemTooltip : MonoBehaviour, IUITooltipOpener, IPointerEnterHandler, IPointerExitHandler
    {
        internal ItemEntity Item;
        internal UI_ShopPanel Panel;
        public bool Showing { get; set; }
        public UI_BaseTooltip LastTooltip { get; set; }
        public void OnPointerEnter(PointerEventData data)
        {
            if (Showing || Item == null || Panel == null || !Panel.IsOpened || Panel.ShopCharacter == null || Panel.BuyerCharacter == null) return;
            RectTransform rect = (RectTransform)transform;
            int price = ItemDatabase.GetItemBuyPrice(Item, Panel.ShopCharacter.GetCustomStat(ECustomStat.Negotiation), Panel.BuyerCharacter.GetCustomStat(ECustomStat.Negotiation));
            if (Item.type == EItemType.Charm)
            {
                UI_CharmTooltip tooltip = UIManager.Instance.GetElement<UI_CharmTooltip>();
                tooltip.Open(this, rect, new Vector2(60, -40), Item);
                tooltip.ShowPrice(price.ToString());
                CatalogTooltipRaycastFilter.Attach(tooltip);
            }
            else
            {
                UI_StoneTabletTooltip tooltip = UIManager.Instance.GetElement<UI_StoneTabletTooltip>();
                tooltip.Open(this, rect, new Vector2(60, -40), Item);
                tooltip.ShowPrice(price.ToString());
                CatalogTooltipRaycastFilter.Attach(tooltip);
            }
        }
        internal void StopShowing()
        {
            Showing = false;
            // The tooltip is shared with native inventory/shop UI. Close only ours.
            UI_BaseTooltip tooltip = LastTooltip;
            if (tooltip != null && object.ReferenceEquals(tooltip.Target, this)) tooltip.Close();
        }
        public void OnPointerExit(PointerEventData data) { StopShowing(); }
        private void OnDisable() { StopShowing(); }
    }

    // Scope input passthrough to catalog-owned tooltips; native tooltip keyword
    // buttons retain their original behavior as soon as the shared tooltip changes owner.
    public sealed class CatalogTooltipRaycastFilter : MonoBehaviour, ICanvasRaycastFilter
    {
        private UI_BaseTooltip owner;
        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            return owner == null || !(owner.Target is CatalogItemTooltip);
        }
        internal static void Attach(UI_BaseTooltip tooltip)
        {
            Add(tooltip.gameObject, tooltip);
            // Cover existing nested canvases too. Root filter covers children created later.
            foreach (Graphic graphic in tooltip.GetComponentsInChildren<Graphic>(true))
                Add(graphic.gameObject, tooltip);
        }
        private static void Add(GameObject target, UI_BaseTooltip tooltip)
        {
            CatalogTooltipRaycastFilter filter = target.GetComponent<CatalogTooltipRaycastFilter>();
            if (filter == null) filter = target.AddComponent<CatalogTooltipRaycastFilter>();
            filter.owner = tooltip;
        }
    }
}


