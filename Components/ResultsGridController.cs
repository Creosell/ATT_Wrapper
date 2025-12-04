using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ATT_Wrapper.Components
    {
    public class ResultsGridController
        {
        private readonly DataGridView _grid;
        private readonly MappingManager _mapper;

        private Dictionary<DataGridViewRow, List<DataGridViewRow>> _groupChildren = new Dictionary<DataGridViewRow, List<DataGridViewRow>>();
        private Dictionary<string, DataGridViewRow> _groupRowsCache = new Dictionary<string, DataGridViewRow>();

        public ResultsGridController(DataGridView grid, MappingManager mapper)
            {
            _grid = grid;
            _mapper = mapper;
            SetupGrid();
            _grid.CellClick += OnCellClick;
            }

        private void SetupGrid()
            {
            _grid.Columns.Clear();
            _grid.RowHeadersVisible = false;
            _grid.ColumnHeadersVisible = false;
            _grid.Columns.Add("Status", "Status");
            _grid.Columns.Add("Component", "Component");
            _grid.Columns[0].FillWeight = 15;
            _grid.Columns[1].FillWeight = 85;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            }

        public void Clear()
            {
            _grid.Rows.Clear();
            _groupChildren.Clear();
            _groupRowsCache.Clear();
            }

        public void HandleLogMessage(string status, string message)
            {
            var (groupName, ufn) = _mapper.IdentifyCheck(message);

            if (string.IsNullOrEmpty(groupName))
                {
                AddFlatRow(status, message);
                return;
                }

            if (!_groupRowsCache.ContainsKey(groupName))
                {
                CreateGroup(groupName);
                }
            var groupRow = _groupRowsCache[groupName];

            if (status == "FAIL")
                {
                UpdateGroupToFail(groupRow, groupName);
                }

            AddChildRow(groupRow, status, ufn ?? message);
            }

        public void UpdateLastRow(string message)
            {
            if (_grid.Rows.Count > 0)
                _grid.Rows[_grid.Rows.Count - 1].Cells[1].Value = message;
            }

        private void CreateGroup(string name)
            {
            int idx = _grid.Rows.Add($"▶ PASS", $"{name}: OK");
            var row = _grid.Rows[idx];
            row.DefaultCellStyle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            row.DefaultCellStyle.BackColor = Color.FromArgb(245, 245, 245);
            SetRowColor(row, "PASS", isGroup: true);

            _groupRowsCache[name] = row;
            _groupChildren[row] = new List<DataGridViewRow>();
            }

        private void UpdateGroupToFail(DataGridViewRow row, string name)
            {
            if (!row.Cells[0].Value.ToString().Contains("FAIL"))
                {
                row.Cells[0].Value = "▼ FAIL";
                row.Cells[1].Value = $"{name}: Failed";
                SetRowColor(row, "FAIL", isGroup: true);
                }
            ToggleGroup(row, forceExpand: true);
            }

        private void AddChildRow(DataGridViewRow groupRow, string status, string message)
            {
            var children = _groupChildren[groupRow];
            int insertIndex = groupRow.Index + children.Count + 1;
            if (insertIndex > _grid.Rows.Count) insertIndex = _grid.Rows.Count;

            _grid.Rows.Insert(insertIndex, status, message);
            var childRow = _grid.Rows[insertIndex];

            SetRowColor(childRow, status, isGroup: false);
            childRow.Cells[1].Style.Padding = new Padding(25, 0, 0, 0);
            childRow.Cells[1].Style.Font = new Font("Segoe UI", 9F, FontStyle.Regular);

            children.Add(childRow);

            bool isExpanded = groupRow.Cells[0].Value.ToString().Contains("▼");
            childRow.Visible = isExpanded;
            }

        private void AddFlatRow(string status, string message)
            {
            int idx = _grid.Rows.Add(status, message);
            SetRowColor(_grid.Rows[idx], status);
            _grid.FirstDisplayedScrollingRowIndex = idx;
            _grid.ClearSelection();
            _grid.CurrentCell = null;
            }

        private void OnCellClick(object sender, DataGridViewCellEventArgs e)
            {
            if (e.RowIndex < 0) return;
            var row = _grid.Rows[e.RowIndex];
            if (_groupChildren.ContainsKey(row)) ToggleGroup(row);
            }

        private void ToggleGroup(DataGridViewRow row, bool? forceExpand = null)
            {
            var children = _groupChildren[row];
            if (children.Count == 0) return;

            bool isExpanded = children[0].Visible;
            bool newState = forceExpand ?? !isExpanded;
            if (isExpanded == newState) return;

            string txt = row.Cells[0].Value.ToString().Replace("▶ ", "").Replace("▼ ", "");
            row.Cells[0].Value = ( newState ? "▼ " : "▶ " ) + txt;

            foreach (var child in children) child.Visible = newState;
            }

        private void SetRowColor(DataGridViewRow row, string status, bool isGroup = false)
            {
            Color bg = isGroup ? ( status == "FAIL" ? Color.FromArgb(255, 235, 235) : Color.FromArgb(245, 245, 245) )
                               : ( status == "FAIL" ? Color.FromArgb(255, 230, 230) : Color.White );

            Color fg = status == "FAIL" ? Color.FromArgb(180, 0, 0) :
                       status == "PASS" ? Color.FromArgb(0, 100, 0) : Color.Black;

            if (status == "PASS" && !isGroup) bg = Color.FromArgb(230, 250, 230);

            row.DefaultCellStyle.BackColor = bg;
            row.DefaultCellStyle.ForeColor = fg;
            }
        }
    }