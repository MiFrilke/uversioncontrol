// Copyright (c) <2017> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>

// This script is a window to display local changes and to perform commands on 
// the repository like updating and committing files.
// SVNIntegration is used to get state and execute commands on the repository.
//
// Although functional the general quality of this file is poor and need a refactor


using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
//using MultiColumnState = MultiColumnState<string, UnityEngine.GUIContent>;

namespace VersionControl.UserInterface
{
    internal class VCWindow : EditorWindow, IHasCustomMenu
    {
        // Const
        const float toolbarHeight = 18.0f;
        const float inStatusHeight = 18.0f;
        const int maxProgressSize = 10000;
        private readonly Color activeColor = new Color(0.8f, 0.8f, 1.0f);

        public static bool c_bNeedsRefresh;

        // State
        private bool m_bShowUserHidden = false;
        private bool m_bShowExternal = false;

        private bool showUnversioned = true;
        private bool showMeta = true;
        private bool showModifiedNoLock = true;
        private bool showProjectSetting = false;
        private float statusHeight = 1000;
        private bool updateInProgress = false;
        private bool refreshInProgress = false;
        private string commandInProgress = "";
        private VCMultiColumnAssetList vcMultiColumnAssetList;
        private VCSettingsWindow settingsWindow;
        private Rect rect;
        private int updateCounter = 0;

        // Cache
        private Vector2 statusScroll = Vector2.zero;
        private string searchString = "";
        private GUIStyle toolbarSearchTextStyle;
        private GUIStyle cancelSearchButtonStyle;
        private GUIStyle cancelSearchButtonEmptyStyle;

        private string m_strHiddenOld = "";

        [MenuItem("Window/UVC/Overview Window %0", false, 1)]
        public static void Init()
        {
            EditorWindow window = GetWindow<VCWindow>(desiredDockNextTo: typeof(SceneView));
            window.titleContent = new GUIContent("VersionControl");
        }

        private bool GUIFilter(VersionControlStatus vcStatus)
        {
            if (searchString.Length > 0)
                return vcStatus.assetPath.Compose().IndexOf(searchString, System.StringComparison.OrdinalIgnoreCase) >= 0;

            var metaStatus = vcStatus.MetaStatus();
            bool projectSetting = vcStatus.assetPath.StartsWith("ProjectSettings/");
            bool unversioned = vcStatus.fileStatus == VCFileStatus.Unversioned;
            bool meta = metaStatus.fileStatus != VCFileStatus.Normal && vcStatus.fileStatus == VCFileStatus.Normal;
            bool modifiedNoLock = !projectSetting && vcStatus.ModifiedOrLocalEditAllowed();

            bool bHidden = false;
            if (!string.IsNullOrEmpty(VCSettings.strHiddenFilePaths))
            {
                string[] arPaths = VCSettings.strHiddenFilePaths.Split(';');
                for (int i = 0; i < arPaths.Length; i++)
                {
                    string strPathToCheck = arPaths[i].Trim(new[] { ' ' });
                    string strPath = vcStatus.assetPath.Compose();
                    if (strPath == strPathToCheck ||
                    (strPathToCheck != "" && strPath.StartsWith(strPathToCheck) && strPath.Replace(strPathToCheck, "").StartsWith("/")))
                        bHidden = true;
                }
            }
            bool bExternal = (vcStatus.fileStatus == VCFileStatus.External);            

            bool bFitsFilter = 
                !((!m_bShowExternal && bExternal) || (!m_bShowUserHidden && bHidden) || (!showUnversioned && unversioned) || (!showMeta && meta) || (!showModifiedNoLock && modifiedNoLock) || (!showProjectSetting && projectSetting));

            return bFitsFilter;
            //bool rest = !unversioned && !meta && !modifiedNoLock && !projectSetting;
            //return (showUnversioned && unversioned) || (showMeta && meta) || (showModifiedNoLock && modifiedNoLock) || (showProjectSetting && projectSetting) || rest;
        }

