using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;


namespace RS_Tools.Tools.FileTileCloner
{
    /// <summary>
    /// Interaction logic for FileTileClonerView.xaml
    /// </summary>
    public partial class FileTileClonerView : UserControl
    {
        public FileTileClonerView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Prevents spaces from being entered in file extension combo box
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ComboBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space) e.Handled = true;
        }

    }
}
