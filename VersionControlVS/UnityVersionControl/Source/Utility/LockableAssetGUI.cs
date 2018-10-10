using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace VersionControl.UserInterface
{
    public class LockableAssetGUI
    {
        private static GUIStyle buttonStyle;
        private static GUIStyle backgroundGuiStyle;

        public static bool drawGUI(string _strAssetPath, bool _bVertical = true)
        {
            VCUtility.RequestStatus(_strAssetPath, VCSettings.HierarchyReflectionMode);
            var vcStatus = VCCommands.Instance.GetAssetStatus(_strAssetPath);

            buttonStyle = new GUIStyle(EditorStyles.miniButton) { margin = new RectOffset(0, 0, 0, 0), fixedWidth = 80 };

            backgroundGuiStyle = VCGUIControls.GetVCBox(vcStatus);
            backgroundGuiStyle.padding = new RectOffset(4, 8, 1, 1);
            backgroundGuiStyle.margin = new RectOffset(1, 1, 1, 1);
            backgroundGuiStyle.border = new RectOffset(1, 1, 1, 1);
            backgroundGuiStyle.alignment = TextAnchor.MiddleCenter;

            GUILayout.TextField(AssetStatusUtils.GetLockStatusMessage(vcStatus), backgroundGuiStyle);

            if (_bVertical)
            {
                using (GUILayoutHelper.Vertical())
                {
                    return drawButtons(_strAssetPath);
                }
            }
            else
            {
                using (GUILayoutHelper.Horizontal())
                {
                    return drawButtons(_strAssetPath);
                }
            }
        }

        static bool drawButtons(string _strAssetPath)
        {
            var validActions = VCGUIControls.GetValidActions(_strAssetPath);

            int numberOfButtons = 0;
            const int maxButtons = 5;

            bool bNeedsRepaint = false;

            using (new PushState<bool>(GUI.enabled, VCCommands.Instance.Ready, v => GUI.enabled = v))
            {
                numberOfButtons++;
                if (GUILayout.Button("Refresh", buttonStyle))
                {
                    Refresh(_strAssetPath);
                }

                if (validActions.showAdd)
                {
                    numberOfButtons++;
                    if (GUILayout.Button(Terminology.add, buttonStyle))
                    {
                        bNeedsRepaint = true;

                        SceneManagerUtilities.SaveActiveScene();
                        OnNextUpdate.Do(() => VCCommands.Instance.CommitDialog(new[] { _strAssetPath }));
                    }
                }
                if (validActions.showOpen)
                {
                    numberOfButtons++;
                    if (GUILayout.Button(Terminology.getlock, buttonStyle))
                    {
                        bNeedsRepaint = true;

                        Refresh(_strAssetPath);
                        if (!validActions.showOpen)
                        {
                            EditorUtility.DisplayDialog("Cannot open Scene!", "This scene has been opened by another user since the last refresh.", "Ok");
                        }
                        else
                            VCCommands.Instance.GetLockTask(new[] { _strAssetPath });
                    }
                }
                if (validActions.showCommit)
                {
                    numberOfButtons++;
                    if (GUILayout.Button(Terminology.commit, buttonStyle))
                    {
                        bNeedsRepaint = true;

                        OnNextUpdate.Do(() => VCCommands.Instance.CommitDialog(new[] { _strAssetPath }));
                    }
                }
                if (validActions.showRevert)
                {
                    numberOfButtons++;
                    if (GUILayout.Button(new GUIContent(Terminology.revert, "Shift-click to " + Terminology.revert + " without confirmation"), buttonStyle))
                    {
                        bNeedsRepaint = true;

                        var assetPath = new[] { _strAssetPath };
                        if (Event.current.shift || VCUtility.VCDialog(Terminology.revert, assetPath))
                        {
                            VCCommands.Instance.Revert(assetPath);
                            OnNextUpdate.Do(AssetDatabase.Refresh);
                        }
                    }
                }
                if (validActions.showOpenLocal)
                {
                    numberOfButtons++;
                    if (GUILayout.Button(Terminology.allowLocalEdit, buttonStyle))
                    {
                        bNeedsRepaint = true;

                        VCCommands.Instance.AllowLocalEdit(new[] { _strAssetPath });
                    }
                }
                if (validActions.showUnlock)
                {
                    numberOfButtons++;
                    if (GUILayout.Button(Terminology.unlock, buttonStyle))
                    {
                        bNeedsRepaint = true;

                        OnNextUpdate.Do(() => VCCommands.Instance.ReleaseLock(new[] { _strAssetPath }));
                    }
                }
                if (validActions.showForceOpen)
                {
                    numberOfButtons++;
                    if (GUILayout.Button("Force Open", buttonStyle))
                    {
                        bNeedsRepaint = true;

                        OnNextUpdate.Do(() => VCUtility.GetLock(_strAssetPath, OperationMode.Force));
                    }
                }

                // bug: Workaround for a bug in Unity to avoid Tools getting stuck when number of GUI elements change while right mouse is down.
                using (GUILayoutHelper.Enabled(false))
                {
                    for (int i = numberOfButtons; i <= maxButtons; ++i)
                    {
                        GUI.Button(new Rect(0, 0, 0, 0), "", EditorStyles.label);
                    }
                }
            }

            return bNeedsRepaint;
        }

        static void Refresh(string _strAssetPath)
        {
            VCUtility.RequestStatus(_strAssetPath, VCSettings.HierarchyReflectionMode);
            SceneView.RepaintAll();
        }

        public static bool bHaveLock(string _strAssetPath)
        {
            VCUtility.RequestStatus(_strAssetPath, VCSettings.HierarchyReflectionMode);

            VersionControlStatus assetStatus = VCCommands.Instance.GetAssetStatus(_strAssetPath);
            return assetStatus != null ? (VCUtility.HaveVCLock(assetStatus) || assetStatus.allowLocalEdit) : true;
        }

    }
}