        // This is a performance critical function
        private bool BaseFilter(VersionControlStatus vcStatus)
        {
            if (!vcStatus.Reflected) return false;

            bool assetCriteria = vcStatus.fileStatus != VCFileStatus.None && (vcStatus.ModifiedOrLocalEditAllowed() || vcStatus.fileStatus != VCFileStatus.Normal) && vcStatus.fileStatus != VCFileStatus.Ignored;
            if (assetCriteria) return true;

            bool localLock = vcStatus.lockStatus == VCLockStatus.LockedHere;
            if (localLock) return true;

            var metaStatus = vcStatus.MetaStatus();
            bool metaCriteria = metaStatus.fileStatus != VCFileStatus.Normal && metaStatus.fileStatus != VCFileStatus.None && metaStatus.fileStatus != VCFileStatus.Ignored;

            if (metaCriteria) return true;

            return false;
        }

        private void UpdateFilteringOfKeys()
        {
            vcMultiColumnAssetList.RefreshGUIFilter();
        }

        private List<string> GetSelectedAssets()
        {
            return vcMultiColumnAssetList.GetSelection().Select(status => status.assetPath).Select(cstr => cstr.Compose()).ToList();
        }

        virtual protected void OnEnable()
        {
            showUnversioned = EditorPrefs.GetBool("VCWindow/showUnversioned", true);
            showMeta = EditorPrefs.GetBool("VCWindow/showMeta", true);
            showModifiedNoLock = EditorPrefs.GetBool("VCWindow/showModifiedNoLock", true);
            statusHeight = EditorPrefs.GetFloat("VCWindow/statusHeight", 400.0f);
            m_bShowUserHidden = EditorPrefs.GetBool("VCWindow/showHidden", false);
            m_bShowExternal = EditorPrefs.GetBool("VCWindow/showExternal", false);

            vcMultiColumnAssetList = new VCMultiColumnAssetList();

            vcMultiColumnAssetList.SetBaseFilter(BaseFilter);
            vcMultiColumnAssetList.SetGUIFilter(GUIFilter);

            VCCommands.Instance.StatusCompleted += RefreshGUI;
            VCCommands.Instance.OperationCompleted += OperationComplete;
            VCCommands.Instance.ProgressInformation += ProgressInformation;
            VCSettings.SettingChanged += Repaint;
            VCSettings.SettingChanged += RefreshInfo;
            VCSettings.EnabledChanged += HandleDisableVc;
            RefreshInfo();

            rect = new Rect(0, statusHeight, position.width, 40.0f);
        }

        virtual protected void OnDisable()
        {
            EditorPrefs.SetBool("VCWindow/showUnversioned", showUnversioned);
            EditorPrefs.SetBool("VCWindow/showMeta", showMeta);
            EditorPrefs.SetBool("VCWindow/showModifiedNoLock", showModifiedNoLock);
            EditorPrefs.SetFloat("VCWindow/statusHeight", statusHeight);
            EditorPrefs.SetBool("VCWindow/showHidden", m_bShowUserHidden);
            EditorPrefs.SetBool("VCWindow/showExternal", m_bShowExternal);

            VCCommands.Instance.StatusCompleted -= RefreshGUI;
            VCCommands.Instance.OperationCompleted -= OperationComplete;
            VCCommands.Instance.ProgressInformation -= ProgressInformation;
            VCSettings.SettingChanged -= Repaint;
            VCSettings.SettingChanged -= RefreshInfo;
            VCSettings.EnabledChanged -= HandleDisableVc;


            vcMultiColumnAssetList.Dispose();
            if (updateInProgress) EditorUtility.ClearProgressBar();
        }

        private void ProgressInformation(string progress)
        {
            if (updateInProgress)
            {
                updateCounter++;
                EditorUtility.DisplayProgressBar(VCSettings.VersionControlBackend + " Updating", progress, 1.0f - (1.0f / updateCounter));
            }
            commandInProgress = commandInProgress + progress;
            if (commandInProgress.Length > maxProgressSize)
            {
                commandInProgress = commandInProgress.Substring(commandInProgress.Length - maxProgressSize);
            }
            statusScroll.y = Mathf.Infinity;
            Repaint();
        }

