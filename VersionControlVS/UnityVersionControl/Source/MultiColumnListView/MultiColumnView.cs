// Copyright (c) <2017> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.IMGUI.Controls;

using TC = UnityEngine.GUIContent;


internal class MultiColumnView
{


    public enum Column
    {
        Selection, Name, Status, AssetPath, Meta, Type, Owner, Conflict, ChangeList
    }

    private List<VersionControl.VersionControlStatus> m_liItems;
    private VCTreeView m_treeView;

    public bool bShowAdditionalFolders
    {
        get
        {
            return m_treeView.multiColumnHeader.sortedColumnIndex == ((int)Column.AssetPath);
        }
    }

    #region used by TreeView

    public VersionControl.VersionControlStatus statusAtId(int _iID)
    {
        return m_liItems[_iID];
    }

    public int iParentOfStatus(int _iID, Dictionary<string, int> _dicDirectoryPathIdx)
    {
        if ((Column)m_treeView.multiColumnHeader.sortedColumnIndex == Column.AssetPath)
        {
            string strDirPath = System.IO.Path.GetDirectoryName(m_liItems[_iID].assetPath.Compose()).Replace('\\', '/');

            int iDirIdx;
            if (!_dicDirectoryPathIdx.TryGetValue(strDirPath, out iDirIdx))
                iDirIdx = -1;

            return iDirIdx;
        }
        else
            return -1;
    }

    public int iStatusCount()
    {
        return m_liItems.Count;
    }
    #endregion

    public MultiColumnView(IEnumerable<VersionControl.VersionControlStatus> _interrestingStatus, bool bIsCommit)
    {
        m_liItems = _interrestingStatus.ToList();


        MultiColumnHeaderState.Column[] arColumns = new MultiColumnHeaderState.Column[Enum.GetValues(typeof(Column)).Length];

        foreach (Column c in Enum.GetValues(typeof(Column)))
        {
            arColumns[(int)c] = new MultiColumnHeaderState.Column()
            {
                width = EditorPrefs.GetFloat("VCMultiColumnState_" + c.ToString(), 50),
                headerContent = new GUIContent(c.ToString())
            };
        }

        MultiColumnHeaderState multiColumnHeaderState = new MultiColumnHeaderState(arColumns);
        MultiColumnHeader multiColumnHeader = new MultiColumnHeader(multiColumnHeaderState);
        multiColumnHeader.sortingChanged += onSortingChanged;

        TreeViewState state = new TreeViewState();
        m_treeView = new VCTreeView(this, state, multiColumnHeader, bIsCommit && VersionControl.VCSettings.SelectiveCommit);

        m_treeView.Reload();

        multiColumnHeader.sortedColumnIndex = EditorPrefs.GetInt("VCMultiColumnState_ColumnIndex", (int)Column.AssetPath);
        multiColumnHeader.SetSortDirection(multiColumnHeader.sortedColumnIndex, EditorPrefs.GetBool("VCMultiColumnState_Ascending", false));
        string strVisible = EditorPrefs.GetString("VCMultiColumnState_Visible", "all");
        if (strVisible != "all")
        {
            multiColumnHeader.state.visibleColumns = Array.ConvertAll(strVisible.Split('_'), x => Int32.Parse(x));
        }

        m_columnSortedBefore = (Column)multiColumnHeader.sortedColumnIndex;
    }

    public void refreshSorting()
    {
        m_liItems.Sort(compareItems);

        if (m_onSortingChanged != null)
            m_onSortingChanged(m_columnSortedBefore, m_columnSortedBefore);
    }

    public void Dispose()
    {
        if (m_treeView != null)
            m_treeView.Dispose();

        foreach (Column c in Enum.GetValues(typeof(Column)))
            EditorPrefs.SetFloat("VCMultiColumnState_" + c.ToString(), m_treeView.multiColumnHeader.GetColumn((int)c).width);

        EditorPrefs.SetInt("VCMultiColumnState_ColumnIndex", m_treeView.multiColumnHeader.sortedColumnIndex);
        EditorPrefs.SetBool("VCMultiColumnState_Ascending", m_treeView.multiColumnHeader.IsSortedAscending(m_treeView.multiColumnHeader.sortedColumnIndex));

        EditorPrefs.SetString("VCMultiColumnState_Visible", string.Join("_", Array.ConvertAll(m_treeView.multiColumnHeader.state.visibleColumns, x => x.ToString())));

        m_treeView.multiColumnHeader.sortingChanged -= onSortingChanged;
    }

