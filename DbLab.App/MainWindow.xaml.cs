using DbLab.App.ViewModels;
using System.Windows;

namespace DbLab.App2
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }
    }
}
