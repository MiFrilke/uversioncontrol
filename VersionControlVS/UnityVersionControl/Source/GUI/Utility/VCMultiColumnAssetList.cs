// Copyright (c) <2017> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using VersionControl.Logging;
using System.Collections;
//using MultiColumnState = MultiColumnState<VersionControl.VersionControlStatus, UnityEngine.GUIContent>;
//using MultiColumnViewOption = MultiColumnView.MultiColumnViewOption<VersionControl.VersionControlStatus>;

namespace VersionControl.UserInterface
{
    using ComposedString = ComposedSet<string, FilesAndFoldersComposedStringDatabase>;
    internal class VCMultiColumnAssetList : IDisposable
    {
        private HashSet<VersionControlStatus> commitSelection = new HashSet<VersionControlStatus>();
        private bool showMasterSelection = false;
        private Action repaint;
        private IEnumerable<VersionControlStatus> interrestingStatus;

        private MultiColumnView m_MultiColumnView;
        //private MultiColumnState multiColumnState;
        ////private MultiColumnViewOption options;

        //private MultiColumnState.Column columnSelection;
        //private MultiColumnState.Column columnAssetPath;
        //private MultiColumnState.Column columnAssetName;
        //private MultiColumnState.Column columnOwner;
        //private MultiColumnState.Column columnFileStatus;
        //private MultiColumnState.Column columnMetaStatus;
        //private MultiColumnState.Column columnFileType;
        //private MultiColumnState.Column columnConflict;
        //private MultiColumnState.Column columnChangelist;

        private Func<VersionControlStatus, bool> guiFilter;
        private Func<VersionControlStatus, bool> baseFilter;

        private static VersionControlStatus GetAssetStatus(string assetPath)
        {
            return VCCommands.Instance.GetAssetStatus(assetPath);
        }

        private static VersionControlStatus GetMetaStatus(string assetPath)
        {
            return VCCommands.Instance.GetAssetStatus(assetPath).MetaStatus();
        }

        public VCMultiColumnAssetList(Action repaint = null, bool showMasterSelection = false)
        {
            this.repaint = repaint;
            this.showMasterSelection = showMasterSelection;
            Initialize();
            interrestingStatus = VCCommands.Instance.GetFilteredAssets(baseFilter);
            initOrRefreshMultiColumnView();
            RefreshBaseFilter();

            VCCommands.Instance.StatusCompleted += RefreshGUI;
            VCSettings.SettingChanged += RefreshGUI;

            if (m_MultiColumnView != null)
                m_MultiColumnView.m_onSortingChanged += onViewSortingChanged;
        }

        public void Dispose()
        {
            if (m_MultiColumnView != null)
            {
                m_MultiColumnView.m_onSortingChanged -= onViewSortingChanged;
                m_MultiColumnView.Dispose();
            }

            VCCommands.Instance.StatusCompleted -= RefreshGUI;
            VCSettings.SettingChanged -= RefreshGUI;
        }

        private void onViewSortingChanged(MultiColumnView.Column _cFrom, MultiColumnView.Column _cTo)
        {
            if ((_cFrom == MultiColumnView.Column.AssetPath || _cTo == MultiColumnView.Column.AssetPath) && _cFrom != _cTo)
                RefreshGUIFilter();
        }

        public static GUIContent GetFileStatusContent(VersionControlStatus assetStatus)
        {
            if (assetStatus.treeConflictStatus != VCTreeConflictStatus.Normal)
                return new GUIContent(assetStatus.treeConflictStatus.ToString(), IconUtils.squareIcon.GetTexture(AssetStatusUtils.GetStatusColor(assetStatus, true)));
            return new GUIContent(AssetStatusUtils.GetStatusText(assetStatus), IconUtils.circleIcon.GetTexture(AssetStatusUtils.GetStatusColor(assetStatus, true)));
        }

        public void initOrRefreshMultiColumnView()
        {
            if (m_MultiColumnView != null)
                m_MultiColumnView.Dispose();
            m_MultiColumnView = new MultiColumnView(showMasterSelection? commitSelection : interrestingStatus, showMasterSelection);
        }

        public void RefreshSorting()
        {
            m_MultiColumnView.refreshSorting();
        }

