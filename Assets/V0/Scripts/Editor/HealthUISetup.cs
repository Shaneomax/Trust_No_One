using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using StarterAssets;
using TrustNoOne.AI;
using V0.Player;
using V0.UI;

namespace V0.Editor
{
    public static class HealthUISetup
    {
        [MenuItem("Tools/Setup Player Health & Damage UI", false, 57)]
        [MenuItem("Tools/Trust No One/Setup Player Health & Damage UI", false, 57)]
        public static void SetupHealthAndUI()
        {
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Setup Player Health & Damage UI");

            // 1. Ensure SoftVignette sprite exists
            Sprite vignetteSprite = EnsureVignetteSprite();

            // 2. Attach PlayerHealth to PlayerCapsule
            FirstPersonController fpc = Object.FindFirstObjectByType<FirstPersonController>();
            if (fpc != null)
            {
                PlayerHealth ph = fpc.GetComponent<PlayerHealth>();
                if (ph == null)
                {
                    ph = Undo.AddComponent<PlayerHealth>(fpc.gameObject);
                }
                EditorUtility.SetDirty(fpc.gameObject);
            }

            // 3. Attach EnemyHealth to Ghost
            EnemyAI ghost = Object.FindFirstObjectByType<EnemyAI>();
            if (ghost != null)
            {
                EnemyHealth eh = ghost.GetComponent<EnemyHealth>();
                if (eh == null)
                {
                    eh = Undo.AddComponent<EnemyHealth>(ghost.gameObject);
                }
                EditorUtility.SetDirty(ghost.gameObject);
            }

            // 4. Create/Configure DamageIndicatorCanvas
            GameObject canvasObj = GameObject.Find("DamageIndicatorCanvas");
            if (canvasObj == null)
            {
                canvasObj = new GameObject("DamageIndicatorCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup), typeof(DamageIndicatorUI));
                Undo.RegisterCreatedObjectUndo(canvasObj, "Create DamageIndicatorCanvas");
            }

            Canvas canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 85;

            CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            DamageIndicatorUI damageUI = canvasObj.GetComponent<DamageIndicatorUI>();
            if (damageUI == null) damageUI = canvasObj.AddComponent<DamageIndicatorUI>();

            // 5. Damage Flash Vignette
            GameObject flashObj = GetOrCreateChild(canvasObj, "DamageFlash");
            CanvasGroup flashGroup = flashObj.GetComponent<CanvasGroup>() ?? flashObj.AddComponent<CanvasGroup>();
            flashGroup.alpha = 0f;
            flashGroup.blocksRaycasts = false;
            flashGroup.interactable = false;
            Image flashImg = flashObj.GetComponent<Image>() ?? flashObj.AddComponent<Image>();
            flashImg.sprite = vignetteSprite;
            flashImg.color = new Color(0.85f, 0.05f, 0.05f, 0.75f);
            flashImg.raycastTarget = false;
            SetFullScreenRect(flashObj.GetComponent<RectTransform>());

            // 6. Low Health Vignette
            GameObject lowHealthObj = GetOrCreateChild(canvasObj, "LowHealthVignette");
            CanvasGroup lowHealthGroup = lowHealthObj.GetComponent<CanvasGroup>() ?? lowHealthObj.AddComponent<CanvasGroup>();
            lowHealthGroup.alpha = 0f;
            lowHealthGroup.blocksRaycasts = false;
            lowHealthGroup.interactable = false;
            Image lowHealthImg = lowHealthObj.GetComponent<Image>() ?? lowHealthObj.AddComponent<Image>();
            lowHealthImg.sprite = vignetteSprite;
            lowHealthImg.color = new Color(0.65f, 0.02f, 0.02f, 0.6f);
            lowHealthImg.raycastTarget = false;
            SetFullScreenRect(lowHealthObj.GetComponent<RectTransform>());

            // 7. Health Bar HUD (Bottom-Left Corner)
            GameObject hudContainer = GetOrCreateChild(canvasObj, "HealthBarHUD");
            RectTransform hudRect = hudContainer.GetComponent<RectTransform>();
            hudRect.anchorMin = new Vector2(0f, 0f);
            hudRect.anchorMax = new Vector2(0f, 0f);
            hudRect.pivot = new Vector2(0f, 0f);
            hudRect.anchoredPosition = new Vector2(40f, 40f);
            hudRect.sizeDelta = new Vector2(220f, 32f);

            CanvasGroup hudGroup = hudContainer.GetComponent<CanvasGroup>() ?? hudContainer.AddComponent<CanvasGroup>();
            hudGroup.alpha = 0.85f;
            hudGroup.blocksRaycasts = false;
            hudGroup.interactable = false;

            // Background Bar
            GameObject bgObj = GetOrCreateChild(hudContainer, "Background");
            RectTransform bgRect = bgObj.GetComponent<RectTransform>();
            SetFullScreenRect(bgRect);
            Image bgImg = bgObj.GetComponent<Image>() ?? bgObj.AddComponent<Image>();
            bgImg.color = new Color(0.08f, 0.08f, 0.08f, 0.8f);
            bgImg.raycastTarget = false;

            // Fill Bar
            GameObject fillObj = GetOrCreateChild(hudContainer, "Fill");
            RectTransform fillRect = fillObj.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = new Vector2(3f, 3f);
            fillRect.offsetMax = new Vector2(-3f, -3f);
            Image fillImg = fillObj.GetComponent<Image>() ?? fillObj.AddComponent<Image>();
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillOrigin = 0;
            fillImg.fillAmount = 1f;
            fillImg.color = new Color(0.2f, 0.8f, 0.35f, 0.95f);
            fillImg.raycastTarget = false;

            // Health Text
            GameObject textObj = GetOrCreateChild(hudContainer, "HealthText");
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            SetFullScreenRect(textRect);
            Text hpText = textObj.GetComponent<Text>() ?? textObj.AddComponent<Text>();
            hpText.text = "100 HP";
            hpText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hpText.fontSize = 15;
            hpText.fontStyle = FontStyle.Bold;
            hpText.alignment = TextAnchor.MiddleCenter;
            hpText.color = Color.white;
            hpText.raycastTarget = false;

            // Wire Serialized Properties
            SerializedObject so = new SerializedObject(damageUI);
            so.FindProperty("_damageFlashCanvasGroup").objectReferenceValue = flashGroup;
            so.FindProperty("_damageFlashImage").objectReferenceValue = flashImg;
            so.FindProperty("_lowHealthVignetteCanvasGroup").objectReferenceValue = lowHealthGroup;
            so.FindProperty("_lowHealthVignetteImage").objectReferenceValue = lowHealthImg;
            so.FindProperty("_healthBarCanvasGroup").objectReferenceValue = hudGroup;
            so.FindProperty("_healthFillImage").objectReferenceValue = fillImg;
            so.FindProperty("_healthText").objectReferenceValue = hpText;
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(canvasObj);
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
            Undo.CollapseUndoOperations(undoGroup);

            Debug.Log("<color=green><b>[HealthUISetup]</b></color> Successfully configured Player Health & Damage UI!");
        }

        private static GameObject GetOrCreateChild(GameObject parent, string name)
        {
            Transform child = parent.transform.Find(name);
            if (child != null) return child.gameObject;

            GameObject newObj = new GameObject(name, typeof(RectTransform));
            newObj.transform.SetParent(parent.transform, false);
            return newObj;
        }

        private static void SetFullScreenRect(RectTransform rt)
        {
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        private static Sprite EnsureVignetteSprite()
        {
            string dirPath = "Assets/V0/Textures/UI";
            string assetPath = $"{dirPath}/SoftVignette.png";

            Sprite loaded = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (loaded != null) return loaded;

            if (!Directory.Exists(dirPath)) Directory.CreateDirectory(dirPath);

            int width = 512;
            int height = 512;
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);

            Vector2 center = new Vector2(width * 0.5f, height * 0.5f);
            float maxRadius = Mathf.Sqrt(center.x * center.x + center.y * center.y);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float normalizedDist = dist / maxRadius;
                    float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1f, normalizedDist));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            byte[] pngData = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            File.WriteAllBytes(assetPath, pngData);

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }
    }
}
