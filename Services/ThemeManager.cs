using MaterialSkin.Controls;
using Serilog;
using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace ATT_Wrapper.Services
    {
    public static class ThemeManager
        {
        public static void Apply(Form form, DataGridView grid, RichTextBox rtb, params FlowLayoutPanel[] layoutPanels)
            {
            Log.Information("Applying MaterialSkin styling to form components");

            form.Font = new Font("Segoe UI", 10F);
            form.BackColor = Color.White;

            // Настройка таблицы результатов
            grid.BackgroundColor = Color.White;
            grid.BorderStyle = BorderStyle.None;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.GridColor = Color.FromArgb(230, 230, 230);
            grid.RowTemplate.Height = 32;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(240, 245, 255);
            grid.DefaultCellStyle.SelectionForeColor = Color.Black;

            // Настройка лога
            rtb.BackColor = Color.Black;
            rtb.ForeColor = Color.White;
            rtb.Font = new Font("Cascadia Code", 10.5F);
            rtb.BorderStyle = BorderStyle.None;

            // Рекурсивная стилизация кнопок
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
                if (c is MaterialButton btn)
                    {
                    // 1. Базовые настройки MaterialSkin
                    btn.Type = MaterialButton.MaterialButtonType.Contained;
                    btn.HighEmphasis = true;
                    btn.Density = MaterialButton.MaterialButtonDensity.Default;
                    btn.Height = 50;

                    // 2. Логика растягивания (Stretching)
                    // Получаем родителя как FlowLayoutPanel явно, чтобы переменная была доступна
                    FlowLayoutPanel flp = btn.Parent as FlowLayoutPanel;
                    bool shouldStretch = false;

                    if (flp != null)
                        {
                        // Проверяем, находится ли панель внутри tabControlActions (на любой глубине)
                        shouldStretch = IsDescendantOf(flp, "tabControlActions");
                        }

                    if (shouldStretch && flp != null)
                        {
                        btn.AutoSize = false;
                        // Растягиваем на всю ширину родителя за вычетом отступов
                        btn.Width = flp.ClientSize.Width - ( btn.Margin.Left + btn.Margin.Right );
                        }
                    else
                        {
                        // Стандартное поведение
                        btn.AutoSize = true;
                        }

                    // 3. Цветовая логика (Красный для Kill/Stop)
                    if (btn.Name.Contains("Kill") || btn.Name.Contains("Stop"))
                        {
                        btn.UseAccentColor = true;
                        }
                    else
                        {
                        btn.UseAccentColor = false;
                        }
                    }

                // Рекурсивный вызов для вложенных контейнеров
                if (c.HasChildren) StyleControls(c);
                }
            }

        /// <summary>
        /// Проверяет, является ли startControl потомком (находится внутри) контрола с именем targetName.
        /// Поднимается по цепочке Parent -> Parent -> Parent.
        /// </summary>
        private static bool IsDescendantOf(Control startControl, string targetName)
            {
            Control current = startControl;
            StringBuilder pathLog = new StringBuilder();

            while (current != null)
                {
                pathLog.Append($"{current.Name} ({current.GetType().Name}) -> ");

                if (current.Name == targetName)
                    {
                    // Цель найдена!
                    return true;
                    }

                current = current.Parent;
                }

            pathLog.Append("null");

            return false;
            }

        /// <summary>
        /// Registers a FlowLayoutPanel for fluid button resizing.
        /// Ensures buttons expand to fill width but avoids scrollbars.
        /// </summary>
        private static void SetupFluidFlowPanel(FlowLayoutPanel panel)
            {
            if (panel == null) return;

            try
                {
                panel.AutoScroll = false;
                panel.HorizontalScroll.Visible = false;
                panel.WrapContents = false;
                panel.FlowDirection = FlowDirection.TopDown;

                // 1. Define logic in a local function to call it manually + on event
                void ResizeLogic()
                    {
                    panel.SuspendLayout();
                    // Width calculation: Client - Padding - small margin for safety
                    int targetWidth = panel.ClientSize.Width - ( panel.Padding.Horizontal + 6 );

                    foreach (Control c in panel.Controls)
                        {
                        if (c is Button btn)
                            {
                            // FIX: AutoSizeMode.None doesn't exist. AutoSize = false is enough.
                            btn.AutoSize = false;
                            btn.MinimumSize = new Size(100, 40);

                            if (btn.Width != targetWidth) btn.Width = targetWidth;
                            }
                        }
                    panel.ResumeLayout(true);
                    }

                // 2. Bind event
                panel.SizeChanged += (s, e) => ResizeLogic();

                // 3. Trigger immediately (Fixes 'OnSizeChanged is inaccessible' error)
                ResizeLogic();
                }
            catch (Exception ex)
                {
                Log.Error(ex, $"[ThemeManager] Failed to setup fluid layout for {panel.Name}");
                }
            }


        }
    }