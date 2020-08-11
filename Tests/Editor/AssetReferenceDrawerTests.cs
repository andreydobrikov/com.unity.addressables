using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEditor.AddressableAssets.GUI;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.IMGUI.Controls;
using UnityEditor.U2D;
using UnityEditor.VersionControl;
using UnityEngine.AddressableAssets;
using UnityEngine.U2D;
using Object = UnityEngine.Object;

namespace UnityEditor.AddressableAssets.Tests
{
    public class AssetReferenceDrawerTests : AddressableAssetTestBase
    {
        AssetReferenceDrawer m_AssetReferenceDrawer;

        class TestARDObjectBlank : TestObject
        {
            [SerializeField]
            public AssetReference Reference = new AssetReference();
        }
        class TestARDObject : TestObject
        {
            [SerializeField]
            [AssetReferenceUILabelRestriction(new[] { "HD" })]
            private AssetReference Reference = new AssetReference();
        }

        class TestARDObjectMultipleLabels : TestObject
        {
            [SerializeField]
            [AssetReferenceUILabelRestriction(new[] { "HDR", "test", "default" })]
            private AssetReference ReferenceMultiple = new AssetReference();
        }
        
        class TestSubObjectsSpriteAtlas : TestObject
        {
            [SerializeField]
            public AssetReferenceSprite testSpriteReference;
        }


        class TestAssetReferenceDrawer : AssetReferenceDrawer
        {
            TestAssetReferencePopup _popup;
            internal void SetAssetReference(AssetReference ar)
            {
                m_AssetRefObject = ar;
            }

            internal void TreeSetup(TreeViewState treeState)
            {
                _popup = new TestAssetReferencePopup(this, "testpopup", "");
                _popup.TreeSetup(treeState, this, _popup);
            }

            internal void TreeSelectionChangedHelper(IList<int> selectedIds)
            {
                _popup.TreeSelectionChangedHelper(selectedIds);
            }

            class TestAssetReferencePopup : AssetReferencePopup
            {
                TestSelectionTree _testTree;
                internal TestAssetReferencePopup(AssetReferenceDrawer drawer, string guid, string nonAddressedAsset)
                    : base(drawer, guid, nonAddressedAsset) {}

                internal void TreeSetup(TreeViewState treeState, AssetReferenceDrawer testARD, AssetReferencePopup popup)
                {
                    _testTree = new TestSelectionTree(treeState, testARD, popup, "testtree", "");
                    _testTree.Reload();
                }

                internal void TreeSelectionChangedHelper(IList<int> selectedIds)
                {
                    _testTree.SelectionChangedHelper(selectedIds);
                }

                class TestSelectionTree : AssetReferencePopup.AssetReferenceTreeView
                {
                    internal TestSelectionTree(TreeViewState state, AssetReferenceDrawer drawer,
                                               AssetReferencePopup popup, string guid, string nonAddressedAsset)
                        : base(state, drawer, popup, guid, nonAddressedAsset) {}

                    internal void SelectionChangedHelper(IList<int> selectedIds)
                    {
                        SelectionChanged(selectedIds);
                    }
                }
            }
        }
        
        public SerializedProperty SetupForSetObjectTests()
        {
            // Setup Original AssetReference to not be null
            m_AssetReferenceDrawer = new AssetReferenceDrawer();
            var assetPath = ConfigFolder + "/test" + "/test.prefab";
            CreateTestPrefabAddressable(assetPath);
            var newEntryGuid = AssetDatabase.AssetPathToGUID(assetPath);
            AssetReference ar = new AssetReference(newEntryGuid);
            Directory.CreateDirectory("Assets/AddressableAssetsData");
            AddressableAssetSettingsDefaultObject.Settings = Settings;

            // Setup property
            TestARDObjectBlank obj = ScriptableObject.CreateInstance<TestARDObjectBlank>();
            Settings.CreateOrMoveEntry(newEntryGuid, Settings.groups[0]);
            obj.Reference = ar;
            var so = new SerializedObject(obj);
            var property = so.FindProperty("Reference");
            m_AssetReferenceDrawer.GatherFilters(property);
            Directory.CreateDirectory("Assets/AddressableAssetsData");
            AddressableAssetSettingsDefaultObject.Settings = Settings;
            string sprGuid;
            m_AssetReferenceDrawer.m_AssetRefObject = ar;
            m_AssetReferenceDrawer.SetObject(property, obj, out sprGuid);

            return property;
        }