    public delegate void SortingChanged(Column _cFrom, Column _cTo);
    public SortingChanged m_onSortingChanged;

    private Column m_columnSortedBefore;
    private void onSortingChanged(MultiColumnHeader _header)
    {
        if (m_columnSortedBefore == (Column)_header.sortedColumnIndex)
            return;

        m_liItems.Sort(compareItems);

        rebuildTreeView();
        if (m_onSortingChanged != null)
            m_onSortingChanged(m_columnSortedBefore, (Column)_header.sortedColumnIndex);
        m_columnSortedBefore = (Column)_header.sortedColumnIndex;

    }

    private int compareItems(VersionControl.VersionControlStatus _vcs1, VersionControl.VersionControlStatus _vcs2)
    {
        string str1 = "";
        string str2 = "";

        switch ((Column)m_treeView.multiColumnHeader.sortedColumnIndex)
        {
            case Column.Selection:
                //Todo: if selected set to a, if not to b
                break;
            case Column.Name:
                str1 = System.IO.Path.GetFileName((_vcs1.assetPath.Compose()));
                str2 = System.IO.Path.GetFileName((_vcs2.assetPath.Compose()));
                break;
            case Column.Status:
                str1 = VersionControl.UserInterface.VCMultiColumnAssetList.GetFileStatusContent((_vcs1)).text;
                str2 = VersionControl.UserInterface.VCMultiColumnAssetList.GetFileStatusContent((_vcs2)).text;
                break;
            case Column.AssetPath:
                str1 = _vcs1.assetPath.Compose();
                str2 = _vcs2.assetPath.Compose();
                break;
            case Column.Meta:
                str1 = VersionControl.UserInterface.VCMultiColumnAssetList.GetFileStatusContent((_vcs1.MetaStatus())).text;
                str2 = VersionControl.UserInterface.VCMultiColumnAssetList.GetFileStatusContent((_vcs2.MetaStatus())).text;
                break;
            case Column.Type:
                str1 = VersionControl.UserInterface.VCMultiColumnAssetList.GetFileType((_vcs1.assetPath.Compose()));
                str2 = VersionControl.UserInterface.VCMultiColumnAssetList.GetFileType((_vcs2.assetPath.Compose()));
                break;
            case Column.Owner:
                str1 = _vcs1.owner;
                str2 = _vcs2.owner;
                break;
            case Column.Conflict:
                str1 = _vcs1.treeConflictStatus.ToString();
                str2 = _vcs2.treeConflictStatus.ToString();
                break;
            case Column.ChangeList:
                str1 = _vcs1.changelist.Compose();
                str2 = _vcs2.changelist.Compose();
                break;
            default:
                break;
        }

        if (m_treeView.multiColumnHeader.IsSortedAscending(m_treeView.multiColumnHeader.sortedColumnIndex))
            return String.Compare(str2, str1, StringComparison.OrdinalIgnoreCase);
        else
            return String.Compare(str1, str2, StringComparison.OrdinalIgnoreCase);
    }

    public void setItems(IEnumerable<VersionControl.VersionControlStatus> _interrestingStatus)
    {
        m_liItems = _interrestingStatus.ToList();
        rebuildTreeView();
    }

    public void rebuildTreeView()
    {
        refreshSorting();
        m_treeView.Reload();
    }

    public IEnumerable<VersionControl.VersionControlStatus> GetSelection()
    {
        return m_treeView.liSelectedData();
    }



    public IEnumerable<VersionControl.VersionControlStatus> GetCommitSelection(bool _bSelectedOnly)
    {
        return m_treeView.GetCommitSelection(_bSelectedOnly);
    }

    public void draw(Rect _rect)
    {
        m_treeView.OnGUI(_rect);
    }

    //    public class MultiColumnViewOption<TD>
    //    {
    //        public GUIStyle headerStyle;
    //        public GUIStyle rowStyle;
    //        public Func<MultiColumnState<TD, TC>.Column, GenericMenu> headerRightClickMenu;
    //        public Func<GenericMenu> rowRightClickMenu;
    //        public Func<MultiColumnState<TD, TC>.Row, MultiColumnState<TD, TC>.Column, bool> cellClickAction;
    //        public Action selectionChangedAction;
    //        public float[] widths;
    //        public Vector2 scrollbarPos;
    //        public readonly Dictionary<string, float> widthTable = new Dictionary<string, float>();
    //        public Action<TD> doubleClickAction;
    //    }

