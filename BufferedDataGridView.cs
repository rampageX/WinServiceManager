using System.Windows.Forms;

namespace WinServMgr
{
    /// <summary>
    /// DataGridView enables full-frame buffering through its protected flag.
    /// Avoid WS_EX_COMPOSITED (can slow resizing and input on Win11).
    /// </summary>
    internal sealed class BufferedDataGridView : DataGridView
    {
        internal BufferedDataGridView()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.AllPaintingInWmPaint, true);
        }
    }
}
