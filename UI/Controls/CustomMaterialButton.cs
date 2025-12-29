using MaterialSkin;
using MaterialSkin.Controls;
using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace ATT_Wrapper.Components
    {
    public class CustomMaterialButton : MaterialButton
        {
        private static readonly FieldInfo _textField = typeof(Control).GetField("text", BindingFlags.Instance | BindingFlags.NonPublic);

        public CustomMaterialButton()
            {
            this.AutoSize = false;
            this.Size = new Size(100, 50);
            }

        protected override void OnPaint(PaintEventArgs pevent)
            {
            // 1. Временно стираем текст для отрисовки фона
            string originalText = this.Text;
            _textField.SetValue(this, string.Empty);

            base.OnPaint(pevent);

            _textField.SetValue(this, originalText);

            // 2. Рисуем текст
            Graphics g = pevent.Graphics;

            TextFormatFlags flags = TextFormatFlags.WordBreak |
                                    TextFormatFlags.HorizontalCenter |
                                    TextFormatFlags.VerticalCenter |
                                    TextFormatFlags.TextBoxControl;

            Rectangle textRect = new Rectangle(
                16,
                6,
                this.Width - 32,
                this.Height - 12
            );

            Color textColor = !this.Enabled ? Color.Gray :
                              ( this.Type == MaterialButtonType.Contained ? Color.White : MaterialSkinManager.Instance.ColorScheme.PrimaryColor );

            using (Font boldFont = new Font(this.Font, FontStyle.Bold))
                {
                // --- ИЗМЕНЕНИЕ: .ToUpper() ---
                // Рисуем текст в верхнем регистре
                TextRenderer.DrawText(g, originalText.ToUpper(), boldFont, textRect, textColor, flags);
                }
            }

        public override Size GetPreferredSize(Size proposedSize)
            {
            using (Font boldFont = new Font(this.Font, FontStyle.Bold))
                {
                TextFormatFlags flags = TextFormatFlags.WordBreak |
                                        TextFormatFlags.HorizontalCenter |
                                        TextFormatFlags.TextBoxControl;

                // --- ИЗМЕНЕНИЕ: .ToUpper() ---
                // Измеряем тоже текст в верхнем регистре, чтобы расчет высоты был точным
                Size textSize = TextRenderer.MeasureText(
                    this.Text.ToUpper(),
                    boldFont,
                    new Size(Math.Max(1, proposedSize.Width - 36), 0),
                    flags
                );

                return new Size(proposedSize.Width, Math.Max(50, textSize.Height + 24));
                }
            }
        }
    }