    //    static bool InBetween(int n, int start, int end) { return ((n >= start && n <= end) || (n <= start && n >= end)); }
    //    static readonly int listViewHash = "MultiColumnView.ListView".GetHashCode();
    //    static int selectedIdx = -1;

    //    public static void ListView<TD>(Rect rect, MultiColumnState<TD, TC> multiColumnState, MultiColumnViewOption<TD> mvcOption, Action onReorder)
    //    {
    //        VersionControl.ProfilerUtilities.BeginSample("MultiColumnView::ListView");
    //        bool controlModifier = ((Application.platform == RuntimePlatform.OSXEditor) ? Event.current.command : Event.current.control);

    //        GUI.BeginGroup(rect);
    //        float headerHeight = mvcOption.headerStyle.lineHeight + mvcOption.headerStyle.margin.vertical + mvcOption.headerStyle.padding.vertical;
    //        float rowHeight = mvcOption.rowStyle.lineHeight + mvcOption.rowStyle.margin.vertical;

    //        float scrollbarWidth = 0.0f;
    //        float total = multiColumnState.GetRowCount();
    //        int size = Mathf.FloorToInt((rect.height - headerHeight) / rowHeight);
    //        if (total > size)
    //        {
    //            scrollbarWidth = 16.0f;
    //            mvcOption.scrollbarPos.y = GUI.VerticalScrollbar(new Rect(rect.width - scrollbarWidth, 0, rect.width, rect.height), mvcOption.scrollbarPos.y, size, 0, total);
    //            if (rect.Contains(Event.current.mousePosition) && Event.current.type == EventType.ScrollWheel)
    //            {
    //                mvcOption.scrollbarPos.y += Mathf.Sign(Event.current.delta.y) * 3.0f;
    //                Event.current.Use();
    //            }
    //        }

    //        GUI.BeginGroup(new Rect(0, 0, rect.width - scrollbarWidth, rect.height));
    //        var headers = multiColumnState.GetColumns().Select(c => c.GetHeader());
    //        var widths = headers.Select(c => mvcOption.widthTable[c.text]).ToArray();
    //        float maxWidth = widths.Sum();

    //        var headerRect = new Rect(0, 0, maxWidth, headerHeight);
    //        ListViewHeader(headerRect, c =>
    //        {
    //            // When sorting by AssetPath, we actually want to enfore ascending sort order.
    //            if (c.GetHeader().text != "AssetPath")
    //                multiColumnState.Ascending = !multiColumnState.Ascending;
    //            else
    //                multiColumnState.Ascending = true;

    //            multiColumnState.SetSortByColumn(c);

    //            if (onReorder != null)
    //                onReorder.Invoke();

    //            // Save column sorting for restore on window relaunch.
    //            int index = multiColumnState.GetColumnIndex(c);
    //            EditorPrefs.SetBool("VCMultiColumnState_Ascending", multiColumnState.Ascending);
    //            EditorPrefs.SetInt("VCMultiColumnState_ColumnIndex", index);
    //            //Debug.Log("Save: column index: " + index);
    //            //Debug.Log("Save: ascending bool: " + multiColumnState.Ascending);
    //        },
    //            () => false, multiColumnState.GetColumns(), mvcOption);

    //        int lowIdx = Mathf.RoundToInt(mvcOption.scrollbarPos.y);
    //        int highIdx = lowIdx + size;
    //        var totalRows = multiColumnState.GetRows();
    //        var rows = totalRows.Where((_, idx) => InBetween(idx, lowIdx, highIdx));

    //        int currentIdx = lowIdx;
    //        float rowHeighStart = headerHeight;
    //        foreach (var rowIt in rows)
    //        {
    //            //D.Log("C# null: " + ((rowIt.data==null)?"true":"false") + ", Unity: " + rowIt.data + ", Type: " + rowIt.data.GetType());
    //            Action selectAction = () =>
    //                {
    //                    if (!controlModifier)
    //                        foreach (var r in totalRows)
    //                            r.selected = false;

