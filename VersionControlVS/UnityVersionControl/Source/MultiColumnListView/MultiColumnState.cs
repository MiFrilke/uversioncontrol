// Copyright (c) <2017> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnityVC@gmail.com>
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal class MultiColumnState<TD, TC>
{
    public class Column
    {
        public Column(TC headerElement, Func<TD, TC> getRowElementFunc)
        {
            this.headerElement = headerElement;
            this.getRowElementFunc = getRowElementFunc;
        }

        public TC GetContent(TD data)
        {
            return getRowElementFunc(data);
        }

        public TC GetHeader()
        {
            return headerElement;
        }

        readonly TC headerElement;
        readonly Func<TD, TC> getRowElementFunc;
    }

    public class Row
    {
        internal Row(ref TD data, IEnumerable<Column> cells)
        {
            this.cells = cells;
            this.data = data;
        }

        public IEnumerable<TC> Cells
        {
            get { return (from cell in cells select cell.GetContent(data)); }
        }

        public IEnumerable<Column> Columns
        {
            get { return cells; }
        }

        public readonly TD data;
        public bool selected;
        readonly IEnumerable<Column> cells;
    }

    public MultiColumnState()
    {
        Refresh(new List<TD>());
    }

    public MultiColumnState(IEnumerable<TD> domainDatas)
    {
        Refresh(domainDatas);
    }

    public void Refresh(IEnumerable<TD> domainDatas)
    {
        var tmp = rows;
        rows = domainDatas.Select((d, index) => new Row(ref d, columns)).ToList();

        // restore selection from before refresh
        if (tmp != null)
        {
            foreach (var row in tmp)
            {
                int iIndex = -1;
                if (row.selected && ((iIndex = rows.FindIndex(x => x.data.Equals(row.data))) != -1))
                {
                    rows[iIndex].selected = true;
                }
            }
        }

        SortByColumn();
    }

    public IEnumerable<Row> GetRows()
    {
        return rows;
    }

    public int GetRowCount() { return rows.Count(); }

    public IEnumerable<Column> GetColumns()
    {
        return columns;
    }

    public IEnumerable<TD> GetSelected()
    {
        return rows.Where(r => r.selected).Select(r => r.data);
    }

    public void RemoveColumn(Column column)
    {
        columns.Remove(column);
    }

    public void AddColumn(Column column)
    {
        columns.Add(column);
    }

    public bool ExistColumn(Column column)
    {
        return columns.Any(c => c == column);
    }

    public int CountColumns()
    {
        return columns.Count;
    }

    private void SortByColumn()
    {
        if (sortByColumn != null && Comparer != null)
            rows.Sort((l, r) => Comparer(l, r, sortByColumn) * (Ascending ? 1 : -1));
    }

    public void SetSortByColumn(Column column)
    {
        sortByColumn = column;
        SortByColumn();

        selectedColumnIndex = GetColumnIndex(column);
    }

    public void PerformActionOnSelected(Action<TD> action)
    {
        foreach (var selected in GetSelected())
            action(selected);
    }

    public Func<Row, Row, Column, int> Comparer { private get; set; }
    public bool Ascending { get; set; }

    public Column GetColumnByIndex(int index)
    {
        if (index >= 0 && index < columns.Count && columns.Count > index)
            return columns[index];
        else
            return null;
    }

    public int GetColumnIndex(MultiColumnState<TD, TC>.Column c)
    {
        return columns.IndexOf(c);
    }

    public int SelectedColumnIndex
    {
        get { return selectedColumnIndex; }
    }

    int selectedColumnIndex = -1;
    List<Row> rows;
    Column sortByColumn;
    readonly List<Column> columns = new List<Column>();
}