        public SerializedProperty SetupForSetAssets(SpriteAtlas spriteAtlas, int numReferences, bool setReferences = true, int numToSet = -1)
        {
            // Setup AssetReference selected
            m_AssetReferenceDrawer = new AssetReferenceDrawer();
            var assetPath = AssetDatabase.GetAssetOrScenePath(spriteAtlas);
            var atlasGuid = AssetDatabase.AssetPathToGUID(assetPath);
            AssetReferenceSprite ar = new AssetReferenceSprite(atlasGuid);

            // Setup property
            if (numToSet == -1)
                numToSet = numReferences-1;
            var targetObjects = new Object[numReferences];
            for (int i = 0; i < numReferences; i++)
            {
                var testScriptable = TestSubObjectsSpriteAtlas.CreateInstance<TestSubObjectsSpriteAtlas>();
                if(setReferences && i <= numToSet)
                    testScriptable.testSpriteReference = ar;
                targetObjects[i] = testScriptable;
            }
            var so = new SerializedObject(targetObjects);
            var property = so.FindProperty("testSpriteReference");
            m_AssetReferenceDrawer.GatherFilters(property);
            Directory.CreateDirectory("Assets/AddressableAssetsData");
            AddressableAssetSettingsDefaultObject.Settings = Settings;
            m_AssetReferenceDrawer.m_AssetRefObject = ar;
            m_AssetReferenceDrawer.m_label = new GUIContent("testSpriteReference");

            return property;
        }

        public string CreateTestPrefabAddressable(string newEntryPath)
        {
            GameObject testObject = new GameObject("TestObject");
            Directory.CreateDirectory(ConfigFolder + "/test");
            PrefabUtility.SaveAsPrefabAsset(testObject, newEntryPath);
            var folderGuid = AssetDatabase.AssetPathToGUID(ConfigFolder + "/test");
            Settings.CreateOrMoveEntry(folderGuid, Settings.groups[0]);
            return folderGuid;
        }
        
