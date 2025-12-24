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
        /// Настраивает панель:
        /// 1. Ширина кнопок подгоняется под ширину панели (минус скролл).
        /// 2. Высота кнопок АВТОМАТИЧЕСКИ увеличивается, если текст не влезает.
        /// 3. Текст выравнивается по левому краю для удобства чтения.
        /// </summary>
        private static void SetupFluidFlowPanel(FlowLayoutPanel panel)
            {
            if (panel == null) return;

            try
                {
                // Базовые настройки панели
                panel.FlowDirection = FlowDirection.TopDown;
                panel.WrapContents = false;
                panel.AutoScroll = true;
                panel.HorizontalScroll.Visible = false;
                panel.HorizontalScroll.Maximum = 0;

                void ResizeLogic()
                    {
                    panel.SuspendLayout();

                    // 1. Вычисляем доступную ширину
                    int availableWidth = panel.ClientSize.Width;
                    int panelPadding = panel.Padding.Horizontal;

                    // Минимальная высота кнопки (для коротких названий)
                    int minHeight = 50; // Чуть увеличим базу, чтобы текст дышал

                    foreach (Control c in panel.Controls)
                        {
                        if (!c.Visible) continue;

                        if (c is Button btn)
                            {
                            btn.AutoSize = false;

                            // --- ШАГ 1: ВЫРАВНИВАНИЕ ---
                            // Для списков левое выравнивание - это стандарт.
                            // К сожалению, MaterialButton иногда игнорирует это, 
                            // но для обычных кнопок это критично.
                            btn.TextAlign = ContentAlignment.MiddleLeft;

                            // --- ШАГ 2: РАСЧЕТ ШИРИНЫ ---
                            // Отнимаем отступы (Margin) самой кнопки
                            int targetWidth = availableWidth - panelPadding - btn.Margin.Horizontal - SystemInformation.VerticalScrollBarWidth;

                            // Страховка
                            if (targetWidth < 50) targetWidth = 50;
                            if (btn.Width != targetWidth) btn.Width = targetWidth;

                            // --- ШАГ 3: РАСЧЕТ ВЫСОТЫ (УМНЫЙ) ---
                            // Спрашиваем у графического движка: "Сколько места займет этот текст при такой ширине?"
                            // TextFormatFlags.WordBreak разрешает перенос слов.
                            Size textSize = TextRenderer.MeasureText(
                                btn.Text,
                                btn.Font,
                                new Size(targetWidth - 20, 0), // -20 это внутренний Padding кнопки (слева/справа)
                                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl
                            );

                            // Если текста мало -> берем minHeight (50px).
                            // Если текста много -> берем высоту текста + отступы сверху/снизу (например +20px).
                            int requiredHeight = Math.Max(minHeight, textSize.Height + 20);

                            if (btn.Height != requiredHeight)
                                {
                                btn.Height = requiredHeight;
                                }
                            }
                        }

                    panel.ResumeLayout(true);
                    }

                // Подписки
                panel.Layout += (s, e) => ResizeLogic();
                panel.SizeChanged += (s, e) => ResizeLogic();
                panel.ControlAdded += (s, e) => ResizeLogic();
                panel.ControlRemoved += (s, e) => ResizeLogic();

                // Запуск
                ResizeLogic();
                }
            catch (Exception ex)
                {
                Console.WriteLine($"[ThemeManager] Error setup fluid panel: {ex.Message}");
                }
            }


        }
    }