        private void Initialize()
        {
            baseFilter = s => false;
            guiFilter = s => true;

            #region oldCode
            //    columnSelection = new MultiColumnState.Column(new GUIContent("[]"), data => new GUIContent(masterSelection.Contains(data) ? " ☑" : " ☐"));

            //    columnAssetPath = new MultiColumnState.Column(new GUIContent("AssetPath"), data =>
            //    {
            //        return new GUIContent(
            //            text: data.assetPath.Compose()
            //        );
            //    });

            //    columnAssetName = new MultiColumnState.Column(new GUIContent("Name"), data =>
            //    {
            //        return new GUIContent(
            //            image: AssetDatabase.GetCachedIcon(data.assetPath.Compose()),
            //            text: System.IO.Path.GetFileName(data.assetPath.Compose())
            //        );
            //    });

            //    columnOwner = new MultiColumnState.Column(new GUIContent("Owner"), data => new GUIContent(data.owner, data.lockToken));
            //    columnFileStatus = new MultiColumnState.Column(new GUIContent("Status"), GetFileStatusContent);
            //    columnMetaStatus = new MultiColumnState.Column(new GUIContent("Meta"), data => GetFileStatusContent(data.MetaStatus()));
            //    columnFileType = new MultiColumnState.Column(new GUIContent("Type"), data => new GUIContent(GetFileType(data.assetPath.Compose())));
            //    columnConflict = new MultiColumnState.Column(new GUIContent("Conflict"), data => new GUIContent(data.treeConflictStatus.ToString()));
            //    columnChangelist = new MultiColumnState.Column(new GUIContent("ChangeList"), data => new GUIContent(data.changelist.Compose()));

            //    var guiSkin = EditorGUIUtility.GetBuiltinSkin(EditorGUIUtility.isProSkin ? EditorSkin.Scene : EditorSkin.Inspector);
            //    multiColumnState = new MultiColumnState();

            //    multiColumnState.Comparer = (r1, r2, c) =>
            //    {
            //        var r1Text = c.GetContent(r1.data).text;
            //        var r2Text = c.GetContent(r2.data).text;
            //        if (r1Text == null) r1Text = "";
            //        if (r2Text == null) r2Text = "";
            //        //D.Log("Comparing: " + r1Text + " with " + r2Text + " : " + r1Text.CompareTo(r2Text));
            //        return String.Compare(r1Text, r2Text, StringComparison.OrdinalIgnoreCase);
            //    };

            //    Func<GenericMenu> rowRightClickMenu = () =>
            //    {
            //        var selected = multiColumnState.GetSelected().Select(status => status.assetPath.Compose());
            //        if (!selected.Any()) return new GenericMenu();
            //        GenericMenu menu = new GenericMenu();
            //        if (selected.Count() == 1) VCGUIControls.CreateVCContextMenu(ref menu, selected.First());
            //        else VCGUIControls.CreateVCContextMenu(ref menu, selected);
            //        var selectedObjs = selected.Select(a => AssetDatabase.LoadMainAssetAtPath(a)).ToArray();
            //        menu.AddSeparator("");
            //        menu.AddItem(new GUIContent("Show in Project"), false, () =>
            //        {
            //            Selection.objects = selectedObjs;
            //            EditorGUIUtility.PingObject(Selection.activeObject);
            //        });
            //        menu.AddItem(new GUIContent("Show on Harddisk"), false, () =>
            //        {
            //            Selection.objects = selectedObjs;
            //            EditorApplication.ExecuteMenuItem((Application.platform == RuntimePlatform.OSXEditor ? "Assets/Reveal in Finder" : "Assets/Show in Explorer"));
            //        });
            //        return menu;
            //    };

            //    Func<MultiColumnState.Column, GenericMenu> headerRightClickMenu = column =>
            //    {
            //        var menu = new GenericMenu();
            //        //menu.AddItem(new GUIContent("Remove"), false, () => { ToggleColumn(column); });
            //        return menu;
            //    };

            //    // Return value of true steals the click from normal selection, false does not.
            //    Func<MultiColumnState.Row, MultiColumnState.Column, bool> cellClickAction = (row, column) =>
            //    {

            //        GUI.FocusControl("");
            //        if (column == columnSelection)
            //        {
            //            var currentSelection = multiColumnState.GetSelected();
            //            if (currentSelection.Contains(row.data))
            //            {
            //                bool currentRowSelection = masterSelection.Contains(row.data);
            //                foreach (var selectionIt in currentSelection)
            //                {
            //                    if (currentRowSelection)
            //                        masterSelection.Remove(selectionIt);
            //                    else
            //                        masterSelection.Add(selectionIt);
            //                }
            //            }
            //            else
            //            {
            //                if (masterSelection.Contains(row.data))
            //                    masterSelection.Remove(row.data);
            //                else
            //                    masterSelection.Add(row.data);
            //            }

            //            return true;
            //        }

            //        return false;
            //    };

            //    Action selectionChangedAction = () =>
            //    {
            //        Selection.objects = multiColumnState.GetSelected().Select(x => AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(x.assetPath.Compose())).ToArray();
            //    };

            //    options = new MultiColumnViewOption
            //    {
            //        headerStyle = new GUIStyle(guiSkin.button),
            //        rowStyle = new GUIStyle(guiSkin.label),
            //        rowRightClickMenu = rowRightClickMenu,
            //        headerRightClickMenu = headerRightClickMenu,
            //        cellClickAction = cellClickAction,
            //        selectionChangedAction = selectionChangedAction,
            //        widths = new float[] { 200 },
            //        doubleClickAction = status =>
            //        {
            //            if (VCUtility.IsDiffableAsset(status.assetPath) && VCUtility.ManagedByRepository(status) && status.fileStatus == VCFileStatus.Modified)
            //                VCUtility.DiffWithBase(status.assetPath.Compose());
            //            else
            //                AssetDatabase.OpenAsset(AssetDatabase.LoadMainAssetAtPath(status.assetPath.Compose()));
            //        }
            //    };

            //    options.headerStyle.fixedHeight = 20.0f;
            //    options.rowStyle.onNormal.background = IconUtils.CreateSquareTexture(4, 1, new Color(0.24f, 0.5f, 0.87f, 0.75f));
            //    options.rowStyle.margin = new RectOffset(2, 2, 2, 1);
            //    options.rowStyle.border = new RectOffset(0, 0, 0, 0);
            //    options.rowStyle.padding = new RectOffset(4, 4, 0, 0);

            //    if (showMasterSelection)
            //    {
            //        multiColumnState.AddColumn(columnSelection);
            //        options.widthTable.Add(columnSelection.GetHeader().text, 25);
            //    }

            //    multiColumnState.AddColumn(columnAssetName);
            //    options.widthTable.Add(columnAssetName.GetHeader().text, 200);

            //    multiColumnState.AddColumn(columnFileStatus);
            //    options.widthTable.Add(columnFileStatus.GetHeader().text, 90);

            //    multiColumnState.AddColumn(columnAssetPath);
            //    options.widthTable.Add(columnAssetPath.GetHeader().text, 300);

            //    multiColumnState.AddColumn(columnMetaStatus);
            //    options.widthTable.Add(columnMetaStatus.GetHeader().text, 100);

            //    multiColumnState.AddColumn(columnFileType);
            //    options.widthTable.Add(columnFileType.GetHeader().text, 80);

            //    multiColumnState.AddColumn(columnOwner);
            //    options.widthTable.Add(columnOwner.GetHeader().text, 60);

            //    multiColumnState.AddColumn(columnChangelist);
            //    options.widthTable.Add(columnChangelist.GetHeader().text, 120);

            //    //columnConflictState.AddColumn(columnConflict);
            //    options.widthTable.Add(columnConflict.GetHeader().text, 80);

            //    // Initialize column sorting from saved EditorPrefs.
            //    multiColumnState.Ascending = EditorPrefs.GetBool("VCMultiColumnState_Ascending", true);
            //    int savedColumnIndex = EditorPrefs.GetInt("VCMultiColumnState_ColumnIndex", 0);
            //    if (savedColumnIndex == 2 && multiColumnState.Ascending) // AssetPath
            //        showBoldLabels = true;
            //    //Debug.Log("Init: Get saved column index: " + savedColumnIndex);
            //    //Debug.Log("Init: Get saved ascending bool: " + multiColumnState.Ascending);
            //    var savedColumn = multiColumnState.GetColumnByIndex(savedColumnIndex);
            //    //Debug.Log("Getting: " + savedColumn.GetHeader().text);
            //    if (savedColumn != null)
            //        multiColumnState.SetSortByColumn(savedColumn);
            #endregion
        }