        private void OperationComplete(OperationType operation, IEnumerable<VersionControlStatus> statusBefore, IEnumerable<VersionControlStatus> statusAfter, bool success)
        {
            if (operation == OperationType.Update)
            {
                EditorUtility.ClearProgressBar();
                updateInProgress = false;
                RefreshGUI();
                updateCounter = 0;
            }
        }

        private void RefreshGUI()
        {
            Repaint();
        }

        private void OnGUI()
        {
            if (VCSettings.strHiddenFilePaths != m_strHiddenOld)
            {
                vcMultiColumnAssetList.RefreshGUIFilter();
                m_strHiddenOld = VCSettings.strHiddenFilePaths;
            }


            HandleInput();

            EditorGUILayout.BeginVertical();

            DrawInfo();

            DrawToolbar();


            EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeVertical);
            rect = GUIControls.DragButton(rect, GUIContent.none, null);
            rect.x = 0.0f;
            //Stella 
            rect.width = position.width;
            statusHeight = rect.y = Mathf.Clamp(rect.y, toolbarHeight + EditorGUIUtility.singleLineHeight, position.height - inStatusHeight);

            Rect rectList = new Rect(0, toolbarHeight + EditorGUIUtility.singleLineHeight + 5f, position.width, rect.y - toolbarHeight - EditorGUIUtility.singleLineHeight);
            GUILayout.BeginArea(rectList);

            vcMultiColumnAssetList.DrawGUI();
            GUILayout.EndArea();

            DrawStatus(new Rect(0, (rect.y + 6), position.width, position.height - (rect.y + 6)));

            EditorGUILayout.EndVertical();
        }

        private void HandleInput()
        {
            if (c_bNeedsRefresh)
            {
                RefreshStatus();
                c_bNeedsRefresh = false;
            }

            if (Event.current.type == EventType.KeyDown)
            {
                if (Event.current.keyCode == KeyCode.F5)
                {
                    RefreshStatus();
                    Event.current.Use();
                }
                if (Event.current.keyCode == KeyCode.Delete)
                {
                    VCUtility.VCDeleteWithConfirmation(GetSelectedAssets());
                    Event.current.Use();
                }
            }
        }

        private void RefreshStatus()
        {
            AssetDatabase.SaveAssets();
            refreshInProgress = true;
            bool remoteProjectReflection = VCSettings.ProjectReflectionMode == VCSettings.EReflectionLevel.Remote;
            VCCommands.Instance.DeactivateRefreshLoop();
            VCCommands.Instance.ClearDatabase();
            var statusLevel = remoteProjectReflection ? StatusLevel.Remote : StatusLevel.Local;
            var detailLevel = remoteProjectReflection ? DetailLevel.Verbose : DetailLevel.Normal;
            System.Threading.Tasks.Task<bool> taskStatus = VCCommands.Instance.StatusTask(statusLevel, detailLevel);
            taskStatus.ContinueWithOnNextUpdate(t =>
            {
                VCCommands.Instance.ActivateRefreshLoop();
                refreshInProgress = false;
            });


            //taskStatus.Wait();
            vcMultiColumnAssetList.RefreshSorting();
            RefreshInfo();

            RefreshGUI();
        }

        private void HandleDisableVc()
        {
            if (!VCSettings.VCEnabled)
            {
                commandInProgress = "";
                RefreshStatus();
            }
        }

        private void RefreshInfo()
        {
            string strInfo = VCCommands.Instance.Info();

            string[] arInfoLines = strInfo.Split('\r');

            if (arInfoLines.Length < 7)
            {
                m_strBranch = m_strRevision = "";
                return;
            }

            m_strBranch = arInfoLines[2].Split('/').Last();
            m_strRevision = arInfoLines[6].Split(' ')[1];
        }

