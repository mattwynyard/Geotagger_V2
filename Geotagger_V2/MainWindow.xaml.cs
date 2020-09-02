using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;

namespace Geotagger_V2
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string mDBPath;
        private string mInputPath;
        private string mOutputPath;
        private DispatcherTimer dispatcherTimer;
        private GeotagManager manager;
        public MainWindow()
        {
            InitializeComponent();
            ProgessBar.Visibility = Visibility.Hidden;

        }

        private void BrowseDB_Button_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.Button b = sender as System.Windows.Controls.Button;
            if (b.Name == "BrowseDB")
            {
                OpenFileDialog openFileDialog = new OpenFileDialog();
                openFileDialog.Title = "Import Access Database";
                openFileDialog.Filter = "MS Access (*.mdb *.accdb)|*.mdb;*.accdb";
                openFileDialog.RestoreDirectory = true;
                openFileDialog.ShowDialog();
                
                if (mDBPath != "")
                {
                    txtBoxDB.Text = mDBPath = openFileDialog.FileName;
                }
                else
                {
                    Console.WriteLine("cancel");
                }
            } else 
            {
                FolderBrowserDialog browseFolderDialog = new FolderBrowserDialog();
                //browseFolderDialog.RestoreDirectory = true;
                browseFolderDialog.ShowDialog();
                if (b.Name == "BrowseInput")
                {
                    if (browseFolderDialog.SelectedPath != "")
                    {
                        txtBoxInput.Text = mInputPath = browseFolderDialog.SelectedPath;
                    }
                } else
                {
                    if (browseFolderDialog.SelectedPath != "")
                    {
                        txtBoxOutput.Text = mOutputPath = browseFolderDialog.SelectedPath;
                    }

                }
                   
            }
        }

        /// <summary>
        /// Fired when user clicks geotag button. Starts geotagger
        /// </summary>
        /// <param name="sender">object - the geotag button</param>
        /// <param name="e">click event</param>
        private async void Geotag_Click(object sender, RoutedEventArgs e)
        {
            
            manager = new GeotagManager();
            dispatcherTimer = new DispatcherTimer();
            dispatcherTimer.Tick += new EventHandler(DispatcherTimer_Tick);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
            dispatcherTimer.Start();

            Task worker = Task.Factory.StartNew(() =>
            {
                showProgressBar();
                manager.photoReader(mInputPath, false);
                //
            });
            await Task.WhenAll(worker);
            dispatcherTimer.Stop();
            //hideProgressBar();
        }

        public void DispatcherTimer_Tick(object sender, EventArgs args)
        {
            Console.WriteLine("tick");
            ProgressObject progress = manager.updateProgress;
            ProgessLabel.Content = progress.Message;
            ProgessBar.Value = progress.Value;
           
        }

        private void hideProgressBar()
        {
            Dispatcher.Invoke((Action)(() => {
                ProgessBar.Visibility = Visibility.Hidden;
            }));
        }
        private void showProgressBar()
        {
            Dispatcher.Invoke((Action)(() => {
                ProgessBar.Visibility = Visibility.Visible;
            }));
        }

        private void updateProgressLabel(string message)
        {
            ProgessLabel.Content= message;
        }
    }
}