    //                    if ((Event.current.modifiers & EventModifiers.Shift) > 0)
    //                    {
    //                        var selection = totalRows.Where((_, idx) => InBetween(idx, selectedIdx, currentIdx));
    //                        foreach (var e in selection)
    //                            e.selected = true;
    //                        mvcOption.selectionChangedAction();
    //                    }
    //                    else
    //                    {
    //                        selectedIdx = currentIdx;
    //                        rowIt.selected = !rowIt.selected;
    //                        mvcOption.selectionChangedAction();
    //                    }
    //                    if (Event.current.clickCount > 1)
    //                    {
    //                        var selection = totalRows.Where(idx => idx.selected);
    //                        foreach (var e in selection)
    //                            mvcOption.doubleClickAction(e.data);
    //                    }
    //                };

    //            var rowRect = new Rect(0, rowHeighStart, maxWidth, rowHeight);
    //            ListViewRow(rowRect, selectAction, () => rowIt.selected, rowIt, widths, mvcOption);

    //            rowHeighStart += rowHeight;
    //            currentIdx++;
    //        }
    //        GUI.EndGroup();
    //        GUI.EndGroup();


    //        int id = GUIUtility.GetControlID(listViewHash, FocusType.Passive);
    //        Event ev = Event.current;
    //        EventType evt = ev.GetTypeForControl(id);
    //        if (rect.Contains(ev.mousePosition) && evt == EventType.MouseDown && ev.button == 0)
    //        {
    //            Event.current.Use();
    //            foreach (var r in totalRows)
    //                r.selected = false;
    //        }


    //        if (controlModifier && Event.current.keyCode == KeyCode.A)
    //        {
    //            foreach (var r in totalRows)
    //                r.selected = true;
    //            Event.current.Use();
    //        }
    //        VersionControl.ProfilerUtilities.EndSample();
    //    }



    //    static readonly int listViewCellHash = "MultiColumnView.ListViewCell".GetHashCode();
    //    const float dragResize = 10.0f;
    //    enum DragType { Normal, Resize }
    //    static DragType dragTypeControl = DragType.Normal;

    //    static void ListViewHeader<TD>(Rect rect, Action<MultiColumnState<TD, TC>.Column> action, Func<bool> selectedFunc, IEnumerable<MultiColumnState<TD, TC>.Column> columns, MultiColumnViewOption<TD> mvcOption)
    //    {

    //        float x = rect.x;
    //        foreach (var columnIt in columns)
    //        {
    //            var cell = columnIt.GetHeader();
    //            float width = mvcOption.widthTable[cell.text];
    //            var r = new Rect(x, rect.y, width, rect.height);
    //            bool bHover = r.Contains(Event.current.mousePosition);
    //            Action<Vector2> dragAction = v => { mvcOption.widthTable[cell.text] = Mathf.Max(mvcOption.widthTable[cell.text] + v.x, dragResize); };

    //            ListViewCell<TD>(r, () => action(columnIt), dragAction, selectedFunc, bHover, cell, mvcOption.headerStyle, () => mvcOption.headerRightClickMenu(columnIt), () => false);
    //            x += width;
    //        }
    //    }

    //    static void ListViewRow<TD>(Rect rect, Action action, Func<bool> selectedFunc, MultiColumnState<TD, TC>.Row row, float[] widths, MultiColumnViewOption<TD> mvcOption)
    //    {
    //        //int id = GUIUtility.GetControlID(listViewCellHash, FocusType.Native);
    //        var columns = row.Columns.ToArray();
    //        bool bHover = rect.Contains(Event.current.mousePosition);
    //        float x = rect.x;

    //        for (int i = 0; i < widths.Length && i < columns.Length; ++i)
    //        {
    //            var width = widths[i];
    //            var column = columns[i];

    //            var r = new Rect(x, rect.y, width, rect.height);

    //            // HACK: For each dummy item, which only represents a folder, we want to draw it as a bold label above.
    //            // Only works when rows are sorted by ascending asset path.
    //            VersionControl.VersionControlStatus status = row.data as VersionControl.VersionControlStatus;
    //            GUIStyle style = mvcOption.rowStyle;
    //            style.padding.left = 10;
    //            style.padding.right = 5;
    //            GUIContent content = column.GetContent(row.data);

    //            if (status.isFolderLabel)
    //            {
    //                style = EditorStyles.boldLabel;
    //                content = new GUIContent(status.assetPath.Compose());

    //                // Only display the name column with the folder path and leave all others empty.
    //                if (column.GetHeader().text != "Name")
    //                    content = GUIContent.none;
    //            }
    //            // End hack.

