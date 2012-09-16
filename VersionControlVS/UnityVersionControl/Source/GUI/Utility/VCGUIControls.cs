// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>

using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using Object = UnityEngine.Object;

namespace VersionControl.UserInterface
{
    public static class VCGUIControls
    {
        private static GUIStyle GetPrefabToolbarStyle(GUIStyle style, bool vcRelated)
        {
            var vcStyle = new GUIStyle(style);
            if (vcRelated)
            {
                vcStyle.fontStyle = FontStyle.Bold;
            }
            return vcStyle;
        }

        public static void VersionControlStatusGUI(GUIStyle style, VersionControlStatus assetStatus, Object obj, bool showAddCommit, bool showLockBypass, bool showRevert, bool confirmRevert = false)
        {
            using (new PushState<bool>(GUI.enabled, VCCommands.Instance.Ready, v => GUI.enabled = v))
            {
                if (assetStatus.lockStatus == VCLockStatus.LockedHere || assetStatus.bypassRevisionControl || !VCUtility.ManagedByRepository(assetStatus))
                {
                    if (!assetStatus.bypassRevisionControl && obj.GetAssetPath() != "" && showAddCommit)
                    {
                        if (GUILayout.Button((VCUtility.ManagedByRepository(assetStatus) ? Terminology.commit : Terminology.add), GetPrefabToolbarStyle(style, true)))
                        {
                            VCUtility.ApplyAndCommit(obj, Terminology.commit + " from Inspector");
                        }
                    }
                }

                if (!VCUtility.HaveVCLock(assetStatus) && VCUtility.ManagedByRepository(assetStatus) && showLockBypass)
                {
                    if (assetStatus.fileStatus == VCFileStatus.Added)
                    {
                        if (GUILayout.Button(Terminology.commit, GetPrefabToolbarStyle(style, true)))
                        {
                            VCUtility.ApplyAndCommit(obj, Terminology.commit + " from Inspector");
                        }
                    }
                    else if (assetStatus.lockStatus != VCLockStatus.LockedOther)
                    {
                        if (GUILayout.Button(Terminology.getlock, GetPrefabToolbarStyle(style, true)))
                        {
                            VCCommands.Instance.GetLockTask(obj.ToAssetPaths());
                        }
                    }
                    if (!assetStatus.bypassRevisionControl)
                    {
                        if (GUILayout.Button(Terminology.bypass, GetPrefabToolbarStyle(style, true)))
                        {
                            VCCommands.Instance.BypassRevision(obj.ToAssetPaths());
                        }
                    }
                }

                if (showRevert)
                {
                    if (GUILayout.Button(Terminology.revert, GetPrefabToolbarStyle(style, VCUtility.ShouldVCRevert(obj))))
                    {
                        if ((!confirmRevert || Event.current.shift) || VCUtility.VCDialog(Terminology.revert, obj))
                        {
                            var seletedGo = Selection.activeGameObject;
                            var revertedObj = VCUtility.Revert(obj);
                            OnNextUpdate.Do(() => Selection.activeObject = ((obj is GameObject) ? revertedObj : seletedGo));
                        }
                    }
                }
            }
        }



        public static GUIStyle GetVCBox(VersionControlStatus assetStatus)
        {
            return new GUIStyle(GUI.skin.box)
            {
                border = new RectOffset(2, 2, 2, 2),
                padding = new RectOffset(1, 1, 1, 1),
                normal = { background = IconUtils.boxIcon.GetTexture(AssetStatusUtils.GetStatusColor(assetStatus, true)) }
            };
        }

