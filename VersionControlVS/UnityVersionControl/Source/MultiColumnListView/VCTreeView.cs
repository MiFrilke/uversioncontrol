using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using VersionControl.UserInterface;

class VCTreeView : TreeView
{
    MultiColumnView m_View;

    public VCTreeView(MultiColumnView _View, TreeViewState state, MultiColumnHeader multiColumnHeader, bool _bSelectionColumnVisible) : base(state, multiColumnHeader)
    {
        columnIndexForTreeFoldouts = (int)MultiColumnView.Column.Name;
        m_View = _View;

        m_bSelectionColumnVisible = _bSelectionColumnVisible;
        multiColumnHeader.visibleColumnsChanged += onVisibleColumnsChanged;

        showAlternatingRowBackgrounds = true;
    }

    private bool m_bSelectionColumnVisible;
    private void onVisibleColumnsChanged(MultiColumnHeader multiColumnHeader)
    {
        if (multiColumnHeader.IsColumnVisible((int)MultiColumnView.Column.Selection) && !m_bSelectionColumnVisible)
        {
            var list = multiColumnHeader.state.visibleColumns.ToList();
            list.Remove((int)MultiColumnView.Column.Selection);
            multiColumnHeader.state.visibleColumns = list.ToArray();
        }
        else if (!multiColumnHeader.IsColumnVisible((int)MultiColumnView.Column.Selection) && m_bSelectionColumnVisible)
        {
            var list = multiColumnHeader.state.visibleColumns.ToList();
            list.Insert(0, (int)MultiColumnView.Column.Selection);
            multiColumnHeader.state.visibleColumns = list.ToArray();
        }
    }

    public void Dispose()
    {
        multiColumnHeader.visibleColumnsChanged -= onVisibleColumnsChanged;

    }

    protected override TreeViewItem BuildRoot()
    {
        VCTreeViewItem root = new VCTreeViewItem { id = 0, depth = -1, displayName = "Root" };

        List<TreeViewItem> liItems = new List<TreeViewItem>();
        for (int i = 0; i < m_View.iStatusCount(); i++)
        {
            VersionControl.VersionControlStatus status = m_View.statusAtId(i);
            VCTreeViewItem treeViewItem = new VCTreeViewItem { id = i + 1, displayName = "Meow", m_data = status };
            liItems.Add(treeViewItem);
        }

        for (int i = 0; i < liItems.Count; i++)
        {
            int iParent = m_View.iParentOfStatus(i);
            if (iParent < 0)
                root.AddChild(liItems[i]);
            else
                liItems[iParent].AddChild(liItems[i]);
        }

        SetupDepthsFromParentsAndChildren(root);

        return root;
    }



    protected override IList<TreeViewItem> BuildRows(TreeViewItem root)
    {
        return base.BuildRows(root);
    }

    protected override void RowGUI(RowGUIArgs args)
    {
        VCTreeViewItem item = (VCTreeViewItem)args.item;

        for (int i = 0; i < args.GetNumVisibleColumns(); ++i)
        {
            CellGUI(args.GetCellRect(i), item, (MultiColumnView.Column)args.GetColumn(i), ref args);
        }
    }

    public override void OnGUI(Rect rect)
    {
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Space)
        {
            var rows = GetRows();
            for (int i = 0; i < rows.Count; i++)
            {
                if (IsSelected(rows[i].id))
                    (rows[i] as VCTreeViewItem).m_bSelectedForCommit = !(rows[i] as VCTreeViewItem).m_bSelectedForCommit;
            }
        }

