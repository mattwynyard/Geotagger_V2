using System;
using System.Diagnostics;
using System.Threading;
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
        private string mInputReadPath;
        private DispatcherTimer dispatcherTimer;
        private GTWriter manager;
        private Stopwatch stopwatch;
        private Boolean timer = false;
        private int startCount = 0;
        public MainWindow()
        {
            InitializeComponent();
            ProgressBar1.Visibility = Visibility.Hidden;
            ProgressText.Visibility = Visibility.Hidden;

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
                browseFolderDialog.ShowDialog();
                if (TabItemWrite.IsSelected) //write
                {
                    if (b.Name == "BrowseInput")
                    {
                        if (browseFolderDialog.SelectedPath != "")
                        {
                            txtBoxInput.Text = mInputPath = browseFolderDialog.SelectedPath;
                        }
                    }
                    else
                    {
                        if (browseFolderDialog.SelectedPath != "")
                        {
                            txtBoxOutput.Text = mOutputPath = browseFolderDialog.SelectedPath;
                        }
                    }
                } else //read
                {
                    if (browseFolderDialog.SelectedPath != "")
                    {
                        txtBoxPhotoRead.Text = mInputPath = browseFolderDialog.SelectedPath;
                    }
                }
                   
            }
        }

        private void GeotagRead_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("click");
            if (manager == null)
            {
                manager = GTWriter.Instance(50);
            }
        }

        /// <summary>
        /// Fired when user clicks geotag button. Starts geotagger
        /// </summary>
        /// <param name="sender">object - the geotag button</param>
        /// <param name="e">click event</param>
        private async void Geotag_Click(object sender, RoutedEventArgs e)
        {

            manager = GTWriter.Instance(50);
            dispatcherTimer = new DispatcherTimer();
            stopwatch = new Stopwatch();
            stopwatch.Start();
            dispatcherTimer.Tick += new EventHandler(DispatcherTimer_Tick);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
            dispatcherTimer.Start();
            Timer();
            BrowseDB.IsEnabled = false;
            BrowseInput.IsEnabled = false;
            BrowseOutput.IsEnabled = false;
            Geotag.IsEnabled = false;
            var source = new CancellationTokenSource();
            CancellationToken token = source.Token;
            Task worker = Task.Factory.StartNew(() =>
            {
                showProgressBar();
                progressIndeterminate(true);
                manager.photoReader(mInputPath, false);
                progressIndeterminate(false);
                TaskStatus result = manager.readDatabase(mDBPath, "").Result;
                Console.WriteLine(result);
                
                if (result == TaskStatus.RanToCompletion)
                {
                    if (manager.updateDuplicateCount == 0)
                    {
                       TaskStatus consumerStatus = manager.writeGeotag(mOutputPath).Result;
                        if (consumerStatus == TaskStatus.RanToCompletion)
                        {
                            Dispatcher.Invoke((Action)(() =>
                            {
                                refreshUI();

                            }));
                        }
                        else
                        {
                            Console.WriteLine(consumerStatus);
                        }
                    } else
                    {
                        source.Cancel();
                    }
                    if (token.IsCancellationRequested)
                    token.ThrowIfCancellationRequested();
                }
                else
                {
                    Console.WriteLine(result);
                }
            }, token);
            await Task.WhenAll(worker);
            BrowseDB.IsEnabled = true;
            BrowseInput.IsEnabled = true;
            BrowseOutput.IsEnabled = true;
            Geotag.IsEnabled = true;
            dispatcherTimer.Stop();
            timer = false;
            TimeSpan ts = stopwatch.Elapsed;
            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
            ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
            LogWriter log = new LogWriter(manager);
            log.Write(mDBPath, elapsedTime);
        }

        /// <summary>
        /// Fired every second updates UI
        /// </summary>
        /// <param name="sender">Dispatach Timer</param>
        /// <param name="args"></param>
        public void DispatcherTimer_Tick(object sender, EventArgs args)
        {
            int geotagCount = manager.updateGeoTagCount;
            int count = geotagCount - startCount;
            SpeedLabel.Content = "Items/sec: " + count;
            startCount = geotagCount;
            refreshUI();
        }

        private void refreshUI()
        {
            ProgressLabel.Content = manager.updateProgessMessage;
            ProgressBar1.Value = manager.updateProgessValue;
            PhotoCountLabel.Content = "Processing photo: "  + (manager.updatePhotoCount - manager.updatePhotoQueueCount) + " of " + manager.updatePhotoCount;
            RecordCountLabel.Content = "Records to process: " + manager.updateRecordCount;
            GeotagLabel.Content = "Geotag Count: " + manager.updateGeoTagCount;
            RecordDictLabel.Content = "Record Dictionary: " + manager.updateRecordDictCount;
            PhotoQueueLabel.Content = "Photo Queue: " + manager.updatePhotoQueueCount;
            BitmapQueueLabel.Content = "Bitmap Queue: " + manager.updateBitmapQueueCount;
            NoRecordLabel.Content = "Photos with no record: " + manager.updateNoRecordCount;
            DuplicateLabel.Content = "Duplicate Records: " + manager.updateDuplicateCount;
            PhotoErrorLabel.Content = "Photo Name Errors: " + manager.updatePhotoNameError;
        }

        private void Timer()
        {
            timer = true;
            stopwatch = new Stopwatch();
            stopwatch.Start();
            
            Task sw = Task.Factory.StartNew(() =>
            {
                
                while (timer)
                {
                    TimeSpan ts = stopwatch.Elapsed;
                    string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                    ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds / 10);
                    Dispatcher.Invoke((Action)(() =>
                    {
                        TimeLabel.Content = elapsedTime;

                    }));
                    Thread.Sleep(10);
                }
            });
        }

        private void hideProgressBar()
        {
            Dispatcher.Invoke((Action)(() => {
                ProgressBar1.Visibility = Visibility.Hidden;
                ProgressText.Visibility = Visibility.Hidden;
                ProgressLabel.Visibility = Visibility.Hidden;
            }));
        }
        private void showProgressBar()
        {
            Dispatcher.Invoke((Action)(() => {
                ProgressBar1.Visibility = Visibility.Visible;
                ProgressText.Visibility = Visibility.Visible;
                ProgressLabel.Visibility = Visibility.Visible;
            }));
        }

        private void progressIndeterminate(Boolean isIndeterminate)
        {
            Dispatcher.Invoke((Action)(() => {
                if (isIndeterminate)
                {
                    ProgressBar1.IsIndeterminate = true;
                }
                else
                {
                    ProgressBar1.IsIndeterminate = false;
                }

            }));
        }

        
    }
}
