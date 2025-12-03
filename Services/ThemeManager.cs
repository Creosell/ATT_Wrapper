using System.Drawing;
using System.Windows.Forms;

namespace ATT_Wrapper.Services
    {
    public static class ThemeManager
        {
        public static void Apply(Form form, DataGridView grid, RichTextBox rtb)
            {
            form.Font = new Font("Segoe UI", 10F);
            form.BackColor = Color.White;

            // Grid
            grid.BackgroundColor = Color.White;
            grid.BorderStyle = BorderStyle.None;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.GridColor = Color.FromArgb(230, 230, 230);
            grid.RowTemplate.Height = 32;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(240, 245, 255);
            grid.DefaultCellStyle.SelectionForeColor = Color.Black;

            // RichTextBox
            rtb.BackColor = Color.FromArgb(30, 30, 30);
            rtb.ForeColor = Color.Gainsboro;
            rtb.Font = new Font("Consolas", 10F);
            rtb.BorderStyle = BorderStyle.None;
            rtb.WordWrap = false;
            rtb.ScrollBars = RichTextBoxScrollBars.Both;

            // Buttons Recursive
            StyleControls(form);
            }

        private static void StyleControls(Control parent)
            {
            foreach (Control c in parent.Controls)
                {
                if (c is Button btn)
                    {
                    btn.FlatStyle = FlatStyle.Flat;
                    btn.FlatAppearance.BorderSize = 0;
                    btn.Font = new Font("Segoe UI Semibold", 9.5F);
                    btn.Cursor = Cursors.Hand;

                    if (btn.Name.Contains("Kill") || btn.Name.Contains("Stop"))
                        btn.BackColor = Color.FromArgb(255, 235, 235);
                    else
                        btn.BackColor = Color.FromArgb(240, 240, 240);
                    }
                if (c.HasChildren) StyleControls(c);
                }
            }
        }
    }