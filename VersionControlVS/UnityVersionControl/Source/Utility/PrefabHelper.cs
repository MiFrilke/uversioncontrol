// Copyright (c) <2017> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using UnityEngine;
using UnityEditor;

namespace VersionControl
{
    public static class PrefabHelper
    {
        #region UnityEditorProxys
        //public static PrefabType GetPrefabType(Object obj) { return PrefabUtility.GetPrefabType(obj); }
        public static PrefabAssetType GetPrefabAssetType(Object obj) { return PrefabUtility.GetPrefabAssetType(obj); }
        public static PrefabInstanceStatus GetPrefabInstanceStatus(Object obj) { return PrefabUtility.GetPrefabInstanceStatus(obj); }
        public static Object GetPrefabSource(Object obj) { return PrefabUtility.GetCorrespondingObjectFromSource(obj); }
        public static GameObject FindPrefabRoot(GameObject go)
        {
            if (IsPrefabAsset(go))
                return go.transform.root.gameObject;
            else
                return PrefabUtility.GetOutermostPrefabInstanceRoot(go);
        }
        public static Object InstantiatePrefab(Object obj) { return PrefabUtility.InstantiatePrefab(obj); }
        #endregion

        public static bool IsPrefabAsset(Object go)
        {
            if (!go) return false;
            return PrefabUtility.IsPartOfPrefabAsset(go);

            //PrefabType pbtype = GetPrefabType(go);
            //bool isPrefabParent =
            //    pbtype == PrefabType.ModelPrefab ||
            //    pbtype == PrefabType.Prefab;

            //return isPrefabParent;
        }

        public static bool IsPrefabRoot(Object obj)
        {
            var gameObject = obj as GameObject;
            if (gameObject && IsPrefab(obj))
            {
                return FindPrefabRoot(gameObject) == gameObject;
            }
            return false;
        }

        public static bool IsPrefab(Object obj, bool includeRegular = true, bool includeModels = true, bool includeMissingAsset = true, bool includeVariant = true)
        {
            if (!obj) return false;

            var assetType = GetPrefabAssetType(obj);

            if (assetType == PrefabAssetType.NotAPrefab)
                return false;

            bool isPrefab =
                (includeRegular && assetType == PrefabAssetType.Regular) ||
                (includeVariant && assetType == PrefabAssetType.Variant) ||
                (includeModels && assetType == PrefabAssetType.Model) ||
                (includeMissingAsset && assetType == PrefabAssetType.MissingAsset);

            return isPrefab;

            //PrefabType pbtype = GetPrefabType(obj);
            //bool isPrefab =
            //    (includeRegular && pbtype == PrefabType.Prefab) ||
            //    (includeRegular && pbtype == PrefabType.PrefabInstance) ||
            //    (includeRegular && includeDisconnected && pbtype == PrefabType.DisconnectedPrefabInstance) ||
            //    (includeModels && pbtype == PrefabType.ModelPrefab) ||
            //    (includeModels && pbtype == PrefabType.ModelPrefabInstance) ||
            //    (includeModels && includeDisconnected && pbtype == PrefabType.DisconnectedModelPrefabInstance);
            //return isPrefab;
        }

        public static void UnpackPrefab(GameObject gameObject)
        {
            GameObject goPrefabRoot = FindPrefabRoot(gameObject);
            Undo.RegisterCompleteObjectUndo(goPrefabRoot, "Unpack prefab");            
            PrefabUtility.UnpackPrefabInstance(FindPrefabRoot(gameObject), PrefabUnpackMode.Completely, InteractionMode.UserAction);
            

            //// instantiate prefab at prefab location, remove original prefab instance.
            //var prefabRoot = FindPrefabRoot(gameObject);
            //string prefabName = prefabRoot.name;

            //var replacedPrefab = Object.Instantiate(prefabRoot, prefabRoot.transform.position, prefabRoot.transform.rotation) as GameObject;
            //Undo.RegisterCreatedObjectUndo(replacedPrefab, "Disconnect Prefab");
            //replacedPrefab.name = prefabName;
            //replacedPrefab.transform.parent = prefabRoot.transform.parent;

            //Undo.DestroyObjectImmediate(prefabRoot);
            //return replacedPrefab;
        }

        public static void SelectPrefab(GameObject gameObject)
        {
            var prefabParent = GetPrefabSource(FindPrefabRoot(gameObject)) as GameObject;
            Selection.activeGameObject = prefabParent;
            EditorGUIUtility.PingObject(Selection.activeGameObject);
        }

        public static void ApplyPrefab(GameObject prefabInstance)
        {            
            GameObject goInstanceRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(prefabInstance);
            var prefabSource = GetPrefabSource(goInstanceRoot) as GameObject;

            if (prefabSource && PrefabUtility.GetPrefabAssetType(prefabSource) == PrefabAssetType.Regular)
            {
                Undo.RecordObject(goInstanceRoot, "Apply Prefab");
                PrefabUtility.ApplyObjectOverride(goInstanceRoot, AssetDatabase.GetAssetPath(prefabSource), InteractionMode.UserAction);
            }
        }
    }
}
