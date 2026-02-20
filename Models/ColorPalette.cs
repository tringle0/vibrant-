using System;
using System.Drawing;
using System.Windows.Forms;

public static class ColorPalette {
    // Core colors
    public static readonly Color Background = Color.FromArgb(48, 52, 70);
    public static readonly Color Surface = Color.FromArgb(65, 69, 89);

    // Text
    public static readonly Color TextPrimary = Color.FromArgb(198, 208, 245);
    public static readonly Color TextSecondary = Color.FromArgb(165, 173, 206);

    // Accent
    public static readonly Color AccentHover = Color.FromArgb(129, 200, 190);
    public static readonly Color Accent = Color.FromArgb(131, 139, 167);

    // Status
    public static readonly Color Success = Color.FromArgb(0, 200, 120);
    public static readonly Color Warning = Color.FromArgb(255, 185, 0);
    public static readonly Color Error = Color.FromArgb(232, 17, 35);

    private static void Button_MouseEnter(object sender, EventArgs e) {
        if (sender is Button btn)
            btn.BackColor = AccentHover;
    }

    private static void Button_MouseLeave(object sender, EventArgs e) {
        if (sender is Button btn)
            btn.BackColor = Accent;
    }

    public static void ApplyTheme(Control control) {

        if(control is Button btn) {
            btn.BackColor = Accent;
            btn.ForeColor = TextPrimary;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;

            btn.MouseEnter -= Button_MouseEnter;
            btn.MouseLeave -= Button_MouseLeave;

            btn.MouseEnter += Button_MouseEnter;
            btn.MouseLeave += Button_MouseLeave;
        }
        else if (control is Panel) {
            control.BackColor = Surface;
            
        }
        else if (control is Label) {
            control.BackColor = Color.Transparent;
            control.ForeColor = TextPrimary;
        }
        else {
            control.BackColor = Background;
            control.ForeColor = TextPrimary;
        }
        if (control.Tag != null && control.Tag.Equals("outline"))
            control.BackColor = TextPrimary;

        foreach (Control child in control.Controls) {
            ApplyTheme(child);
        }
    }

}