        public static GUIStyle GetLockStatusStyle()
        {
            return new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.black }, alignment = TextAnchor.MiddleCenter };
        }
        
        public static GenericMenu CreateVCContextMenu(IEnumerable<string> assetPaths)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent(Terminology.add), false, () => VCCommands.Instance.Add(assetPaths));
            menu.AddItem(new GUIContent(Terminology.getlock), false, () => VCCommands.Instance.GetLock(assetPaths));
            menu.AddItem(new GUIContent(Terminology.commit), false, () => VCCommands.Instance.CommitDialog(assetPaths));
            menu.AddItem(new GUIContent(Terminology.revert), false, () => VCCommands.Instance.Revert(assetPaths));
            menu.AddItem(new GUIContent(Terminology.delete), false, () => VCCommands.Instance.Delete(assetPaths));
            return menu;
        }

        public static GenericMenu CreateVCContextMenu(string assetPath, Object instance = null)
        {
            var menu = new GenericMenu();
            if (VCUtility.ValidAssetPath(assetPath))
            {
                var assetStatus = VCCommands.Instance.GetAssetStatus(assetPath);
                if (ObjectExtension.ChangesStoredInScene(AssetDatabase.LoadMainAssetAtPath(assetPath))) assetPath = EditorApplication.currentScene;

                bool ready = VCCommands.Instance.Ready;
                bool isPrefab = instance != null && PrefabHelper.IsPrefab(instance);
                bool isPrefabParent = isPrefab && PrefabHelper.IsPrefabParent(instance);
                bool isFolder = System.IO.Directory.Exists(assetPath);
                bool modifiedTextAsset = VCUtility.IsTextAsset(assetPath) && assetStatus.fileStatus != VCFileStatus.Normal;
                bool modifiedMeta = assetStatus.MetaStatus().fileStatus != VCFileStatus.Normal;
                bool deleted = assetStatus.fileStatus == VCFileStatus.Deleted;
                bool added = assetStatus.fileStatus == VCFileStatus.Added;
                bool unversioned = assetStatus.fileStatus == VCFileStatus.Unversioned;
                bool ignored = assetStatus.fileStatus == VCFileStatus.Ignored;
                bool replaced = assetStatus.fileStatus == VCFileStatus.Replaced;
                bool lockedByOther = assetStatus.lockStatus == VCLockStatus.LockedOther;
                bool managedByRep = VCUtility.ManagedByRepository(assetStatus);
                bool haveControl = VCUtility.HaveAssetControl(assetStatus);
                bool haveLock = VCUtility.HaveVCLock(assetStatus);
                bool bypass = assetStatus.bypassRevisionControl;
                bool pending = assetStatus.reflectionLevel == VCReflectionLevel.Pending;

                bool showAdd = ready && !pending && !ignored && unversioned;
                bool showOpen = ready && !pending && !showAdd && !added && !haveLock && !deleted && !isFolder && (!lockedByOther || bypass);
                bool showDiff = ready && !pending && !ignored && modifiedTextAsset && managedByRep;
                bool showCommit = ready && !pending && !ignored && !bypass && (haveControl || added || deleted || modifiedTextAsset || isFolder || modifiedMeta);
                bool showRevert = ready && !pending && !ignored && !unversioned && (haveControl || added || deleted || replaced || modifiedTextAsset || modifiedMeta);
                bool showDelete = ready && !pending && !ignored && !deleted && !lockedByOther;
                bool showOpenLocal = ready && !pending && !ignored && !deleted && !isFolder && !bypass && !unversioned && !added && !haveLock;
                bool showUnlock = ready && !pending && !ignored && !bypass && haveLock;
                bool showUpdate = ready && !pending && !ignored && !added && managedByRep && instance != null;
                bool showForceOpen = ready && !pending && !ignored && !deleted && !isFolder && !bypass && !unversioned && !added && lockedByOther && Event.current.shift;
                bool showDisconnect = isPrefab && !isPrefabParent;

                if (showAdd) menu.AddItem(new GUIContent(Terminology.add), false, () => VCCommands.Instance.Add(new[] { assetPath }));
                if (showOpen) menu.AddItem(new GUIContent(Terminology.getlock), false, () => VCCommands.Instance.GetLock(new[] { assetPath }));
                if (showOpenLocal) menu.AddItem(new GUIContent(Terminology.bypass), false, () => VCCommands.Instance.BypassRevision(new[] { assetPath }));
                if (showForceOpen) menu.AddItem(new GUIContent("Force " + Terminology.getlock), false, () => VCUtility.VCForceOpen(assetPath, assetStatus));
                if (showCommit) menu.AddItem(new GUIContent(Terminology.commit), false, () => Commit(assetPath, instance));
                if (showDelete) menu.AddItem(new GUIContent(Terminology.delete), false, () => VCCommands.Instance.Delete(new[] { assetPath }, OperationMode.Force));
                if (showRevert) menu.AddItem(new GUIContent(Terminology.revert), false, () => Revert(assetPath, instance));
                if (showUnlock) menu.AddItem(new GUIContent(Terminology.unlock), false, () => VCCommands.Instance.ReleaseLock(new[] { assetPath }));
                if (showDisconnect) menu.AddItem(new GUIContent("Disconnect"), false, () => PrefabHelper.DisconnectPrefab(instance as GameObject));
                if (showUpdate) menu.AddItem(new GUIContent(Terminology.update), false, () => VCCommands.Instance.UpdateTask(new[] { assetPath }));
                if (showDiff) menu.AddItem(new GUIContent(Terminology.diff), false, () => VCUtility.DiffWithBase(assetPath));
            }
            return menu;
        }

        private static void Commit(string assetPath, Object instance)
        {
            if (instance != null) VCUtility.ApplyAndCommit(instance, "");
            else VCCommands.Instance.CommitDialog(new[] { assetPath });
        }

        private static void Revert(string assetPath, Object instance)
        {
            if (instance != null) VCUtility.Revert(instance);
            else VCCommands.Instance.Revert(new[] { assetPath });
        }

        public static void DiaplayVCContextMenu(Object instance)
        {
            CreateVCContextMenu(instance.GetAssetPath(), instance).ShowAsContext();
            Event.current.Use();
        }
    }
}

