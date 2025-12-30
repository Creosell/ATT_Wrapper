using ATT_Wrapper.UI.Controls;
using MaterialSkin.Controls;
using Serilog;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace ATT_Wrapper.UI.Helpers
    {
    public static class ThemeManager
        {
        // ==========================================
        //         КОНФИГУРАЦИЯ ШРИФТОВ
        // ==========================================
        private const string FORM_FONT_FAMILY = "Segoe UI";
        private const float FORM_FONT_SIZE = 10F;

        private const string BUTTON_FONT_FAMILY = "Segoe UI";
        private const float BUTTON_FONT_SIZE = 11F;

        private const string LOG_FONT_FAMILY = "Cascadia Code";
        private const float LOG_FONT_SIZE = 10.5F;
        // ==========================================

        public static void Apply(Form form, DataGridView grid, RichTextBox rtb, params FlowLayoutPanel[] layoutPanels)
            {
            Log.Information("Applying MaterialSkin styling to form components");

            form.Font = new Font(FORM_FONT_FAMILY, FORM_FONT_SIZE);
            form.BackColor = Color.White;

            grid.BackgroundColor = Color.White;
            grid.BorderStyle = BorderStyle.None;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.GridColor = Color.FromArgb(230, 230, 230);
            grid.RowTemplate.Height = 32;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(240, 245, 255);
            grid.DefaultCellStyle.SelectionForeColor = Color.Black;

            rtb.BackColor = Color.Black;
            rtb.ForeColor = Color.White;
            rtb.Font = new Font(LOG_FONT_FAMILY, LOG_FONT_SIZE);
            rtb.BorderStyle = BorderStyle.None;

            StyleControls(form);

            foreach (var panel in layoutPanels)
                {
                SetupFluidFlowPanel(panel);
                }
            }

        private static void StyleControls(Control parent)
            {
            foreach (Control c in parent.Controls)
                {
                if (c is CustomMaterialButton btn)
                    {
                    btn.Font = new Font(BUTTON_FONT_FAMILY, BUTTON_FONT_SIZE, FontStyle.Regular);
                    btn.Type = MaterialButton.MaterialButtonType.Contained;
                    btn.HighEmphasis = true;
                    btn.Density = MaterialButton.MaterialButtonDensity.Default;
                    btn.Height = 50;

                    FlowLayoutPanel flp = btn.Parent as FlowLayoutPanel;
                    bool shouldStretch = false;

                    if (flp != null) shouldStretch = IsDescendantOf(flp, "tabControlActions");

                    if (shouldStretch && flp != null)
                        {
                        btn.AutoSize = false;
                        btn.Width = flp.ClientSize.Width - ( btn.Margin.Left + btn.Margin.Right );
                        }
                    else
                        {
                        btn.AutoSize = true;
                        }

                    if (btn.Name.Contains("Kill") || btn.Name.Contains("Stop"))
                        btn.UseAccentColor = true;
                    else
                        btn.UseAccentColor = false;
                    }

                if (c.HasChildren) StyleControls(c);
                }
            }

        private static bool IsDescendantOf(Control startControl, string targetName)
            {
            Control current = startControl;
            while (current != null)
                {
                if (current.Name == targetName) return true;
                current = current.Parent;
                }
            return false;
            }

        private static void SetupFluidFlowPanel(FlowLayoutPanel panel)
            {
            if (panel == null) return;

            try
                {
                panel.FlowDirection = FlowDirection.TopDown;
                panel.WrapContents = false;
                panel.AutoScroll = true;
                panel.HorizontalScroll.Visible = false;
                panel.HorizontalScroll.Maximum = 0;

                // Локальная функция пересчета (чтобы вызывать её из разных событий)
                void ResizeLogic()
                    {
                    panel.SuspendLayout();

                    int availableWidth = panel.ClientSize.Width;
                    int panelPadding = panel.Padding.Horizontal;

                    foreach (Control c in panel.Controls)
                        {
                        if (!c.Visible) continue;

                        if (c is CustomMaterialButton btn)
                            {
                            btn.AutoSize = false;

                            int targetWidth = availableWidth - panelPadding - btn.Margin.Horizontal - SystemInformation.VerticalScrollBarWidth;
                            if (targetWidth < 50) targetWidth = 50;

                            if (btn.Width != targetWidth) btn.Width = targetWidth;

                            Size preferredSize = btn.GetPreferredSize(new Size(targetWidth, 0));

                            if (btn.Height != preferredSize.Height)
                                {
                                btn.Height = preferredSize.Height;
                                }
                            }
                        }
                    panel.ResumeLayout(true);
                    }

                // 1. Стандартные события панели
                panel.Layout += (s, e) => ResizeLogic();
                panel.SizeChanged += (s, e) => ResizeLogic();

                // 2. События добавления/удаления контролов
                panel.ControlAdded += (s, e) =>
                {
                    // ВАЖНО: При добавлении кнопки подписываемся на смену её текста
                    if (e.Control is CustomMaterialButton btn)
                        {
                        // Удаляем на всякий случай, чтобы не дублировать
                        btn.TextChanged -= (sender, args) => ResizeLogic();
                        // Подписываемся: Текст поменялся -> Пересчитать размеры!
                        btn.TextChanged += (sender, args) => ResizeLogic();
                        }
                    ResizeLogic();
                };

                panel.ControlRemoved += (s, e) => ResizeLogic();

                // 3. Первичная подписка для уже существующих кнопок (если они добавлены в дизайнере)
                foreach (Control c in panel.Controls)
                    {
                    if (c is CustomMaterialButton btn)
                        {
                        btn.TextChanged -= (sender, args) => ResizeLogic();
                        btn.TextChanged += (sender, args) => ResizeLogic();
                        }
                    }

                ResizeLogic();
                }
            catch (Exception ex)
                {
                Console.WriteLine($"[ThemeManager] Error setup fluid panel: {ex.Message}");
                }
            }
        }
    }