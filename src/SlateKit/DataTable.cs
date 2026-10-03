using System;
using System.Collections.Generic;
using System.Linq;
using ContentOS.Domain;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace SlateKit;

public class DataTable<TItem> : ComponentBase
{
	private HashSet<string> _selectedIds = new HashSet<string>();

	private string SortKey = "";

	private string SortDir = "asc";

	private int _page = 1;

	[Parameter]
	public IEnumerable<TItem> Items { get; set; } = Array.Empty<TItem>();

	[Parameter]
	public List<DataTableColumn<TItem>> Columns { get; set; } = new List<DataTableColumn<TItem>>();

	[Parameter]
	public bool ShowSelectColumn { get; set; } = true;

	[Parameter]
	public bool ShowPagination { get; set; } = true;

	[Parameter]
	public int PageSize { get; set; } = 25;

	[Parameter]
	public string EmptyMessage { get; set; } = "No data available.";

	[Parameter]
	public string ItemsName { get; set; } = "items";

	[Parameter]
	public Func<TItem, string>? RowClass { get; set; }

	private bool AllSelected => _selectedIds.Count == TotalItems && TotalItems > 0;

	public int TotalItems => Items?.Count() ?? 0;

	public int TotalPages => (int)Math.Ceiling((double)TotalItems / (double)PageSize);

	private IEnumerable<TItem> PagedItems
	{
		get
		{
			IEnumerable<TItem> source = Items ?? Array.Empty<TItem>();
			if (!string.IsNullOrEmpty(SortKey))
			{
				DataTableColumn<TItem> dataTableColumn = Columns.FirstOrDefault((DataTableColumn<TItem> c) => c.Key == SortKey);
				if (dataTableColumn?.SortValue != null)
				{
					source = ((SortDir == "asc") ? source.OrderBy(dataTableColumn.SortValue) : source.OrderByDescending(dataTableColumn.SortValue));
				}
			}
			return source.Skip((_page - 1) * PageSize).Take(PageSize);
		}
	}