        base.OnGUI(rect);
    }


    void CellGUI(Rect _rectCell, VCTreeViewItem _item, MultiColumnView.Column _column, ref RowGUIArgs _args)
    {
        // Center the cell rect vertically using EditorGUIUtility.singleLineHeight.
        // This makes it easier to place controls and icons in the cells.
        CenterRectUsingSingleLineHeight(ref _rectCell);

        switch (_column)
        {
            case MultiColumnView.Column.Selection:
                drawSelectionEntry(_rectCell, _item);
                break;
            case MultiColumnView.Column.Name:
                drawNameEntry(_rectCell, _item);
                break;
            case MultiColumnView.Column.Status:
                drawStatusEntry(_rectCell, _item);
                break;
            case MultiColumnView.Column.AssetPath:
                drawAssetPathEntry(_rectCell, _item);
                break;
            case MultiColumnView.Column.Meta:
                drawMetaEntry(_rectCell, _item);
                break;
            case MultiColumnView.Column.Type:
                drawTypeEntry(_rectCell, _item);
                break;
            case MultiColumnView.Column.Owner:
                drawOwnerEntry(_rectCell, _item);
                break;
            case MultiColumnView.Column.Conflict:
                drawConflictEntry(_rectCell, _item);
                break;
            case MultiColumnView.Column.ChangeList:
                drawChangeListEntry(_rectCell, _item);
                break;
            default:
                break;
        }
    }

    #region draw Cell entries
    private void drawSelectionEntry(Rect _rectCell, VCTreeViewItem _item)
    {
        //    columnSelection = new MultiColumnState.Column(new GUIContent("[]"), data => new GUIContent(masterSelection.Contains(data) ? " ☑" : " ☐"));

        bool bOld = _item.m_bSelectedForCommit;
        _item.m_bSelectedForCommit = GUI.Toggle(_rectCell, _item.m_bSelectedForCommit, "");

        if (_item.m_bSelectedForCommit != bOld)
        {
            var rows = GetRows();
            for (int i = 0; i < rows.Count; i++)
            {
                if (IsSelected(rows[i].id) && rows[i].id != _item.id)
                    (rows[i] as VCTreeViewItem).m_bSelectedForCommit = _item.m_bSelectedForCommit;
            }
        }
    }
    private void drawNameEntry(Rect _rectCell, VCTreeViewItem _item)
    {
        if (multiColumnHeader.sortedColumnIndex == (int)MultiColumnView.Column.AssetPath)
            _rectCell.xMin += GetContentIndent(_item);
        GUI.Label(_rectCell, new GUIContent(new GUIContent(
                       image: UnityEditor.AssetDatabase.GetCachedIcon(_item.m_data.assetPath.Compose()),
                       text: System.IO.Path.GetFileName((_item.m_data.assetPath.Compose())
                     ))));
    }
    private void drawStatusEntry(Rect _rectCell, VCTreeViewItem _item)
    {
        GUI.Label(_rectCell, VCMultiColumnAssetList.GetFileStatusContent(_item.m_data));
    }
    private void drawAssetPathEntry(Rect _rectCell, VCTreeViewItem _item)
    {
        GUI.Label(_rectCell, _item.m_data.assetPath.Compose());
    }
    private void drawMetaEntry(Rect _rectCell, VCTreeViewItem _item)
    {
        GUI.Label(_rectCell, VCMultiColumnAssetList.GetFileStatusContent(_item.m_data.MetaStatus()));
    }
    private void drawTypeEntry(Rect _rectCell, VCTreeViewItem _item)
    {
        GUI.Label(_rectCell, new GUIContent(VCMultiColumnAssetList.GetFileType(_item.m_data.assetPath.Compose())));
    }
    private void drawOwnerEntry(Rect _rectCell, VCTreeViewItem _item)
    {
        GUI.Label(_rectCell, new GUIContent(_item.m_data.owner, _item.m_data.lockToken));
    }
    private void drawConflictEntry(Rect _rectCell, VCTreeViewItem _item)
    {
        GUI.Label(_rectCell, new GUIContent(_item.m_data.treeConflictStatus.ToString()));
    }
    private void drawChangeListEntry(Rect _rectCell, VCTreeViewItem _item)
    {
        GUI.Label(_rectCell, new GUIContent(_item.m_data.changelist.Compose()));
    }
    #endregion

    protected override void SelectionChanged(IList<int> selectedIds)
    {
        base.SelectionChanged(selectedIds);

        Selection.objects = selectedIds.ToList().ConvertAll(x =>
            AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                (FindItem(x, rootItem) as VCTreeViewItem).m_data.assetPath.Compose())).ToArray();
    }

    protected override void ContextClickedItem(int id)
    {
        base.ContextClickedItem(id);

        List<VersionControl.VersionControlStatus> liSelected = liSelectedData();
        if (!liSelected.Any())
            return;

        GenericMenu menu = new GenericMenu();
        if (liSelected.Count() == 1)
            VCGUIControls.CreateVCContextMenu(ref menu, liSelected.First().assetPath.Compose());
        else
            VCGUIControls.CreateVCContextMenu(ref menu, liSelected.ConvertAll(x => x.assetPath.Compose()));

        var selectedObjs = liSelected.Select(a => AssetDatabase.LoadMainAssetAtPath(a.assetPath.Compose())).ToArray();
        menu.AddSeparator("");
        menu.AddItem(new GUIContent("Show in Project"), false, () =>
        {
            Selection.objects = selectedObjs;
            EditorGUIUtility.PingObject(Selection.activeObject);
        });
        menu.AddItem(new GUIContent("Show on Harddisk"), false, () =>
        {
            Selection.objects = selectedObjs;
            EditorApplication.ExecuteMenuItem((Application.platform == RuntimePlatform.OSXEditor ? "Assets/Reveal in Finder" : "Assets/Show in Explorer"));
        });

        menu.ShowAsContext();
    }

    protected override void DoubleClickedItem(int id)
    {
        base.DoubleClickedItem(id);

        VCTreeViewItem item = FindItem(id, rootItem) as VCTreeViewItem;
        if (VersionControl.VCUtility.IsDiffableAsset(item.m_data.assetPath) && VersionControl.VCUtility.ManagedByRepository(item.m_data) 
                && item.m_data.fileStatus == VersionControl.VCFileStatus.Modified)
            VersionControl.VCUtility.DiffWithBase(item.m_data.assetPath.Compose());
        else
            AssetDatabase.OpenAsset(AssetDatabase.LoadMainAssetAtPath(item.m_data.assetPath.Compose()));
    }

    public List<VersionControl.VersionControlStatus> liSelectedData()
    {
         List<VersionControl.VersionControlStatus> liResult = new List<VersionControl.VersionControlStatus>();
        IList<TreeViewItem> liItems = FindRows(GetSelection());
        for (int i = 0; i < liItems.Count; i++)
            liResult.Add((liItems[i] as VCTreeViewItem).m_data);
        return liResult;
    }

    public class VCTreeViewItem : TreeViewItem
    {
        public VersionControl.VersionControlStatus m_data;
        public bool m_bSelectedForCommit;
    }

    public IEnumerable<VersionControl.VersionControlStatus> GetCommitSelection(bool _bSelectedOnly)
    {
        return (_bSelectedOnly ?
            GetRows().Where(x => (x as VCTreeViewItem).m_bSelectedForCommit).Select(x => (x as VCTreeViewItem).m_data) :
            GetRows().Select(x => (x as VCTreeViewItem).m_data));
    }
}