        //bool showBoldLabels;

        public void SetBaseFilter(Func<VersionControlStatus, bool> newBaseFilter)
        {
            baseFilter = newBaseFilter;
            RefreshBaseFilter();
        }

        public void SetGUIFilter(Func<VersionControlStatus, bool> newGUIFilter)
        {
            guiFilter = newGUIFilter;
            RefreshGUIFilter();
        }

        private void RefreshBaseFilter()
        {
            ProfilerUtilities.BeginSample("MultiColumnAssetList::RefreshBaseFilter");
            interrestingStatus = VCCommands.Instance.GetFilteredAssets(baseFilter);
            //D.Log("RefreshBaseFilter, interrestingStatus.Count : " + interrestingStatus.Count());
            RefreshGUIFilter();
            ProfilerUtilities.EndSample();
        }

        public static string GetFileType(string assetPath)
        {
            int indexOfLastDot = assetPath.LastIndexOf(".", StringComparison.Ordinal);
            return (indexOfLastDot > 0) ? assetPath.Substring(assetPath.LastIndexOf(".", StringComparison.Ordinal) + 1) : (System.IO.Directory.Exists(assetPath) ? "[folder]" : "[unknown]");
        }

        public void RefreshGUIFilter()
        {
            ProfilerUtilities.BeginSample("MultiColumnAssetList::RefreshGUIFilter");

            //showBoldLabels = multiColumnState.Ascending && multiColumnState.SelectedColumnIndex == 2;

            if (m_MultiColumnView.bShowAdditionalFolders)
            {
                List<VersionControlStatus> dataWithLabels = interrestingStatus.Where(status => guiFilter(status)).ToList();
                foreach (VersionControlStatus item in dataWithLabels.ToList())
                {
                    string strPath = System.IO.Path.GetDirectoryName(item.assetPath.Compose()).Replace('\\', '/');
                    VersionControlStatus itemCurrent = item;
                    while (strPath != "")
                    {
                        int index = dataWithLabels.IndexOf(itemCurrent) + 1;
                        VersionControlStatus status = new VersionControlStatus();
                        status.assetPath = strPath;
                        status.allowLocalEdit = false;
                        status.isFolderLabel = true;

                        if (!dataWithLabels.Any(x => x.assetPath == status.assetPath))
                            dataWithLabels.Insert(index, status);


                        itemCurrent = status;
                        strPath = System.IO.Path.GetDirectoryName(itemCurrent.assetPath.Compose()).Replace('\\', '/');
                    }
                }
                m_MultiColumnView.setItems(dataWithLabels);
            }
            else
            //if (showBoldLabels)
            //{
            //    // HACK: We want to display bold folder labels above our rows like the old Unity Asset Server window.
            //    // Therefore, for each folder path we add an additional asset which only displays the path.
            //    // Used in MultiColumnView.ListViewRow to draw bold labels without any meta information when isFolderLabel is true.
            //    List<VersionControlStatus> dataWithLabels = interrestingStatus.Where(status => guiFilter(status)).ToList();
            //    foreach (VersionControlStatus item in dataWithLabels.ToList())
            //    {
            //        int index = dataWithLabels.IndexOf(item) + 1;
            //        VersionControlStatus status = new VersionControlStatus();
            //        status.assetPath = System.IO.Path.GetDirectoryName(item.assetPath.Compose());
            //        status.allowLocalEdit = false;
            //        status.isFolderLabel = true;

            //        if (!dataWithLabels.Any(x => x.assetPath == status.assetPath))
            //            dataWithLabels.Insert(index, status);
            //    }
            //    multiColumnState.Refresh(dataWithLabels);
            //}
            //else
            //{
            m_MultiColumnView.setItems(interrestingStatus.Where(status => guiFilter(status)));
            //}
            // End Hack.

            ProfilerUtilities.EndSample();
        }

  
        public IEnumerable<VersionControlStatus> GetSelection()
        {
            return m_MultiColumnView.GetSelection();
        }