        public SpriteAtlas SetUpSpriteAtlas(int numAtlasObjects, out List<Object> subAssets)
        {
            // Setup Sprite data
            var texture = new Texture2D(32, 32);
            var data = ImageConversion.EncodeToPNG(texture);
            UnityEngine.Object.DestroyImmediate(texture);

            // Setup Sprites
            subAssets = new List<Object>();
            Directory.CreateDirectory(ConfigFolder+ "/test");
            var atlasPath = ConfigFolder + "/test" + "/testAtlas.spriteatlas";
            var newAtlas = new SpriteAtlas();
            var sprites = new Object[numAtlasObjects];
            for (int i = 0; i < numAtlasObjects; i++)
            {
                AssetDatabase.GenerateUniqueAssetPath(ConfigFolder);
                var newSpritePath = ConfigFolder + "/test" + "/testSprite" + i +".png";
                File.WriteAllBytes(newSpritePath, data);

                AssetDatabase.ImportAsset(newSpritePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

                var spriteGuid = AssetDatabase.AssetPathToGUID(newSpritePath);
                var importer = (TextureImporter)AssetImporter.GetAtPath(newSpritePath);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Multiple;
                importer.spritesheet = new SpriteMetaData[] { new SpriteMetaData() { name = "topleft", pivot = Vector2.zero, rect = new Rect(0, 0, 16, 16) },
                    new SpriteMetaData() { name = "testSprite" + i, pivot = Vector2.zero, rect = new Rect(16, 16, 16, 16) }};
                importer.SaveAndReimport();

                Object spr = AssetDatabase.LoadAssetAtPath(newSpritePath, typeof(Sprite));
                spr.name = "testSprite" + i;
                sprites[i] = spr;
                subAssets.Add(spr);
            }
            newAtlas.Add(sprites);
            AssetDatabase.CreateAsset(newAtlas,atlasPath);
            AssetDatabase.Refresh();

            SpriteAtlasExtensions.Add(newAtlas, sprites );
            SpriteAtlasUtility.PackAtlases(new SpriteAtlas[] { newAtlas }, EditorUserBuildSettings.activeBuildTarget, false);
            
            var atlasGuid = AssetDatabase.AssetPathToGUID(atlasPath);
            Settings.CreateOrMoveEntry(atlasGuid, Settings.groups[0]);
            newAtlas.GetSprite("testSprite" + 0);
            return newAtlas;
        }

        [Test]
        public void CanRestrictLabel()
        {
            m_AssetReferenceDrawer = new AssetReferenceDrawer();
            TestARDObject obj = ScriptableObject.CreateInstance<TestARDObject>();
            var so = new SerializedObject(obj);
            var property = so.FindProperty("Reference");
            m_AssetReferenceDrawer.GatherFilters(property);
            Assert.AreEqual(m_AssetReferenceDrawer.Restrictions.Count, 1);
            List<AssetReferenceUIRestrictionSurrogate> restrictions = m_AssetReferenceDrawer.Restrictions;
            Assert.True(restrictions.First().ToString().Contains("HD"));
        }

        [Test]
        public void CanRestrictMultipleLabels()
        {
            m_AssetReferenceDrawer = new AssetReferenceDrawer();
            TestARDObjectMultipleLabels obj = ScriptableObject.CreateInstance<TestARDObjectMultipleLabels>();
            var so = new SerializedObject(obj);
            var property = so.FindProperty("ReferenceMultiple");
            m_AssetReferenceDrawer.GatherFilters(property);
            List<AssetReferenceUIRestrictionSurrogate> restrictions = m_AssetReferenceDrawer.Restrictions;
            string restriction = restrictions.First().ToString();
            Assert.True(restriction.Contains("HDR"));
            Assert.True(restriction.Contains("test"));
            Assert.True(restriction.Contains("default"));
        }

        [Test]
        public void AssetReferenceDrawer_IsAssetPathInAddressableDirectory_PathInAddressableFolder()
        {
            // Asset setup
            m_AssetReferenceDrawer = new AssetReferenceDrawer();
            string assetName = "";
            var newEntryPath = ConfigFolder + "/test" + "/test.prefab";
            var folderGuid = CreateTestPrefabAddressable(newEntryPath);

            // Test
            Assert.IsTrue(Settings.IsAssetPathInAddressableDirectory(newEntryPath, out assetName));
            Assert.AreEqual(assetName, newEntryPath);

            // Cleanup
            Settings.RemoveAssetEntry(AssetDatabase.AssetPathToGUID(newEntryPath));
            Settings.RemoveAssetEntry(folderGuid);
            if (Directory.Exists(ConfigFolder + "/test"))
                AssetDatabase.DeleteAsset(ConfigFolder + "/test");
            m_AssetReferenceDrawer = null;
        }

        [Test]
        public void AssetReferenceDrawer_IsAssetPathInAddressableDirectory_PathNotInAddressableFolder()
        {
            // Asset setup
            m_AssetReferenceDrawer = new AssetReferenceDrawer();
            string assetName = "";
            var newEntryPath = ConfigFolder + "/test" + "/test.prefab";
            GameObject testObject = new GameObject("TestObject");
            Directory.CreateDirectory(ConfigFolder + "/test");
            PrefabUtility.SaveAsPrefabAsset(testObject, newEntryPath);
            var folderGuid = AssetDatabase.AssetPathToGUID(ConfigFolder + "/test");

            // Test
            Assert.IsFalse(Settings.IsAssetPathInAddressableDirectory(newEntryPath, out assetName));
            Assert.AreEqual(assetName, "");

            // Cleanup
            Settings.RemoveAssetEntry(AssetDatabase.AssetPathToGUID(newEntryPath));
            Settings.RemoveAssetEntry(folderGuid);
            if (Directory.Exists(ConfigFolder + "/test"))
                AssetDatabase.DeleteAsset(ConfigFolder + "/test");
            m_AssetReferenceDrawer = null;
        }

        [Test]
        public void AssetReferenceDrawer_IsAssetPathInAddressableDirectory_PathEmptyString()
        {
            m_AssetReferenceDrawer = new AssetReferenceDrawer();
            string assetName = "";

            Assert.IsFalse(Settings.IsAssetPathInAddressableDirectory("", out assetName));
            Assert.AreEqual(assetName, "");
        }

        [Test]
        public void AssetReferenceDrawer_IsAssetPathInAddressableDirectory_PathPointToNonexistentAsset()
        {
            m_AssetReferenceDrawer = new AssetReferenceDrawer();
            string assetName = "";

            Assert.IsFalse(Settings.IsAssetPathInAddressableDirectory(ConfigFolder + "/test.prefab", out assetName));
            Assert.AreEqual(assetName, "");
        }

        [Test]
        public void AssetReferenceDrawer_SelectionChanged_CanSelectSameNameAssetsInDifferentGroups()
        {
            // Drawer Setup
            var testARD = new TestAssetReferenceDrawer();
            testARD.SetAssetReference(new AssetReference());
            TestARDObjectBlank obj = ScriptableObject.CreateInstance<TestARDObjectBlank>();
            var so = new SerializedObject(obj);
            var property = so.FindProperty("Reference");
            testARD.GatherFilters(property);

            // Entries setup
            var newEntryPath = ConfigFolder + "/test" + "/test.prefab";
            var testEntry = Settings.CreateOrMoveEntry(m_AssetGUID, Settings.groups[0]);
            GameObject testObject = new GameObject("TestObject");
            Directory.CreateDirectory(ConfigFolder + "/test");
            PrefabUtility.SaveAsPrefabAsset(testObject, newEntryPath);
            var newEntryGuid = AssetDatabase.AssetPathToGUID(newEntryPath);
            var secondTestEntry = Settings.CreateOrMoveEntry(newEntryGuid, Settings.groups[1]);

            // Tree setup
            var testId = testEntry.AssetPath.GetHashCode();
            List<int> selectedIds = new List<int>() { testId };
            var treeState = new TreeViewState();
            treeState.selectedIDs = selectedIds;
            Directory.CreateDirectory("Assets/AddressableAssetsData");
            AddressableAssetSettingsDefaultObject.Settings = Settings;
            testARD.TreeSetup(treeState);

            // Test
            testARD.TreeSelectionChangedHelper(selectedIds);
            Assert.AreEqual(m_AssetGUID, testARD.newGuid);
            selectedIds[0] = secondTestEntry.AssetPath.GetHashCode();
            testARD.TreeSelectionChangedHelper(selectedIds);
            Assert.AreEqual(AssetDatabase.AssetPathToGUID(newEntryPath), testARD.newGuid);

            // Cleanup
            if (Directory.Exists("Assets/AddressableAssetsData"))
                AssetDatabase.DeleteAsset("Assets/AddressableAssetsData");
            EditorBuildSettings.RemoveConfigObject("Assets/AddressableAssetsData");
            Settings.RemoveAssetEntry(AssetDatabase.AssetPathToGUID(newEntryPath));
            Settings.RemoveAssetEntry(m_AssetGUID);
        }

        [Test]
        public void AssetReferenceDrawer_HandleDragAndDrop_CanRecognizeNonAddressableInAddressableFolder()
        {
            // ScriptableObject property and Drawer setup
            m_AssetReferenceDrawer = new AssetReferenceDrawer();
            m_AssetReferenceDrawer.m_AssetRefObject = new AssetReference();
            TestARDObject obj = ScriptableObject.CreateInstance<TestARDObject>();
            var so = new SerializedObject(obj);
            var propertyName = "Reference";
            var property = so.FindProperty(propertyName);

            // Group setup
            string groupName = "TestGroup";
            Settings.CreateGroup(groupName, false, false, false, null);

            // Asset setup
            var newEntryPath = ConfigFolder + "/test" + "/test.prefab";
            var folderGuid = CreateTestPrefabAddressable(newEntryPath);
            var newAssetGuid = AssetDatabase.AssetPathToGUID(newEntryPath);
            Settings.CreateOrMoveEntry(folderGuid, Settings.groups[2]);
            Directory.CreateDirectory("Assets/AddressableAssetsData");
            AddressableAssetSettingsDefaultObject.Settings = Settings;

            // Test
            m_AssetReferenceDrawer.DragAndDropNotFromAddressableGroupWindow(newEntryPath, newAssetGuid, property, Settings);
            var newentry = Settings.FindAssetEntry(newAssetGuid);
            Assert.IsNull(newentry);
            Assert.AreEqual(m_AssetReferenceDrawer.m_AssetRefObject.AssetGUID, newAssetGuid);

            // Cleanup
            Settings.RemoveAssetEntry(AssetDatabase.AssetPathToGUID(newEntryPath));
            Settings.RemoveAssetEntry(folderGuid);
            Settings.RemoveGroup(Settings.groups[2]);
            if (Directory.Exists(ConfigFolder + "/test"))
                AssetDatabase.DeleteAsset(ConfigFolder + "/test");
            m_AssetReferenceDrawer = null;
        }
        
        [Test]
        public void AssetReferenceDrawer_SetObject_CanSetObject()
        {
            // Setup AssetReference
            m_AssetReferenceDrawer = new AssetReferenceDrawer();
            AssetReference ar = new AssetReference();
            var assetPath = AssetDatabase.GUIDToAssetPath(m_AssetGUID);
            m_AssetReferenceDrawer.m_AssetRefObject = ar;
            m_AssetReferenceDrawer.m_AssetRefObject.SubObjectName = "test";
            var testObject = AssetDatabase.LoadMainAssetAtPath(assetPath);

            // Setup property
            TestARDObjectBlank obj = ScriptableObject.CreateInstance<TestARDObjectBlank>();
            var so = new SerializedObject(obj);
            var property = so.FindProperty("Reference");
            m_AssetReferenceDrawer.GatherFilters(property);

            // Test
            string guid;
            Assert.IsTrue(m_AssetReferenceDrawer.SetObject(property, testObject, out guid));
            Assert.AreEqual(m_AssetGUID, m_AssetReferenceDrawer.m_AssetRefObject.AssetGUID);
            Assert.AreEqual(m_AssetGUID, guid);
            Assert.AreEqual(testObject.name, m_AssetReferenceDrawer.m_AssetRefObject.editorAsset.name);
        }
        
        [Test]
        public void AssetReferenceDrawer_SetObject_CanSetToNull()
        {
            // Setup Original AssetReference to not be null and property
            var property = SetupForSetObjectTests();

            // Test
            string guid;
            m_AssetReferenceDrawer.SetObject(property, null, out guid);
            Assert.AreEqual(null, m_AssetReferenceDrawer.m_AssetRefObject.SubObjectName);
            Assert.AreEqual(string.Empty, m_AssetReferenceDrawer.m_AssetRefObject.AssetGUID);
        }

#if UNITY_2019_2_OR_NEWER
        
        [Test]
        public void AssetReferenceDrawer_SetObject_SetToNullDirtiesObject()
        {
            // Setup Original AssetReference to not be null and property
            var property = SetupForSetObjectTests();

            // Test
            string guid;
            EditorUtility.ClearDirty(property.serializedObject.targetObject);
            var prevDirty = EditorUtility.IsDirty(property.serializedObject.targetObject);
            m_AssetReferenceDrawer.SetObject(property, null, out guid);
            Assert.IsFalse(prevDirty);
            Assert.IsTrue(EditorUtility.IsDirty(property.serializedObject.targetObject));
        }

        [TestCase(1,0,1)]
        [TestCase(20,10,8)]
        public void AssetReferenceDrawer_SetSubAssets_CanSetSubAssets(int numAtlasObjects,int selectedId, int numReferences)
        { 
            // Setup
            var subAssets = new List<Object>();
            var atlas = SetUpSpriteAtlas(numAtlasObjects, out subAssets);
            var property = SetupForSetAssets(atlas, numReferences, true);
            m_AssetReferenceDrawer.m_label = new GUIContent("testSpriteReference");
            FieldInfo propertyFieldInfo = typeof(TestSubObjectsSpriteAtlas).GetField("testSpriteReference", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            
            // Test
            m_AssetReferenceDrawer.SetSubAssets(property,subAssets[selectedId],propertyFieldInfo);
            foreach(var obj in property.serializedObject.targetObjects) {
                Assert.AreEqual(((TestSubObjectsSpriteAtlas)obj).testSpriteReference.SubObjectName,subAssets[selectedId].name);
            }
            
            // Cleanup
            if (Directory.Exists("Assets/AddressableAssetsData"))
                AssetDatabase.DeleteAsset("Assets/AddressableAssetsData");
            EditorBuildSettings.RemoveConfigObject("Assets/AddressableAssetsData");
        }
        
        [TestCase(1,0,1)]
        [TestCase(20,10,8)]
        public void AssetReferenceDrawer_SetSubAssets_CanSetSubAssetsToNull(int numAtlasObjects,int selectedId, int numReferences)
        { 
            // Setup
            var subAssets = new List<Object>();
            var atlas = SetUpSpriteAtlas(numAtlasObjects, out subAssets);
            var property = SetupForSetAssets(atlas, numReferences, true);
            m_AssetReferenceDrawer.m_label = new GUIContent("testSpriteReference");
            FieldInfo propertyFieldInfo = typeof(TestSubObjectsSpriteAtlas).GetField("testSpriteReference", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            
            // Test
            m_AssetReferenceDrawer.SetSubAssets(property,subAssets[selectedId],propertyFieldInfo);
            m_AssetReferenceDrawer.SetSubAssets(property,null,propertyFieldInfo);
            foreach(var obj in property.serializedObject.targetObjects) {
                Assert.AreEqual(((TestSubObjectsSpriteAtlas)obj).testSpriteReference.SubObjectName,null);
            }
            
            // Cleanup
            if (Directory.Exists("Assets/AddressableAssetsData"))
                AssetDatabase.DeleteAsset("Assets/AddressableAssetsData");
            EditorBuildSettings.RemoveConfigObject("Assets/AddressableAssetsData");
        }

        
        [TestCase(1,1)]
        [TestCase(20,8)]
        public void AssetReferenceDrawer_SetMainAssets_CanSetMultipleAssets(int numAtlasObjects, int numReferences)
        { 
            // Setup
            var subAssets = new List<Object>();
            var atlas = SetUpSpriteAtlas(numAtlasObjects, out subAssets);
            var property = SetupForSetAssets(atlas, numReferences);
            m_AssetReferenceDrawer.m_label = new GUIContent("testSpriteReference");
            FieldInfo propertyFieldInfo = typeof(TestSubObjectsSpriteAtlas).GetField("testSpriteReference", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            var assetPath = AssetDatabase.GetAssetOrScenePath(atlas);
            var atlasGuid = AssetDatabase.AssetPathToGUID(assetPath);

            // Test
            m_AssetReferenceDrawer.SetMainAssets(property,atlas, null, propertyFieldInfo);
            foreach(var obj in property.serializedObject.targetObjects) {
                Assert.AreEqual(((TestSubObjectsSpriteAtlas)obj).testSpriteReference.AssetGUID,atlasGuid);
            }
            
            // Cleanup
            if (Directory.Exists("Assets/AddressableAssetsData"))
                AssetDatabase.DeleteAsset("Assets/AddressableAssetsData");
            EditorBuildSettings.RemoveConfigObject("Assets/AddressableAssetsData");
        }

        
        [Test]
        public void AssetReferenceDrawer_SetMainAssets_CanSetToNull()
        { 
            // Setup
            var subAssets = new List<Object>();
            var atlas = SetUpSpriteAtlas(1, out subAssets);
            var property = SetupForSetAssets(atlas, 1);
            m_AssetReferenceDrawer.m_label = new GUIContent("testSpriteReference");
            FieldInfo propertyFieldInfo = typeof(TestSubObjectsSpriteAtlas).GetField("testSpriteReference", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            var assetPath = AssetDatabase.GetAssetOrScenePath(atlas);

            // Test
            m_AssetReferenceDrawer.SetMainAssets(property,null, null,propertyFieldInfo);
            Assert.AreEqual(((TestSubObjectsSpriteAtlas)property.serializedObject.targetObject).testSpriteReference.Asset,null);

            // Cleanup
            if (Directory.Exists("Assets/AddressableAssetsData"))
                AssetDatabase.DeleteAsset("Assets/AddressableAssetsData");
            EditorBuildSettings.RemoveConfigObject("Assets/AddressableAssetsData");
        }
        
        [Test]
        public void AssetReferenceDrawer_SetMainAssets_SetToNullDirtiesObject()
        {
            // Setup
            var subAssets = new List<Object>();
            var atlas = SetUpSpriteAtlas(1, out subAssets);
            var property = SetupForSetAssets(atlas, 1);
            m_AssetReferenceDrawer.m_label = new GUIContent("testSpriteReference");
            FieldInfo propertyFieldInfo = typeof(TestSubObjectsSpriteAtlas).GetField("testSpriteReference", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            var assetPath = AssetDatabase.GetAssetOrScenePath(atlas);

            // Test
            EditorUtility.ClearDirty(property.serializedObject.targetObject);
            var prevDirty = EditorUtility.IsDirty(property.serializedObject.targetObject);
            m_AssetReferenceDrawer.SetMainAssets(property,null, null,propertyFieldInfo);
            Assert.IsFalse(prevDirty);
            Assert.IsTrue(EditorUtility.IsDirty(property.serializedObject.targetObject));
        }
        
        [TestCase(1,1)]
        [TestCase(20,8)]
        public void AssetReferenceDrawer_GetNameForAsset_CanGetAssetNameWhenAllSame(int numAtlasObjects, int numReferences)
        {
            // Setup
            var subAssets = new List<Object>();
            var atlas = SetUpSpriteAtlas(numAtlasObjects, out subAssets);
            var property = SetupForSetAssets(atlas, numReferences);
            FieldInfo propertyFieldInfo = typeof(TestSubObjectsSpriteAtlas).GetField("testSpriteReference", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            m_AssetReferenceDrawer.m_AssetName = atlas.name;
            
            // Test
            var nameforAsset = m_AssetReferenceDrawer.GetNameForAsset(property, false, propertyFieldInfo);
            Assert.AreEqual(atlas.name,nameforAsset);
            
            // Cleanup
            if (Directory.Exists("Assets/AddressableAssetsData"))
                AssetDatabase.DeleteAsset("Assets/AddressableAssetsData");
            EditorBuildSettings.RemoveConfigObject("Assets/AddressableAssetsData");
        }
        
        [TestCase(10,4,8)]
        public void AssetReferenceDrawer_GetNameForAsset_CanGetAssetNameWhenDifferent(int numAtlasObjects,int numToSet, int numReferences)
        {
            // Setup
            var subAssets = new List<Object>();
            var atlas = SetUpSpriteAtlas(numAtlasObjects, out subAssets);
            var property = SetupForSetAssets(atlas, numReferences,true, numToSet);
            m_AssetReferenceDrawer.m_label = new GUIContent("testSpriteReference");
            FieldInfo propertyFieldInfo = typeof(TestSubObjectsSpriteAtlas).GetField("testSpriteReference", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            m_AssetReferenceDrawer.m_AssetName = atlas.name;
            
            // Test
            var nameforAsset = m_AssetReferenceDrawer.GetNameForAsset(property, false, propertyFieldInfo);
            Assert.AreEqual("--",nameforAsset);
            
            // Cleanup
            if (Directory.Exists("Assets/AddressableAssetsData"))
                AssetDatabase.DeleteAsset("Assets/AddressableAssetsData");
            EditorBuildSettings.RemoveConfigObject("Assets/AddressableAssetsData");
        }
#endif
    }
}