    //            ListViewCell<TD>(r, action, _ => { }, selectedFunc, bHover, content, style, mvcOption.rowRightClickMenu, () => mvcOption.cellClickAction(row, column));
    //            x += width;
    //        }
    //    }

    //    static void ListViewCell<TD>(Rect rect, Action action, Action<Vector2> dragAction, Func<bool> selectedFunc, bool bHover, GUIContent content, GUIStyle style, Func<GenericMenu> contextMenu, Func<bool> cellClickAction)
    //    {
    //        int id = GUIUtility.GetControlID(listViewCellHash, FocusType.Passive);
    //        Event e = Event.current;
    //        switch (e.GetTypeForControl(id))
    //        {
    //            case EventType.ContextClick: ListViewCellContext(id, e, rect, contextMenu()); break;
    //            case EventType.MouseDown: ListViewCellMouseDown(id, e, rect, action, cellClickAction); break;
    //            case EventType.MouseUp: ListViewCellMouseUp(id, e, rect, action); break;
    //            case EventType.MouseDrag: ListViewCellMouseDrag(id, e, rect, dragAction); break;
    //            case EventType.Repaint: ListViewCellRepaint(id, e, rect, selectedFunc, bHover, content, style); break;
    //        }
    //    }

    //    static void ListViewCellContext(int id, Event e, Rect rect, GenericMenu contextMenu)
    //    {
    //        if (rect.Contains(e.mousePosition))
    //        {
    //            if (contextMenu != null) contextMenu.DropDown(new Rect(e.mousePosition.x, e.mousePosition.y, rect.width, rect.height));
    //            Event.current.Use();
    //            //GUIUtility.hotControl = id;
    //        }
    //    }

    //    static void ListViewCellMouseDown(int id, Event e, Rect rect, Action action, Func<bool> cellClickAction)
    //    {
    //        if (rect.Contains(e.mousePosition) && e.button == 0)
    //        {
    //            var r = new Rect(rect.xMax - dragResize, rect.y, dragResize, rect.height);
    //            if (r.Contains(e.mousePosition))
    //            {
    //                dragTypeControl = DragType.Resize;
    //            }
    //            else
    //            {
    //                dragTypeControl = DragType.Normal;
    //                if (!cellClickAction())
    //                    action();
    //            }
    //            GUIUtility.hotControl = id;
    //            Event.current.Use();
    //        }
    //    }

    //    static void ListViewCellMouseUp(int id, Event e, Rect rect, Action action)
    //    {
    //        if (GUIUtility.hotControl == id)
    //        {
    //            GUIUtility.hotControl = 0;
    //            Event.current.Use();
    //        }
    //    }

    //    static void ListViewCellMouseDrag(int id, Event e, Rect rect, Action<Vector2> dragAction)
    //    {
    //        if (GUIUtility.hotControl == id && dragTypeControl == DragType.Resize)
    //        {
    //            dragAction(Event.current.delta);
    //            Event.current.Use();
    //        }
    //    }

    //    static void ListViewCellRepaint(int id, Event e, Rect rect, Func<bool> selectedFunc, bool bHover, GUIContent content, GUIStyle style)
    //    {
    //        var r = new Rect(rect.xMax - dragResize, rect.y, dragResize, rect.height);
    //        EditorGUIUtility.AddCursorRect(r, MouseCursor.ResizeHorizontal);

    //        bool bActive = GUIUtility.hotControl == id && dragTypeControl != DragType.Resize;
    //        bool bOn = selectedFunc();
    //        bool bKeyboardFocus = GUIUtility.keyboardControl == id;

    //        // Draw image so that it doesn't get scaled first. Instead crop text.
    //        if (content.text != null && content.text.Length > 0 && content.image != null)
    //        {
    //            Rect splitRect = new Rect(rect);
    //            style.Draw(splitRect, new GUIContent(content.image), bHover, bActive, bOn, bKeyboardFocus);
    //            splitRect.x += style.lineHeight + style.padding.left;
    //            splitRect.width -= style.lineHeight + style.padding.left;
    //            style.Draw(splitRect, content.text, bHover, bActive, bOn, bKeyboardFocus);
    //        }
    //        else
    //            style.Draw(rect, content, bHover, bActive, bOn, bKeyboardFocus);
    //    }
}