        private void DrawToolbar()
        {
            GUILayoutOption[] buttonLayout = { GUILayout.MaxWidth(50) };
            {
                // Buttons at top        
                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

                using (new PushState<bool>(GUI.enabled, VCCommands.Instance.Ready && !refreshInProgress, v => GUI.enabled = v))
                {
                    // HACK
                    if (GUILayout.Button("Folder", EditorStyles.toolbarButton, buttonLayout))
                    {
                        System.Diagnostics.Process.Start(System.IO.Directory.GetParent(Application.dataPath).FullName);
                    }
                    if (GUILayout.Button(Terminology.status, EditorStyles.toolbarButton, buttonLayout))
                    {
                        RefreshStatus();
                    }
                    // Update functionality has a serious bug, where prefabs might become corrupted.
                    //if (GUILayout.Button(Terminology.update, EditorStyles.toolbarButton, buttonLayout))
                    //{
                    //    updateInProgress = true;
                    //    EditorUtility.DisplayProgressBar(VCSettings.VersionControlBackend + " Updating", "", 0.0f);
                    //    VCCommands.Instance.UpdateTask();
                    //}
                    if (GUILayout.Button(Terminology.revert, EditorStyles.toolbarButton, buttonLayout))
                    {
                        VCCommands.Instance.RevertDialog(GetSelectedAssets().ToArray());
                    }
                    if (GUILayout.Button(Terminology.delete, EditorStyles.toolbarButton, buttonLayout))
                    {
                        VCCommands.Instance.DeleteDialog(GetSelectedAssets().ToArray());
                    }
                    if (GUILayout.Button(Terminology.unlock, EditorStyles.toolbarButton, buttonLayout))
                    {
                        VCCommands.Instance.ReleaseLock(GetSelectedAssets().ToArray());
                    }
                    if (GUILayout.Button(Terminology.add, EditorStyles.toolbarButton, buttonLayout))
                    {
                        VCCommands.Instance.AddTask(GetSelectedAssets().ToArray());
                    }
                    if (GUILayout.Button(Terminology.commit, EditorStyles.toolbarButton, buttonLayout))
                    {
                        VCCommands.Instance.CommitDialog(GetSelectedAssets().ToArray(), true);
                    }
                    if (GUILayout.Button(Terminology.log, EditorStyles.toolbarButton, buttonLayout))
                    {
                        UnityVersionControl.Source.GUI.Windows.VCLogWindow.showLogWindow();
                    }

                    DrawSearchField();
                }

                GUILayout.FlexibleSpace();

                bool newShowModifiedProjectSettings = GUILayout.Toggle(showProjectSetting, "Project Settings", EditorStyles.toolbarButton, new[] { GUILayout.MaxWidth(95) });
                if (newShowModifiedProjectSettings != showProjectSetting)
                {
                    showProjectSetting = newShowModifiedProjectSettings;
                    UpdateFilteringOfKeys();
                }
                //Stella: Hide Items
                bool bNewShowUserHidden = GUILayout.Toggle(m_bShowUserHidden, "Hidden", EditorStyles.toolbarButton, new[] { GUILayout.MaxWidth(80) });
                if (bNewShowUserHidden != m_bShowUserHidden)
                {
                    m_bShowUserHidden = bNewShowUserHidden;
                    UpdateFilteringOfKeys();
                }

                //Stella: External Items
                bool bNewShowExternal = GUILayout.Toggle(m_bShowExternal, "External", EditorStyles.toolbarButton, new[] { GUILayout.MaxWidth(80) });
                if (bNewShowExternal != m_bShowExternal)
                {
                    m_bShowExternal = bNewShowExternal;
                    UpdateFilteringOfKeys();
                }


                bool newShowModifiedNoLock = GUILayout.Toggle(showModifiedNoLock, Terminology.localModified, EditorStyles.toolbarButton, new[] { GUILayout.MaxWidth(90) });
                if (newShowModifiedNoLock != showModifiedNoLock)
                {
                    showModifiedNoLock = newShowModifiedNoLock;
                    UpdateFilteringOfKeys();
                }

                bool newShowUnversioned = GUILayout.Toggle(showUnversioned, "Unversioned", EditorStyles.toolbarButton, new[] { GUILayout.MaxWidth(80) });
                if (newShowUnversioned != showUnversioned)
                {
                    showUnversioned = newShowUnversioned;
                    UpdateFilteringOfKeys();
                }

                bool newShowMeta = GUILayout.Toggle(showMeta, "Meta", EditorStyles.toolbarButton, new[] { GUILayout.MaxWidth(40) });
                if (newShowMeta != showMeta)
                {
                    showMeta = newShowMeta;
                    UpdateFilteringOfKeys();
                }

                GUILayout.Space(7.0f);

                if (GUILayout.Button("Settings", EditorStyles.toolbarButton, new[] { GUILayout.MaxWidth(55) }))
                {
                    if (settingsWindow == null)
                    {
                        settingsWindow = CreateInstance<VCSettingsWindow>();
                        settingsWindow.titleContent = new GUIContent("Version Control Settings");
                        settingsWindow.ShowUtility();
                    }
                    else
                    {
                        settingsWindow.Close();
                    }
                }

                GUILayout.Space(7.0f);

                bool vcsOn = VCSettings.VCEnabled;
                using (GUIColor(vcsOn ? Color.green : Color.red))
                {
                    if (GUILayout.Button(new GUIContent(vcsOn ? "On" : "Off", "Toggle Version Control"), EditorStyles.toolbarButton, new[] { GUILayout.MaxWidth(25) }))
                    {
                        commandInProgress = "";
                        VCSettings.VCEnabled = !VCSettings.VCEnabled;
                        RefreshStatus();
                    }
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Separator();
            }
        }

        private string m_strRevision = "";
        private string m_strBranch = "";
        private void DrawInfo()
        {
            GUILayout.BeginHorizontal();

            GUILayout.Space(20);
            GUILayout.Label("Revision: " + m_strRevision, GUILayout.Width(100));
            GUILayout.Label("Branch: " + m_strBranch, GUILayout.Width(100));

            GUILayout.EndHorizontal();
        }
        public void AddItemsToMenu(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Enabled"), VCSettings.VCEnabled, () => 
                {
                    commandInProgress = "";
                    VCSettings.VCEnabled = !VCSettings.VCEnabled;
                    RefreshStatus();
                });
        }

