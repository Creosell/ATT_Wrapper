using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ATT_Wrapper.Components
    {
    public class ReportStatusManager
        {
        // Храним пару: Иконка (PictureBox) и Текст (Label)
        private readonly Dictionary<string, (PictureBox Icon, Label Label)> _items;

        // Цвета
        private readonly Color _colorPending = Color.FromArgb(189, 189, 189); // Серый
        private readonly Color _colorSuccess = Color.FromArgb(76, 175, 80);  // Зеленый
        private readonly Color _colorFail = Color.FromArgb(244, 67, 54);     // Красный

        public ReportStatusManager()
            {
            _items = new Dictionary<string, (PictureBox, Label)>(StringComparer.OrdinalIgnoreCase);
            }

        public void Register(string uploaderKey, PictureBox iconBox, Label labelControl)
            {
            if (!_items.ContainsKey(uploaderKey))
                {
                _items.Add(uploaderKey, (iconBox, labelControl));
                }
            // Рисуем исходный серый круг
            SetIconColor(iconBox, _colorPending);
            }

        public void ResetAll()
            {
            foreach (var item in _items.Values)
                {
                if (item.Icon.InvokeRequired)
                    item.Icon.BeginInvoke(new Action(() => SetIconColor(item.Icon, _colorPending)));
                else
                    SetIconColor(item.Icon, _colorPending);
                }
            }

        public void UpdateStatus(string uploaderName, string status)
            {
            if (_items.TryGetValue(uploaderName, out var item))
                {
                Color targetColor = status == "PASS" ? _colorSuccess : _colorFail;

                Action updateAction = () =>
                {
                    SetIconColor(item.Icon, targetColor);
                    // Опционально: можно менять цвет текста лейбла, если хочется
                    // item.Label.ForeColor = targetColor; 
                };

                if (item.Icon.InvokeRequired) item.Icon.BeginInvoke(updateAction);
                else updateAction();
                }
            }

        // Вспомогательный метод для рисования круга внутри PictureBox
        private void SetIconColor(PictureBox box, Color color)
            {
            int size = Math.Min(box.Width, box.Height);

            // Создаем картинку под размер PictureBox
            Bitmap bmp = new Bitmap(box.Width, box.Height);

            using (Graphics g = Graphics.FromImage(bmp))
                {
                // Включаем сглаживание, чтобы круг был ровным
                g.SmoothingMode = SmoothingMode.AntiAlias;

                using (Brush brush = new SolidBrush(color))
                    {
                    // Рисуем круг с небольшим отступом (padding 2px)
                    int padding = 2;
                    g.FillEllipse(brush, padding, padding, size - ( padding * 2 ), size - ( padding * 2 ));
                    }
                }

            // Если была старая картинка, освобождаем память
            box.Image?.Dispose();

            box.Image = bmp;
            }
        }
    }