	protected override void BuildRenderTree(RenderTreeBuilder __builder)
	{
		__builder.OpenElement(0, "div");
		__builder.AddAttribute(1, "class", "data-table");
		if (Items == null || !Items.Any())
		{
			__builder.OpenElement(2, "div");
			__builder.AddAttribute(3, "class", "dt-empty-state");
			__builder.OpenElement(4, "p");
			__builder.AddContent(5, EmptyMessage);
			__builder.CloseElement();
			__builder.CloseElement();
		}
		else
		{
			__builder.OpenElement(6, "table");
			__builder.AddAttribute(7, "class", "table");
			__builder.OpenElement(8, "thead");
			__builder.OpenElement(9, "tr");
			if (ShowSelectColumn)
			{
				__builder.OpenElement(10, "th");
				__builder.AddAttribute(11, "class", "col-checkbox");
				__builder.OpenElement(12, "input");
				__builder.AddAttribute(13, "type", "checkbox");
				__builder.AddAttribute(14, "checked", AllSelected);
				__builder.AddAttribute(15, "onchange", EventCallback.Factory.Create<ChangeEventArgs>((object)this, (Action)ToggleAll));
				__builder.CloseElement();
				__builder.CloseElement();
			}
			foreach (DataTableColumn<TItem> col in Columns)
			{
				__builder.OpenElement(16, "th");
				__builder.AddAttribute(17, "class", col.IsSortable ? "sortable" : "");
				__builder.AddAttribute(18, "onclick", EventCallback.Factory.Create<MouseEventArgs>((object)this, (Action)delegate
				{
					Sort(col);
				}));
				__builder.AddContent(19, col.Label);
				if (col.IsSortable && SortKey == col.Key)
				{
					__builder.OpenElement(20, "span");
					__builder.AddAttribute(21, "class", "sort-dir");
					__builder.AddContent(22, (SortDir == "asc") ? "▲" : "▼");
					__builder.CloseElement();
				}
				__builder.CloseElement();
			}
			__builder.CloseElement();
			__builder.CloseElement();
			__builder.AddMarkupContent(23, "\n            ");
			__builder.OpenElement(24, "tbody");
			foreach (TItem item in PagedItems)
			{
				__builder.OpenElement(25, "tr");
				__builder.AddAttribute(26, "class", (_selectedIds.Contains(GetItemId(item)) ? "selected" : "") + " " + RowClass?.Invoke(item));
				if (ShowSelectColumn)
				{
					__builder.OpenElement(27, "td");
					__builder.AddAttribute(28, "class", "col-checkbox");
					__builder.OpenElement(29, "input");
					__builder.AddAttribute(30, "type", "checkbox");
					__builder.AddAttribute(31, "checked", _selectedIds.Contains(GetItemId(item)));
					__builder.AddAttribute(32, "onchange", EventCallback.Factory.Create<ChangeEventArgs>((object)this, (Action)delegate
					{
						ToggleRow(GetItemId(item));
					}));
					__builder.CloseElement();
					__builder.CloseElement();
				}
				foreach (DataTableColumn<TItem> column in Columns)
				{
					__builder.OpenElement(33, "td");
					__builder.AddContent(34, column.CellTemplate(item));
					__builder.CloseElement();
				}
				__builder.CloseElement();
			}
			__builder.CloseElement();
			__builder.CloseElement();
			if (ShowPagination && TotalPages > 1)
			{
				__builder.OpenElement(35, "div");
				__builder.AddAttribute(36, "class", "dt-pagination");
				__builder.OpenElement(37, "span");
				__builder.AddAttribute(38, "class", "dt-pagination-info");
				__builder.AddContent(39, _selectedIds.Count);
				__builder.AddContent(40, " of ");
				__builder.AddContent(41, TotalItems);
				__builder.AddContent(42, " ");
				__builder.AddContent(43, ItemsName);
				__builder.AddContent(44, " selected.");
				__builder.CloseElement();
				__builder.AddMarkupContent(45, "\n                ");
				__builder.OpenElement(46, "div");
				__builder.AddAttribute(47, "class", "dt-pagination-controls");
				__builder.OpenElement(48, "button");
				__builder.AddAttribute(49, "class", "btn btn-outline btn-sm");
				__builder.AddAttribute(50, "disabled", _page == 1);
				__builder.AddAttribute(51, "onclick", EventCallback.Factory.Create<MouseEventArgs>((object)this, (Action)PrevPage));
				__builder.AddContent(52, "Prev");
				__builder.CloseElement();
				__builder.AddMarkupContent(53, "\n                    ");
				__builder.OpenElement(54, "span");
				__builder.AddAttribute(55, "class", "dt-pagination-page");
				__builder.AddContent(56, "Page ");
				__builder.AddContent(57, _page);
				__builder.AddContent(58, " of ");
				__builder.AddContent(59, TotalPages);
				__builder.CloseElement();
				__builder.AddMarkupContent(60, "\n                    ");
				__builder.OpenElement(61, "button");
				__builder.AddAttribute(62, "class", "btn btn-outline btn-sm");
				__builder.AddAttribute(63, "disabled", _page == TotalPages);
				__builder.AddAttribute(64, "onclick", EventCallback.Factory.Create<MouseEventArgs>((object)this, (Action)NextPage));
				__builder.AddContent(65, "Next");
				__builder.CloseElement();
				__builder.CloseElement();
				__builder.CloseElement();
			}
		}
		__builder.CloseElement();
	}

	private string GetItemId(TItem item)
	{
		if (item is IHasId { Id: var id })
		{
			return id.ToString();
		}
		return item?.GetHashCode().ToString() ?? "";
	}

	private void ToggleAll()
	{
		if (AllSelected)
		{
			_selectedIds.Clear();
			return;
		}
		_selectedIds = new HashSet<string>(Items.Select((TItem i) => GetItemId(i)));
	}

	private void ToggleRow(string id)
	{
		if (!_selectedIds.Add(id))
		{
			_selectedIds.Remove(id);
		}
	}

	private void Sort(DataTableColumn<TItem> col)
	{
		if (col.IsSortable)
		{
			if (SortKey == col.Key)
			{
				SortDir = ((SortDir == "asc") ? "desc" : "asc");
				return;
			}
			SortKey = col.Key;
			SortDir = "asc";
		}
	}

	private void PrevPage()
	{
		if (_page > 1)
		{
			_page--;
		}
	}

	private void NextPage()
	{
		if (_page < TotalPages)
		{
			_page++;
		}
	}
}