        private void DrawSearchField()
        {
            // Create search bar styles if needed.
            if (toolbarSearchTextStyle == null)
                toolbarSearchTextStyle = GUI.skin.FindStyle("ToolbarSeachTextField");

            if (cancelSearchButtonStyle == null)
                cancelSearchButtonStyle = GUI.skin.FindStyle("ToolbarSeachCancelButton");

            if (cancelSearchButtonEmptyStyle == null)
                cancelSearchButtonEmptyStyle = GUI.skin.FindStyle("ToolbarSeachCancelButtonEmpty");

            GUIStyle buttonStyle = searchString.Length > 0 ? cancelSearchButtonStyle : cancelSearchButtonEmptyStyle;

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal();
            searchString = EditorGUILayout.TextField(searchString, toolbarSearchTextStyle);

            if (GUILayout.Button(string.Empty, buttonStyle))
            {
                searchString = string.Empty;
                GUI.FocusControl(null); // Clears the search text field visually.
            }
            EditorGUILayout.EndHorizontal();
            if (EditorGUI.EndChangeCheck())
                UpdateFilteringOfKeys();
        }

        private void DrawStatus(Rect _rect)
        {
            GUILayout.BeginArea(_rect);
            statusScroll = EditorGUILayout.BeginScrollView(statusScroll, false, false);
            var originalColor = GUI.backgroundColor;
            if (updateInProgress) GUI.backgroundColor = activeColor;
            //GUILayout.TextArea(commandInProgress, GUILayout.ExpandHeight(true));

            Vector2 textSize = GUI.skin.label.CalcSize(new GUIContent(commandInProgress));

            EditorGUILayout.SelectableLabel( commandInProgress, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true), GUILayout.MinWidth(textSize.x),GUILayout.MinHeight(textSize.y));
            GUI.backgroundColor = originalColor;
            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        public static PushState<Color> GUIColor(Color color)
        {
            return new PushState<Color>(GUI.color, GUI.color = color, c => GUI.color = c);
        }

        public static PushState<Color> BackgroundColor(Color color)
        {
            return new PushState<Color>(GUI.backgroundColor, GUI.backgroundColor = color, c => GUI.backgroundColor = c);
        }
    }
}