        public IEnumerable<VersionControlStatus> GetMasterSelection()
        {
            return commitSelection;
        }

        public void SetMasterSelection(VersionControlStatus status, bool selected)
        {
            commitSelection.RemoveWhere(x => x.assetPath.Compose().Equals(status.assetPath.Compose())) ;

            if (selected)
                commitSelection.Add(status);
        }

        public void refreshSelection(IEnumerable<ComposedString> _assePaths)
        {
            foreach (VersionControl.VersionControlStatus status in interrestingStatus)
            {
               SetMasterSelection(status, VCSettings.IncludeDepedenciesAsDefault || _assePaths.Contains(status.assetPath));

            }

            initOrRefreshMultiColumnView();
        }

        private void RefreshGUI()
        {
            RefreshBaseFilter();
        }

        private void ToggleMasterSelection()
        {
            var selected = GetSelection();
            if (selected.Any())
            {
                bool toggle = commitSelection.Contains(selected.First());
                foreach (var item in selected)
                {
                    if (toggle)
                        commitSelection.Remove(item);
                    else
                        commitSelection.Add(item);
                }
                if (repaint != null) repaint();
            }
        }


        public IEnumerable<VersionControlStatus> GetCommitSelection(bool _bSelectedOnly)
        {
            return m_MultiColumnView.GetCommitSelection(_bSelectedOnly);
        }


        public void DrawGUI()
        {
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Space)
            {
                if (GUI.GetNameOfFocusedControl() != "Message")
                {
                    ToggleMasterSelection();
                    //Debug.Log("Toggle: " + GetMasterSelection().Count() + " - " + GUI.GetNameOfFocusedControl());
                }
                else
                {
                    Event.current.Use();
                }
            }
            //else if (Event.current.isKey)
            //    Debug.Log("Fail: " + GUI.GetNameOfFocusedControl() + " - " + GUIUtility.hotControl + " - " + GUIUtility.keyboardControl + " - " + Event.current.type + " - " + Event.current.keyCode);



            Rect rect = GUILayoutUtility.GetRect(5, float.MaxValue, 5, float.MaxValue, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            GUI.Box(rect, "");

            m_MultiColumnView.draw(rect);
            //MultiColumnView.ListView(rect, multiColumnState, options, RefreshGUIFilter);
        }
    }
}
