using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ATT_Wrapper.Components
    {
    public class ResultsGridController
        {
        // --- КОНСТАНТЫ СТИЛЕЙ (ШРИФТЫ И ЦВЕТА) ---

        // Шрифты
        private static readonly Font GroupHeaderFont = new Font("Segoe UI", 10F, FontStyle.Bold);
        private static readonly Font ChildRowFont = new Font("Segoe UI", 9F, FontStyle.Regular);

        // Цвета фона (Background)
        private static readonly Color GroupPassBackColor = Color.FromArgb(245, 245, 245);
        private static readonly Color GroupFailBackColor = Color.FromArgb(255, 235, 235);
        private static readonly Color ChildFailBackColor = Color.FromArgb(255, 230, 230);
        private static readonly Color ChildPassBackColor = Color.FromArgb(230, 250, 230);
        private static readonly Color DefaultBackColor = Color.White;

        // Цвета текста (Foreground)
        private static readonly Color FailForeColor = Color.FromArgb(180, 0, 0);
        private static readonly Color PassForeColor = Color.FromArgb(0, 100, 0);
        private static readonly Color DefaultForeColor = Color.Black;

        // -----------------------------------------

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
            _grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            _grid.RowTemplate.MinimumHeight = 10;
            _grid.RowTemplate.Height = 12;
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

            ClearSelection();
            }

        public void UpdateLastRow(string message)
            {
            if (_grid.Rows.Count > 0)
                _grid.Rows[_grid.Rows.Count - 1].Cells[1].Value = message;

            ClearSelection();
            }

        private void CreateGroup(string name)
            {
            int idx = _grid.Rows.Add($"▶ PASS", $"{name}: OK");
            var row = _grid.Rows[idx];

            // Использование констант
            row.DefaultCellStyle.Font = GroupHeaderFont;
            row.DefaultCellStyle.BackColor = GroupPassBackColor;

            SetRowColor(row, "PASS", isGroup: true);

            _groupRowsCache[name] = row;
            _groupChildren[row] = new List<DataGridViewRow>();

            ClearSelection();
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

            ClearSelection();
            }

        private void AddChildRow(DataGridViewRow groupRow, string status, string message)
            {
            var children = _groupChildren[groupRow];
            int insertIndex = groupRow.Index + children.Count + 1;
            if (insertIndex > _grid.Rows.Count) insertIndex = _grid.Rows.Count;

            _grid.Rows.Insert(insertIndex, status, message);
            var childRow = _grid.Rows[insertIndex];

            SetRowColor(childRow, status, isGroup: false);
            childRow.Cells[0].Style.Padding = new Padding(25, 0, 0, 0);
            childRow.Cells[1].Style.Padding = new Padding(25, 0, 0, 0);

            // Использование константы
            childRow.Cells[1].Style.Font = ChildRowFont;

            children.Add(childRow);

            bool isExpanded = groupRow.Cells[0].Value.ToString().Contains("▼");
            childRow.Visible = isExpanded;

            ClearSelection();
            }

        private void AddFlatRow(string status, string message)
            {
            int idx = _grid.Rows.Add(status, message);
            SetRowColor(_grid.Rows[idx], status);
            _grid.FirstDisplayedScrollingRowIndex = idx;
            ClearSelection();
            }

        private void OnCellClick(object sender, DataGridViewCellEventArgs e)
            {
            if (e.RowIndex < 0) return;
            var row = _grid.Rows[e.RowIndex];
            if (_groupChildren.ContainsKey(row)) ToggleGroup(row);

            ClearSelection();
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

            ClearSelection();
            }

        private void SetRowColor(DataGridViewRow row, string status, bool isGroup = false)
            {
            bool isFailOrError = ( status == "FAIL" ) || ( status == "ERROR" );

            // Логика выбора цвета фона на основе констант
            Color bg;
            if (isGroup)
                {
                bg = isFailOrError ? GroupFailBackColor : GroupPassBackColor;
                }
            else
                {
                if (isFailOrError) bg = ChildFailBackColor;
                else if (status == "PASS") bg = ChildPassBackColor;
                else bg = DefaultBackColor;
                }

            // Логика выбора цвета текста на основе констант
            Color fg = isFailOrError ? FailForeColor :
                       status == "PASS" ? PassForeColor : DefaultForeColor;

            row.DefaultCellStyle.BackColor = bg;
            row.DefaultCellStyle.ForeColor = fg;

            ClearSelection();
            }

        private void ClearSelection()
            {
            _grid.ClearSelection();
            _grid.CurrentCell = null;
            }
        }
    }