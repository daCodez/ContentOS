using System;
using Microsoft.AspNetCore.Components;

namespace SlateKit;

public class DataTableColumn<TItem>
{
	public string Key { get; set; } = "";

	public string Label { get; set; } = "";

	public bool IsSortable { get; set; } = false;

	public Func<TItem, MarkupString> CellTemplate { get; set; } = (TItem _) => (MarkupString)"";

	public Func<TItem, IComparable>? SortValue { get; set; }
}
public class DataTableColumn : DataTableColumn<object>
{
}
